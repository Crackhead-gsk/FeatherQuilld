using System.Text.RegularExpressions;
using FeatherQuilld.Plugins.Events;
using FeatherQuilld.Plugins.Routing;
using Microsoft.AspNetCore.Http;
using SdkRouteBuilder = FeatherQuilld.Plugins.Routing.RouteBuilder;

namespace FeatherQuilld.Utils.Plugins.Routing;

/// <summary>Collects plugin routes and route hooks until the HTTP pipeline is built.</summary>
public sealed class RouteRegistry : IRouteRegistry
{
    private readonly List<RouteDescriptor> _routes = [];
    private readonly List<RouteHook> _beforeHooks = [];
    private readonly List<RouteHook> _afterHooks = [];
    private readonly List<(string Pattern, Action<RouteDescriptor> Alter)> _alterations = [];
    private readonly HashSet<string> _disabledPlugins = new(StringComparer.OrdinalIgnoreCase);

    private string? _currentPluginId;
    private IReadOnlyList<string> _currentCapabilities = [];
    private bool _strictCapabilities;

    public IReadOnlyList<RouteDescriptor> Routes => _routes;

    /// <summary>Called by PluginManager while configuring a single plugin.</summary>
    public void BeginPlugin(string pluginId, IReadOnlyList<string> capabilities, bool strict)
    {
        _currentPluginId = pluginId;
        _currentCapabilities = capabilities ?? [];
        _strictCapabilities = strict;
    }

    public void EndPlugin()
    {
        _currentPluginId = null;
        _currentCapabilities = [];
        _strictCapabilities = false;
    }

    public void DisablePlugin(string pluginId)
    {
        _disabledPlugins.Add(pluginId);
        foreach (var route in _routes)
        {
            if (string.Equals(route.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
                route.Disabled = true;
        }

        _beforeHooks.RemoveAll(h =>
            string.Equals(h.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
        _afterHooks.RemoveAll(h =>
            string.Equals(h.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsPluginDisabled(string pluginId) =>
        _disabledPlugins.Contains(pluginId);

    /// <summary>True when the request matches a soft-unloaded plugin route.</summary>
    public bool IsDisabledRoute(string method, string path)
    {
        foreach (var route in _routes)
        {
            if (!route.Disabled && (route.PluginId is null || !IsPluginDisabled(route.PluginId)))
                continue;
            if (!string.Equals(route.Method, method, StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(route.Pattern, path, StringComparison.OrdinalIgnoreCase)
                || MatchesPattern(path, route.Pattern))
                return true;
        }

        return false;
    }

    public SdkRouteBuilder MapGet(string pattern, Delegate handler, string? name = null) =>
        Map("GET", pattern, handler, name);

    public SdkRouteBuilder MapPost(string pattern, Delegate handler, string? name = null) =>
        Map("POST", pattern, handler, name);

    public SdkRouteBuilder MapPut(string pattern, Delegate handler, string? name = null) =>
        Map("PUT", pattern, handler, name);

    public SdkRouteBuilder MapDelete(string pattern, Delegate handler, string? name = null) =>
        Map("DELETE", pattern, handler, name);

    public SdkRouteBuilder MapPatch(string pattern, Delegate handler, string? name = null) =>
        Map("PATCH", pattern, handler, name);

    public void Before(string pattern, Func<HttpContext, HookResult> hook, int priority = 0)
    {
        EnsureRouteCapability(pattern);
        _beforeHooks.Add(new RouteHook(
            pattern, priority, _currentPluginId,
            (ctx, _, _) => Task.FromResult(hook(ctx))));
    }

    public void After(string pattern, Func<HttpContext, object?, HookResult> hook, int priority = 0)
    {
        EnsureRouteCapability(pattern);
        _afterHooks.Add(new RouteHook(
            pattern, priority, _currentPluginId,
            (ctx, result, _) => Task.FromResult(hook(ctx, result))));
    }

    public void Alter(string pattern, Action<RouteDescriptor> alter) =>
        _alterations.Add((pattern, alter));

    internal void ApplyAlterations()
    {
        foreach (var route in _routes)
        {
            foreach (var (pattern, alter) in _alterations)
            {
                if (MatchesPattern(route.Pattern, pattern))
                    alter(route);
            }
        }
    }

    internal async Task<HookResult> RunBeforeHooksAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        foreach (var hook in _beforeHooks.OrderBy(h => h.Priority))
        {
            if (hook.PluginId is not null && IsPluginDisabled(hook.PluginId))
                continue;
            if (!MatchesPattern(path, hook.Pattern))
                continue;

            var result = await hook.Handler(context, null, CancellationToken.None).ConfigureAwait(false);
            if (result.Action != HookAction.Continue)
                return result;
        }

        return HookResult.Continue();
    }

    /// <summary>Runs after-hooks (observe-only; Cancel/Replace ignored for control flow).</summary>
    internal async Task<HookResult> RunAfterHooksAsync(HttpContext context, object? result)
    {
        var path = context.Request.Path.Value ?? "/";
        foreach (var hook in _afterHooks.OrderBy(h => h.Priority))
        {
            if (hook.PluginId is not null && IsPluginDisabled(hook.PluginId))
                continue;
            if (!MatchesPattern(path, hook.Pattern))
                continue;

            _ = await hook.Handler(context, result, CancellationToken.None).ConfigureAwait(false);
        }

        return HookResult.Continue();
    }

    private SdkRouteBuilder Map(string method, string pattern, Delegate handler, string? name)
    {
        EnsureRouteCapability(pattern);

        var descriptor = new RouteDescriptor
        {
            Pattern = pattern,
            Method = method,
            Handler = handler,
            Name = name,
            PluginId = _currentPluginId,
        };

        _routes.Add(descriptor);
        return new SdkRouteBuilder { Descriptor = descriptor };
    }

    private void EnsureRouteCapability(string pattern)
    {
        if (_currentPluginId is null)
            return;

        var required = RouteCapabilityRequirements.RequiredCapability(pattern);
        if (_currentCapabilities.Contains(required, StringComparer.OrdinalIgnoreCase))
            return;

        // Always refuse to register privileged routes without the capability.
        // PluginManager treats PluginCapabilityException as a configure warning unless Strict.
        throw new PluginCapabilityException(_currentPluginId, required, pattern);
    }

    private static bool MatchesPattern(string path, string pattern)
    {
        if (pattern == "*" || pattern == "**")
            return true;

        if (pattern.EndsWith('*') && pattern.Length > 1)
            return path.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);

        return string.Equals(path, pattern, StringComparison.OrdinalIgnoreCase)
               || Regex.IsMatch(path, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$",
                   RegexOptions.IgnoreCase);
    }

    private sealed record RouteHook(
        string Pattern,
        int Priority,
        string? PluginId,
        Func<HttpContext, object?, CancellationToken, Task<HookResult>> Handler);
}
