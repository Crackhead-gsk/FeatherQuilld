using FeatherQuilld.Utils.WebSpaces;

namespace FeatherQuilld.Tests.WebSpaces;

/// <summary>
/// Regression guards for PHP WebSpace file ownership. Apache/PHP inside the WebPlate
/// container runs as www-data (uid/gid 33), while WebPlate installers, uploads and the
/// file service all write as root, and a fusequota mount is created with --uid 0 --gid 0
/// and ignores chown issued from inside the container. Without correcting ownership on
/// the source volume from the host, the application (e.g. WordPress) has no write access
/// at all: it cannot create wp-config.php, cannot accept uploads, and cannot self-update.
/// </summary>
public class WebSpaceFsOwnershipTests
{
    private static WebSpace Php() => new()
    {
        Uuid = Guid.NewGuid(),
        Runtime = "php",
    };

    private static WebSpace Node() => new()
    {
        Uuid = Guid.NewGuid(),
        Runtime = "node",
    };

    private static WebSpace Static() => new()
    {
        Uuid = Guid.NewGuid(),
        Runtime = "static",
    };

    [Fact]
    public void IsWebServerRuntime_TrueOnlyForPhp()
    {
        Assert.True(WebSpaceFsOwnership.IsWebServerRuntime("php"));
        Assert.True(WebSpaceFsOwnership.IsWebServerRuntime("PHP"));
        Assert.False(WebSpaceFsOwnership.IsWebServerRuntime("node"));
        Assert.False(WebSpaceFsOwnership.IsWebServerRuntime("static"));
        Assert.False(WebSpaceFsOwnership.IsWebServerRuntime(""));
    }

    [Fact]
    public void MountOwnerUidGid_ReturnWwwDataForPhpOnly()
    {
        Assert.Equal(33, WebSpaceFsOwnership.MountOwnerUid("php"));
        Assert.Equal(33, WebSpaceFsOwnership.MountOwnerGid("php"));

        Assert.Null(WebSpaceFsOwnership.MountOwnerUid("node"));
        Assert.Null(WebSpaceFsOwnership.MountOwnerGid("node"));
        Assert.Null(WebSpaceFsOwnership.MountOwnerUid("static"));
        Assert.Null(WebSpaceFsOwnership.MountOwnerGid("static"));
    }

    [Fact]
    public void EnsureWebServerOwnership_NoOpForNonPhpRuntimes()
    {
        // Passing a path that does not exist would make a real chown attempt throw/log a
        // warning if this ever tried to run for non-PHP runtimes - the early return must
        // prevent that regardless of the filesystem state.
        var missingPath = Path.Combine(Path.GetTempPath(), "fq-ownership-missing-" + Guid.NewGuid().ToString("N"));
        WebSpaceFsOwnership.EnsureWebServerOwnership(Node(), missingPath);
        WebSpaceFsOwnership.EnsureWebServerOwnership(Static(), missingPath);
        // No exception thrown = pass; there's nothing else to assert on a static no-op.
    }

    [Fact]
    public void EnsureWebServerOwnership_NoOpWhenSourceDirectoryDoesNotExist()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), "fq-ownership-missing-" + Guid.NewGuid().ToString("N"));
        // PHP runtime but the directory genuinely doesn't exist - must not throw even
        // though the runtime check passes.
        WebSpaceFsOwnership.EnsureWebServerOwnership(Php(), missingPath);
    }

    [Fact]
    public void EnsureWebServerOwnership_ChownsExistingPhpDirectoryToWwwData()
    {
        if (OperatingSystem.IsWindows())
            return; // chown is a POSIX operation; this asserts the actual Linux behavior.

        var dir = Path.Combine(Path.GetTempPath(), "fq-ownership-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "wp-config.php");
        File.WriteAllText(file, "<?php // seeded by installer as root\n");
        try
        {
            WebSpaceFsOwnership.EnsureWebServerOwnership(Php(), dir);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "stat",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("%u:%g");
            psi.ArgumentList.Add(file);
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var owner = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            Assert.Equal($"{WebSpaceFsOwnership.WebServerUid}:{WebSpaceFsOwnership.WebServerGid}", owner);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }
}
