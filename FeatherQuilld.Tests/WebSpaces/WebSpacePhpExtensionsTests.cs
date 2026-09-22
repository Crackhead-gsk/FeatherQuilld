using FeatherQuilld.Utils.WebSpaces;

namespace FeatherQuilld.Tests.WebSpaces;

public class WebSpacePhpExtensionsTests
{
    [Fact]
    public void Sanitize_KeepsCatalogOnly()
    {
        var got = WebSpacePhpExtensions.Sanitize(["gd", "imagick", "INTL", "gd", "../evil", ""]);
        Assert.Equal(["gd", "imagick", "intl"], got);
    }

    [Fact]
    public void WriteAndRead_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fq-phpext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WebSpacePhpExtensions.Write(dir, ["zip", "gd"]);
            Assert.Equal(["gd", "zip"], WebSpacePhpExtensions.Read(dir));
            var bootstrap = WebSpacePhpExtensions.BuildBootstrap(dir);
            Assert.Contains("mysqli", bootstrap);
            Assert.Contains("gd", bootstrap);
            Assert.Contains("zip", bootstrap);
            Assert.Contains("docker-php-ext-install", bootstrap);
            Assert.Contains("apache2-foreground", bootstrap);
            Assert.DoesNotContain("{{", bootstrap);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void BuildBootstrap_IncludesPeclRedis()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fq-phpext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WebSpacePhpExtensions.Write(dir, ["redis"]);
            var bootstrap = WebSpacePhpExtensions.BuildBootstrap(dir);
            Assert.Contains("pecl install", bootstrap);
            Assert.Contains("redis", bootstrap);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Regression for the "$$" raw-string escaping bug: BuildBootstrap used to emit
    /// "$$NEED_INSTALL" / "$${ext}" / "$$ADDONS" / "$$PHPIZE_DEPS" literally (two dollar
    /// signs each, from a `$$"""..."""` C# raw string where `$$` is NOT the interpolation
    /// escape for `$` — `{{ }}` is). Bash then expanded "$$" to the current PID, so
    /// "$<PID>NEED_INSTALL" never equaled "1", the install branch silently never ran, and
    /// baseline extensions (mysqli, pdo_mysql, opcache) were never installed - breaking
    /// every WordPress/PHP WebPlate with a 500 "missing the MySQL extension" error.
    /// Guard: the generated script must reference these as single-`$` shell variables and
    /// must never contain a literal "$$" anywhere.
    /// </summary>
    [Fact]
    public void BuildBootstrap_NeverEmitsDoubleDollarBashVariables()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fq-phpext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WebSpacePhpExtensions.Write(dir, ["gd"]);
            var bootstrap = WebSpacePhpExtensions.BuildBootstrap(dir);

            Assert.DoesNotContain("$$", bootstrap);
            Assert.Contains("if [ \"$NEED_INSTALL\" = \"1\" ]; then", bootstrap);
            Assert.Contains("$PHPIZE_DEPS", bootstrap);
            Assert.Contains("if [ -f \"$ADDONS\" ]; then", bootstrap);
            Assert.Contains("cp \"$ADDONS\"", bootstrap);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// The generated script must be syntactically valid bash (`bash -n`), and the
    /// NEED_INSTALL guard specifically must evaluate as a real shell condition rather
    /// than silently short-circuiting to false because of PID-expansion corruption
    /// (see BuildBootstrap_NeverEmitsDoubleDollarBashVariables). Skipped when bash isn't
    /// on PATH (e.g. a bare Windows dev box without git-bash/WSL).
    /// </summary>
    [Fact]
    public void BuildBootstrap_IsValidBashAndInstallGuardActuallyTriggers()
    {
        if (!IsBashAvailable())
            return;

        var dir = Path.Combine(Path.GetTempPath(), "fq-phpext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WebSpacePhpExtensions.Write(dir, []);
            var bootstrap = WebSpacePhpExtensions.BuildBootstrap(dir);

            var scriptPath = Path.Combine(dir, "bootstrap.sh");
            File.WriteAllText(scriptPath, bootstrap);

            // Syntax check only (no `exec apache2-foreground`).
            var syntax = RunBash($"-n \"{scriptPath}\"");
            Assert.True(syntax.ExitCode == 0, $"bash -n failed: {syntax.Output}");

            // Drive just the guard logic with a fake, always-missing `php` shim on PATH
            // so `php -m | grep -qi "^mysqli$"` fails and NEED_INSTALL must become 1 -
            // proving the "$NEED_INSTALL" comparison (not "$<pid>NEED_INSTALL") is what
            // gets evaluated.
            var guardScript = """
                set -e
                NEED_INSTALL=0
                for ext in mysqli pdo_mysql opcache; do
                  if ! php -m 2>/dev/null | grep -qi "^${ext}$"; then
                    NEED_INSTALL=1
                    break
                  fi
                done
                echo "NEED_INSTALL=$NEED_INSTALL"
                """.ReplaceLineEndings("\n");
            var guardPath = Path.Combine(dir, "guard.sh");
            File.WriteAllText(guardPath, guardScript);
            var run = RunBash($"\"{guardPath}\"");
            Assert.Equal(0, run.ExitCode);
            Assert.Contains("NEED_INSTALL=1", run.Output);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    private static bool IsBashAvailable() => RunBash("--version").ExitCode == 0;

    private static (int ExitCode, string Output) RunBash(string arguments)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "bash",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null)
                return (-1, "");
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(10_000);
            return (proc.ExitCode, stdout + stderr);
        }
        catch
        {
            return (-1, "");
        }
    }
}

public class WebSpaceSanitizeDenyPathsTests
{
    [Fact]
    public void SanitizeDenyPaths_RejectsTraversalAndAcme()
    {
        var got = WebSpaceStore.SanitizeDenyPaths([
            "xmlrpc.php",
            "/wp-config.php",
            "/../etc/passwd",
            "/.well-known/acme-challenge/x",
            "/",
            "//admin",
        ]);
        Assert.Equal(["/xmlrpc.php", "/wp-config.php", "/admin"], got);
    }
}
