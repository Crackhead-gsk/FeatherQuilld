using FeatherQuilld.Plugins.Abstractions;
using FeatherQuilld.Plugins.Context;
using FeatherQuilld.Plugins.Events;
using FeatherQuilld.Plugins.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FeatherQuilld.Plugins.Hello;

/// <summary>Sample plugin demonstrating routes, events, settings, Host API, and lifecycle.</summary>
public sealed class HelloPlugin : IPlugin, IPluginLifecycle
{
    private PluginContext? _context;

    public PluginMetadata Metadata { get; } = new()
    {
        Id = "hello",
        Name = "Hello Plugin",
        Version = "0.3.0",
        Description = "Sample plugin: routes, hooks, settings, Host API, Cancel/Replace demos.",
        Author = "FeatherQuilld",
        MinHostVersion = "0.1.0",
        Capabilities =
        [
            PluginCapabilities.RoutesPublic,
            PluginCapabilities.RoutesSystem,
            PluginCapabilities.HostInfo,
            PluginCapabilities.WebSpacesRead,
        ],
        Settings = new Dictionary<string, object?>
        {
            ["greeting"] = "Hello from plugin!",
        },
    };

    public void Configure(PluginContext context)
    {
        _context = context;
        context.Logger.LogInformation("Hello from {Name}!", Metadata.Name);

        var greeting = context.Settings.TryGetValue("greeting", out var g) && g is not null
            ? g.ToString()
            : "Hello from plugin!";

        context.Routes
            .MapGet("/api/hello", (HttpContext _) =>
                Results.Json(new
                {
                    message = greeting,
                    plugin = Metadata.Id,
                    webspaces = context.Host?.WebSpaces.List().Count,
                }))
            .WithName("hello-greeting")
            .WithTags("Hello");

        context.Events.On<ApplicationStartedEvent>(_ =>
        {
            context.Logger.LogInformation("Host is up — {Name} is live.", Metadata.Name);
            try
            {
                var version = context.Host?.Daemon.Version;
                context.Logger.LogInformation("Host version via IPluginHost: {Version}", version);
            }
            catch (UnauthorizedAccessException)
            {
                context.Logger.LogWarning("Missing host.info capability for Daemon.Version");
            }

            return HookResult.Continue();
        });

        context.Events.On<WebSpacePowerBeforeEvent>(evt =>
        {
            context.Logger.LogInformation(
                "Power {Action} requested for {Uuid}",
                evt.Action,
                evt.WebSpaceUuid);
            return HookResult.Continue();
        });

        // Demo: Cancel health when query ?plugin_block=1 (observe in tests / manual).
        context.Events.On<HealthCheckEvent>(evt =>
        {
            if (evt.Context.Request.Query.ContainsKey("plugin_block"))
                return HookResult.Cancel();
            return HookResult.Continue();
        });

        context.Routes.Before("/api/system/*", ctx =>
        {
            context.Logger.LogDebug("Request → {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            return HookResult.Continue();
        });

        context.Routes.After("/api/system/*", (ctx, _) =>
        {
            context.Logger.LogDebug("Response ← {StatusCode} {Path}", ctx.Response.StatusCode, ctx.Request.Path);
            return HookResult.Continue();
        });
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _context?.Logger.LogInformation("{Name} StartAsync", Metadata.Name);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _context?.Logger.LogInformation("{Name} StopAsync", Metadata.Name);
        return Task.CompletedTask;
    }
}
