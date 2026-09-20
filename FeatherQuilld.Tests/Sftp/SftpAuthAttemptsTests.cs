using FeatherQuilld.Utils.Sftp;

namespace FeatherQuilld.Tests.Sftp;

/// <summary>
/// Regression tests for the per-connection auth attempt limits.
///
/// The bug this guards against: the panel's password-attempt limit (default 3)
/// was passed 1:1 as the SSH library's MaxAuthAttempts, but the library counts
/// EVERY auth request (the "none" probe, each publickey probe, then the
/// password), so a successful key-first login — probe + key probe + signed key —
/// exhausted the limit exactly and the library threw right after sending
/// USERAUTH_SUCCESS. FileZilla-style key-first clients therefore failed 100% of
/// the time.
/// </summary>
public sealed class SftpAuthAttemptsTests
{
    [Fact]
    public void PasswordLimit_AllowsExactlyMaxAttempts()
    {
        var limiter = new SftpAuthAttemptLimiter();
        const string session = "session-a";

        Assert.True(limiter.AllowPassword(session, 3));
        limiter.RecordPasswordFailure(session);

        Assert.True(limiter.AllowPassword(session, 3));
        limiter.RecordPasswordFailure(session);

        Assert.True(limiter.AllowPassword(session, 3));
        limiter.RecordPasswordFailure(session);

        // Fourth attempt on the same connection must be rejected.
        Assert.False(limiter.AllowPassword(session, 3));
    }

    [Fact]
    public void PublicKeyLimit_IsCountedSeparatelyFromPassword()
    {
        var limiter = new SftpAuthAttemptLimiter();
        const string session = "session-b";

        // Key probes WITHOUT a signature do not count (they are not attempts).
        // Exhausting the key limit does not affect the password budget.
        for (var i = 0; i < 20; i++)
        {
            Assert.True(limiter.AllowPublicKey(session, 20));
            limiter.RecordPublicKeyFailure(session);
        }
        Assert.False(limiter.AllowPublicKey(session, 20));

        Assert.True(limiter.AllowPassword(session, 3));
    }

    [Fact]
    public void Limits_ArePerConnection()
    {
        var limiter = new SftpAuthAttemptLimiter();

        for (var i = 0; i < 3; i++)
        {
            limiter.RecordPasswordFailure("session-1");
        }
        Assert.False(limiter.AllowPassword("session-1", 3));

        // A different connection has its own budget.
        Assert.True(limiter.AllowPassword("session-2", 3));
    }

    [Fact]
    public void LimitZeroOrNegative_MeansUnlimited()
    {
        var limiter = new SftpAuthAttemptLimiter();
        const string session = "session-c";

        for (var i = 0; i < 50; i++)
        {
            Assert.True(limiter.AllowPassword(session, 0));
            limiter.RecordPasswordFailure(session);
        }
        Assert.True(limiter.AllowPassword(session, -1));
    }

    [Fact]
    public void Remove_ResetsTheConnection()
    {
        var limiter = new SftpAuthAttemptLimiter();
        const string session = "session-d";

        for (var i = 0; i < 3; i++)
            limiter.RecordPasswordFailure(session);
        Assert.False(limiter.AllowPassword(session, 3));

        limiter.Remove(session);

        Assert.True(limiter.AllowPassword(session, 3));
    }

    [Fact]
    public void EmptySessionKey_SkipsLimiting()
    {
        var limiter = new SftpAuthAttemptLimiter();

        // Session id unknown (pre-KEX auth contexts can't happen in practice,
        // but the limiter must not count them against each other or throw).
        Assert.True(limiter.AllowPassword("", 3));
        limiter.RecordPasswordFailure("");
        Assert.True(limiter.AllowPassword("", 3));
    }
}
