using FeatherQuilld.Plugins.Events;
using FeatherQuilld.Plugins.Metadata;
using FeatherQuilld.Plugins.Routing;
using FeatherQuilld.Utils.Config;
using FeatherQuilld.Utils.Logger;
using FeatherQuilld.Utils.Plugins;
using FeatherQuilld.Utils.Plugins.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using AppConfig = FeatherQuilld.Utils.Config.Config;

namespace FeatherQuilld.Tests.Plugins;

public class PluginPlatformTests
{
    [Fact]
    public void GetPluginSettings_MergesManifestThenHostWins()
    {
        using var logger = CreateLogger();
        var config = new AppConfig
        {
            Plugins =
            {
                Enabled = true,
                Settings =
                {
                    ["demo"] = new Dictionary<string, object?>
                    {
                        ["greeting"] = "from-host",
                        ["extra"] = 42,
                    },
                },
            },
        };
        var manager = new PluginManager(config, logger);
        var meta = new PluginMetadata
        {
            Id = "demo",
            Name = "Demo",
            Version = "1.0.0",
            Settings = new Dictionary<string, object?> { ["greeting"] = "from-code" },
        };
        var manifest = new PluginManifest
        {
            Settings = new Dictionary<string, object?> { ["greeting"] = "from-manifest", ["color"] = "blue" },
        };

        var settings = manager.GetPluginSettings("demo", meta, manifest);

        Assert.Equal("from-host", settings["greeting"]?.ToString());
        Assert.Equal("blue", settings["color"]?.ToString());
        Assert.Equal(42, Convert.ToInt32(settings["extra"]));
    }

    [Fact]
    public void RouteRegistry_SystemRoute_RequiresRoutesSystemCapability()
    {
        var reg = new RouteRegistry();
        reg.BeginPlugin("p1", [PluginCapabilities.RoutesPublic], strict: false);

        var ex = Assert.Throws<PluginCapabilityException>(() =>
            reg.MapGet("/api/system/health", (HttpContext _) => Results.Ok()));

        Assert.Equal(PluginCapabilities.RoutesSystem, ex.Capability);
        reg.EndPlugin();
    }

    [Fact]
    public void RouteRegistry_PublicRoute_AllowedWithRoutesPublic()
    {
        var reg = new RouteRegistry();
        reg.BeginPlugin("p1", [PluginCapabilities.RoutesPublic], strict: false);
        var builder = reg.MapGet("/api/hello", (HttpContext _) => Results.Ok());
        Assert.Equal("p1", builder.Descriptor.PluginId);
        reg.EndPlugin();
    }

    [Fact]
    public void RouteRegistry_DisablePlugin_MarksRoutesAndIsDisabledRoute()
    {
        var reg = new RouteRegistry();
        reg.BeginPlugin("p1", [PluginCapabilities.RoutesPublic], strict: false);
        reg.MapGet("/api/hello", (HttpContext _) => Results.Ok());
        reg.EndPlugin();

        Assert.False(reg.IsDisabledRoute("GET", "/api/hello"));
        reg.DisablePlugin("p1");
        Assert.True(reg.IsDisabledRoute("GET", "/api/hello"));
        Assert.True(reg.Routes[0].Disabled);
    }

    [Fact]
    public void OwningEventBus_Dispose_Unsubscribes()
    {
        var bus = new Utils.Plugins.Events.EventBus();
        var owning = new OwningEventBus(bus);
        var hits = 0;
        owning.On<ApplicationStartedEvent>(_ =>
        {
            hits++;
            return HookResult.Continue();
        });

        bus.Emit(new ApplicationStartedEvent { Services = new ServiceCollection().BuildServiceProvider() });
        Assert.Equal(1, hits);

        owning.Dispose();
        bus.Emit(new ApplicationStartedEvent { Services = new ServiceCollection().BuildServiceProvider() });
        Assert.Equal(1, hits);
    }

    private static Logger CreateLogger()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fq-plugin-plat-" + Guid.NewGuid().ToString("N"));
        return new Logger(new LoggerOptions { Directory = dir, Debug = false, MaxArchives = 0 });
    }
}
