using FeatherQuilld.Plugins.Hello;
using FeatherQuilld.Plugins.Context;
using FeatherQuilld.Plugins.Events;
using FeatherQuilld.Plugins.Metadata;
using FeatherQuilld.Utils.Plugins.Events;
using FeatherQuilld.Utils.Plugins.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeatherQuilld.Tests.Plugins;

public class HelloPluginTests
{
    [Fact]
    public void Metadata_MatchesManifestIdentity()
    {
        var plugin = new HelloPlugin();
        Assert.Equal("hello", plugin.Metadata.Id);
        Assert.Equal("Hello Plugin", plugin.Metadata.Name);
        Assert.Equal("0.3.0", plugin.Metadata.Version);
        Assert.Contains(PluginCapabilities.RoutesPublic, plugin.Metadata.Capabilities);
        Assert.False(string.IsNullOrWhiteSpace(plugin.Metadata.Author));
    }

    [Fact]
    public void Configure_RegistersGreetingRoute_AndStartedHook()
    {
        var plugin = new HelloPlugin();
        var routes = new RouteRegistry();
        var events = new EventBus();
        var services = new ServiceCollection();

        routes.BeginPlugin(plugin.Metadata.Id, plugin.Metadata.Capabilities, strict: false);
        try
        {
            plugin.Configure(new PluginContext
            {
                Metadata = plugin.Metadata,
                Services = services,
                Events = events,
                Routes = routes,
                Logger = NullLogger.Instance,
            });
        }
        finally
        {
            routes.EndPlugin();
        }

        Assert.Contains(routes.Routes, r =>
            r.Method == "GET"
            && r.Pattern == "/api/hello"
            && r.Name == "hello-greeting"
            && r.PluginId == "hello");

        // ApplicationStarted handler should Continue without throwing.
        var result = events.Emit(new ApplicationStartedEvent { Services = services.BuildServiceProvider() });
        Assert.Equal(HookAction.Continue, result.Action);
    }
}
