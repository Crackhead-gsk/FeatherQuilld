using System.Collections.Concurrent;

namespace FeatherQuilld.Utils.Sftp;

/// <summary>
/// Per-connection authentication attempt accounting for the embedded SSH server.
///
/// The panel config limits password attempts (default 3) and publickey attempts
/// (default 20) per connection. These limits cannot be delegated to the SSH
/// library's MaxAuthAttempts: the library counts EVERY auth request against it —
/// the "none" probe, each publickey probe, and then the password — so a limit of
/// 3 is exhausted exactly by a successful key-first login (probe + key probe +
/// signed key) and the library kills the connection right after
/// USERAUTH_SUCCESS. Instead, the library gets a generous total budget and the
/// per-method limits are enforced here, counting only the method each limit
/// governs.
///
/// Keyed by the SSH session id (available in the auth context once the key
/// exchange has completed); entries are removed when the connection handler
/// ends. A limit of &lt;= 0 means unlimited.
/// </summary>
internal sealed class SftpAuthAttemptLimiter
{
    private sealed class Counts
    {
        public int PasswordFailures;
        public int PublicKeyFailures;
    }

    private readonly ConcurrentDictionary<string, Counts> _counts = new(StringComparer.Ordinal);

    public bool AllowPassword(string sessionKey, int maxAttempts) =>
        Allow(sessionKey, maxAttempts, static c => c.PasswordFailures);

    public bool AllowPublicKey(string sessionKey, int maxAttempts) =>
        Allow(sessionKey, maxAttempts, static c => c.PublicKeyFailures);

    public void RecordPasswordFailure(string sessionKey) =>
        Record(sessionKey, static c => c.PasswordFailures++);

    public void RecordPublicKeyFailure(string sessionKey) =>
        Record(sessionKey, static c => c.PublicKeyFailures++);

    public void Remove(string sessionKey)
    {
        if (!string.IsNullOrEmpty(sessionKey))
            _counts.TryRemove(sessionKey, out _);
    }

    private bool Allow(string sessionKey, int maxAttempts, Func<Counts, int> selector)
    {
        if (maxAttempts <= 0 || string.IsNullOrEmpty(sessionKey))
            return true;

        var counts = _counts.GetOrAdd(sessionKey, static _ => new Counts());
        lock (counts)
        {
            return selector(counts) < maxAttempts;
        }
    }

    private void Record(string sessionKey, Action<Counts> recorder)
    {
        if (string.IsNullOrEmpty(sessionKey))
            return;

        var counts = _counts.GetOrAdd(sessionKey, static _ => new Counts());
        lock (counts)
        {
            recorder(counts);
        }
    }
}
