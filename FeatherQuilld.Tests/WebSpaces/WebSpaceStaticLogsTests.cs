using FeatherQuilld.Utils.Config;
using FeatherQuilld.Utils.Docker;
using FeatherQuilld.Utils.Proxy;
using FeatherQuilld.Utils.Remote;
using FeatherQuilld.Utils.Sftp;
using FeatherQuilld.Utils.WebSpaces;
using AppConfig = FeatherQuilld.Utils.Config.Config;

namespace FeatherQuilld.Tests.WebSpaces;

/// <summary>
/// Regression guards for WebSpace logs: static WebSpaces have no runtime container, so
/// GET /logs and the console socket used to return a static, useless placeholder line
/// ("(static WebSpace no runtime container logs)") instead of anything an operator could
/// act on. Static WebSpaces now surface the web server's own access/error log for each of
/// their domains, and the follow/stream path tails newly appended access log lines instead
/// of repeating the placeholder forever.
/// </summary>
public sealed class WebSpaceStaticLogsTests
{
    private static (WebSpaceStore Store, WebSpace Space, AppConfig Config) BuildStoreWithStaticSpace(
        params string[] domains)
    {
        var uuid = Guid.NewGuid();
        var testRoot = Path.Combine(Path.GetTempPath(), "fq-logs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        var config = new AppConfig
        {
            System =
            {
                RootDirectory = testRoot,
                Data = Path.Combine(testRoot, "volumes"),
                VmountDirectory = Path.Combine(testRoot, "vmounts"),
                TmpDirectory = Path.Combine(testRoot, "tmp"),
                BackupDirectory = Path.Combine(testRoot, "backups"),
                DiskLimiterMode = "none",
            },
        };
        config.System.Proxy.Enabled = false;
        config.Docker.RuntimeReconciliation.Enabled = false;
        config.System.Quotas.Enabled = false;

        var panel = new StaticSpacePanel(uuid, domains);
        var store = new WebSpaceStore(
            config,
            panel,
            new ReverseProxyManager(config),
            new PortAllocator(config.System.Proxy),
            new WebSpaceInstaller(config.Docker),
            new WebSpaceRuntime(config.Docker));

        var space = store.CreateFromPanel(new CreateWebSpaceRequest
        {
            Uuid = uuid,
            SkipScripts = true,
            StartOnCompletion = false,
        });

        return (store, space, config);
    }

    [Fact]
    public void GetRuntimeLogs_StaticWebSpace_ReturnsAccessLogInsteadOfPlaceholder()
    {
        var (store, space, config) = BuildStoreWithStaticSpace("static.example.com");
        try
        {
            var accessPath = ProxyAccessLogs.AccessLogPath(config.System.RootDirectory, space.Uuid, "static.example.com");
            ProxyAccessLogs.EnsureDir(config.System.RootDirectory, space.Uuid);
            File.WriteAllText(accessPath,
                "1.2.3.4 - - [01/Jan/2026:00:00:00 +0000] \"GET / HTTP/1.1\" 200 1234\n");

            var logs = store.GetRuntimeLogs(space.Uuid, lines: 100);

            Assert.DoesNotContain("no runtime container logs", logs);
            Assert.Contains("static.example.com", logs);
            Assert.Contains("GET / HTTP/1.1", logs);
        }
        finally
        {
            try { Directory.Delete(config.System.RootDirectory, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void GetRuntimeLogs_StaticWebSpace_NoRequestsYet_SaysSoInsteadOfPlaceholder()
    {
        var (store, space, config) = BuildStoreWithStaticSpace("empty.example.com");
        try
        {
            var logs = store.GetRuntimeLogs(space.Uuid, lines: 100);

            Assert.DoesNotContain("no runtime container logs", logs);
            Assert.Contains("empty.example.com", logs);
            Assert.Contains("no requests logged yet", logs);
        }
        finally
        {
            try { Directory.Delete(config.System.RootDirectory, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void GetRuntimeLogs_StaticWebSpace_IncludesErrorLogWhenPresent()
    {
        var (store, space, config) = BuildStoreWithStaticSpace("erroring.example.com");
        try
        {
            var errorPath = ProxyAccessLogs.ErrorLogPath(config.System.RootDirectory, space.Uuid, "erroring.example.com");
            ProxyAccessLogs.EnsureDir(config.System.RootDirectory, space.Uuid);
            File.WriteAllText(errorPath, "2026/01/01 00:00:00 [error] open() failed (2: No such file or directory)\n");

            var logs = store.GetRuntimeLogs(space.Uuid, lines: 100);

            Assert.Contains("erroring.example.com error log", logs);
            Assert.Contains("open() failed", logs);
        }
        finally
        {
            try { Directory.Delete(config.System.RootDirectory, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task FollowRuntimeLogsAsync_StaticWebSpace_StreamsNewlyAppendedAccessLines()
    {
        var (store, space, config) = BuildStoreWithStaticSpace("streamed.example.com");
        try
        {
            var accessPath = ProxyAccessLogs.AccessLogPath(config.System.RootDirectory, space.Uuid, "streamed.example.com");
            ProxyAccessLogs.EnsureDir(config.System.RootDirectory, space.Uuid);
            File.WriteAllText(accessPath, "");

            using var cts = new CancellationTokenSource();
            var received = new List<string>();
            var followTask = Task.Run(async () =>
            {
                await foreach (var line in store.FollowRuntimeLogsAsync(space.Uuid, sinceLines: 0, cts.Token))
                {
                    received.Add(line);
                    if (received.Count >= 1)
                        break;
                }
            });

            // Give the follower a moment to open the file at offset 0, then append a line.
            await Task.Delay(200);
            await File.AppendAllTextAsync(accessPath,
                "5.6.7.8 - - [01/Jan/2026:00:00:01 +0000] \"GET /new HTTP/1.1\" 200 42\n");

            var completed = await Task.WhenAny(followTask, Task.Delay(TimeSpan.FromSeconds(10)));
            cts.Cancel();

            Assert.Same(followTask, completed);
            Assert.Contains(received, l => l.Contains("GET /new HTTP/1.1"));
        }
        finally
        {
            try { Directory.Delete(config.System.RootDirectory, true); } catch { /* ignore */ }
        }
    }

    private sealed class StaticSpacePanel(Guid uuid, string[] domains) : IPanelClient
    {
        public Task<AppConfig> FetchRuntimeConfigAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppConfig());

        public Task<string> FetchRuntimeConfigYamlAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);

        public Task<PanelHealthResponse> FetchHealthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PanelHealthResponse { Success = true });

        public Task<PanelWebSpaceConfig> FetchWebSpaceAsync(Guid requested, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PanelWebSpaceConfig
            {
                Uuid = uuid,
                Name = "static-test",
                Domains = domains.ToList(),
                DomainRoutes = domains
                    .Select(d => new PanelDomainRoute { Domain = d, Type = "primary" })
                    .ToList(),
                Webplate = new PanelWebPlateRef { Id = "static-test-plate", Runtime = "static" },
                Build = new PanelWebSpaceBuild { DiskSpace = 0 },
            });

        public Task<PanelInstallScript> FetchWebSpaceInstallAsync(Guid requested, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PanelInstallScript { Script = "" });

        public Task ReportWebSpaceInstallAsync(
            Guid requested,
            bool successful,
            bool reinstall = false,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SyncWebSpaceStateAsync(
            Guid requested,
            int backendPort,
            string state,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReportTransferAsync(Guid requested, bool successful, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReportActivitiesAsync(
            IReadOnlyList<PanelActivityEntry> entries,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<SftpAuthResult?> AuthenticateSftpAsync(
            string type,
            string username,
            string password,
            string? publicKey = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SftpAuthResult?>(null);

        public Task AcmeDnsAsync(
            Guid requested,
            string action,
            string name,
            string content,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
