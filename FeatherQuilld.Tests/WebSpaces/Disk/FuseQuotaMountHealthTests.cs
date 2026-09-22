using FeatherQuilld.Utils.WebSpaces.Disk;

namespace FeatherQuilld.Tests.WebSpaces.Disk;

/// <summary>
/// Regression guards for stale fusequota mount detection/repair. A fusequota mount that
/// outlives its daemon (e.g. after the FeatherQuilld process was restarted or killed)
/// stays registered in the kernel mount table but fails every access with ENOTCONN, so a
/// static WebSpace answers 404/502 forever although its files are present on disk. The old
/// code's "already running? spawn" check never noticed this, because
/// Directory.CreateDirectory on the stale mountpoint throws "The file ... already exists"
/// and aborted the whole attach before any remount was attempted.
/// </summary>
public class FuseQuotaMountHealthTests
{
    [Fact]
    public void IsMounted_FalseForOrdinaryDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fq-mount-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.False(FuseQuotaLimiter.IsMounted(dir));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void IsMounted_FalseForNonExistentPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fq-mount-missing-" + Guid.NewGuid().ToString("N"));
        Assert.False(FuseQuotaLimiter.IsMounted(dir));
    }

    [Fact]
    public void IsMountAccessible_TrueForOrdinaryDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fq-mount-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.True(FuseQuotaLimiter.IsMountAccessible(dir));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void IsMountAccessible_TrueForNonExistentPath()
    {
        // A path that simply doesn't exist yet is not "dead" - Setup() will create it.
        // Only an existing-but-broken (ENOTCONN) mount should read as inaccessible.
        var dir = Path.Combine(Path.GetTempPath(), "fq-mount-missing-" + Guid.NewGuid().ToString("N"));
        Assert.True(FuseQuotaLimiter.IsMountAccessible(dir));
    }

    /// <summary>
    /// Real bind-mount proof on Linux: IsMounted must find a genuine mount point in
    /// /proc/self/mounts and correctly report false again once it's unmounted, and
    /// IsMountAccessible must stay true for a healthy (non-broken) mount. Skipped when not
    /// running as root (mount/umount require CAP_SYS_ADMIN) or on non-Linux.
    /// </summary>
    [Fact]
    public void IsMounted_DetectsARealBindMount_ThenClearsAfterUnmount()
    {
        if (!OperatingSystem.IsLinux() || !IsRunningAsRoot())
            return;

        var source = Path.Combine(Path.GetTempPath(), "fq-mount-src-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(Path.GetTempPath(), "fq-mount-tgt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        var mounted = false;
        try
        {
            mounted = RunProcess("mount", "--bind", source, target) == 0;
            if (!mounted)
                return; // environment doesn't allow bind mounts (e.g. sandboxed container); skip

            Assert.True(FuseQuotaLimiter.IsMounted(target));
            Assert.True(FuseQuotaLimiter.IsMountAccessible(target));

            RunProcess("umount", target);
            mounted = false;

            Assert.False(FuseQuotaLimiter.IsMounted(target));
        }
        finally
        {
            if (mounted)
                RunProcess("umount", "-l", target);
            try { Directory.Delete(source, true); } catch { /* ignore */ }
            try { Directory.Delete(target, true); } catch { /* ignore */ }
        }
    }

    private static bool IsRunningAsRoot() => RunProcessCapture("id", "-u").Trim() == "0";

    private static int RunProcess(string fileName, params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc is null)
            return -1;
        proc.WaitForExit(10_000);
        return proc.ExitCode;
    }

    private static string RunProcessCapture(string fileName, params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc is null)
            return "";
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);
        return output;
    }
}
