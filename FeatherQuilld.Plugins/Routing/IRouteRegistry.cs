using FeatherQuilld.Plugins.Events;
using FeatherQuilld.Plugins.Metadata;
using Microsoft.AspNetCore.Http;

namespace FeatherQuilld.Plugins.Routing;

public interface IRouteRegistry
{
    RouteBuilder MapGet(string pattern, Delegate handler, string? name = null);
    RouteBuilder MapPost(string pattern, Delegate handler, string? name = null);
    RouteBuilder MapPut(string pattern, Delegate handler, string? name = null);
    RouteBuilder MapDelete(string pattern, Delegate handler, string? name = null);
    RouteBuilder MapPatch(string pattern, Delegate handler, string? name = null);
    void Before(string pattern, Func<HttpContext, HookResult> hook, int priority = 0);
    void After(string pattern, Func<HttpContext, object?, HookResult> hook, int priority = 0);
    void Alter(string pattern, Action<RouteDescriptor> alter);
}

public sealed class RouteBuilder
{
    public required RouteDescriptor Descriptor { get; init; }

    public RouteBuilder WithName(string name)
    {
        Descriptor.Name = name;
        return this;
    }

    public RouteBuilder WithTags(params string[] tags)
    {
        Descriptor.Tags = tags;
        return this;
    }
}

public sealed class RouteDescriptor
{
    public required string Pattern { get; init; }
    public required string Method { get; init; }
    public required Delegate Handler { get; init; }
    public string? Name { get; set; }
    public string[] Tags { get; set; } = [];
    public string? PluginId { get; set; }

    /// <summary>Soft-unloaded plugins keep the descriptor but are skipped at dispatch.</summary>
    public bool Disabled { get; set; }
}

/// <summary>Thrown when a plugin maps a route without the required capability.</summary>
public sealed class PluginCapabilityException : InvalidOperationException
{
    public PluginCapabilityException(string pluginId, string capability, string pattern)
        : base($"Plugin '{pluginId}' lacks capability '{capability}' required for route '{pattern}'.")
    {
        PluginId = pluginId;
        Capability = capability;
        Pattern = pattern;
    }

    public string PluginId { get; }
    public string Capability { get; }
    public string Pattern { get; }
}

/// <summary>Helpers for deciding which capability a route pattern requires.</summary>
public static class RouteCapabilityRequirements
{
    public static string RequiredCapability(string pattern)
    {
        var path = pattern.StartsWith('/') ? pattern : "/" + pattern;
        if (path.StartsWith("/api/system", StringComparison.OrdinalIgnoreCase))
            return PluginCapabilities.RoutesSystem;
        return PluginCapabilities.RoutesPublic;
    }
}
