using FeatherQuilld.Plugins.Host;
using FeatherQuilld.Plugins.Metadata;

namespace FeatherQuilld.Utils.Plugins.Host;

internal sealed class PluginHostFacade : IPluginHost
{
    public PluginHostFacade(
        IDaemonInfo daemon,
        IWebSpaceLookup webSpaces,
        IPluginConfig config)
    {
        Daemon = daemon;
        WebSpaces = webSpaces;
        Config = config;
    }

    public IDaemonInfo Daemon { get; }
    public IWebSpaceLookup WebSpaces { get; }
    public IPluginConfig Config { get; }
}

internal sealed class PluginConfigFacade : IPluginConfig
{
    private readonly HashSet<string> _capabilities;

    public PluginConfigFacade(
        string pluginId,
        IReadOnlyDictionary<string, object?> settings,
        IReadOnlyList<string> capabilities)
    {
        PluginId = pluginId;
        Settings = settings;
        _capabilities = new HashSet<string>(capabilities, StringComparer.OrdinalIgnoreCase);
    }

    public string PluginId { get; }
    public IReadOnlyDictionary<string, object?> Settings { get; }

    public T? Get<T>(string key)
    {
        if (!Settings.TryGetValue(key, out var value) || value is null)
            return default;
        if (value is T typed)
            return typed;
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    internal void Require(string capability)
    {
        if (!_capabilities.Contains(capability))
            throw new UnauthorizedAccessException(
                $"Plugin '{PluginId}' lacks capability '{capability}'.");
    }
}

internal sealed class DaemonInfoFacade : IDaemonInfo
{
    private readonly PluginConfigFacade _config;
    private readonly Func<string> _version;
    private readonly Func<string> _uuid;
    private readonly Func<long> _uptime;

    public DaemonInfoFacade(
        PluginConfigFacade config,
        Func<string> version,
        Func<string> uuid,
        Func<long> uptime)
    {
        _config = config;
        _version = version;
        _uuid = uuid;
        _uptime = uptime;
    }

    public string Version
    {
        get
        {
            _config.Require(PluginCapabilities.HostInfo);
            return _version();
        }
    }

    public string Uuid
    {
        get
        {
            _config.Require(PluginCapabilities.HostInfo);
            return _uuid();
        }
    }

    public long UptimeSeconds
    {
        get
        {
            _config.Require(PluginCapabilities.HostInfo);
            return _uptime();
        }
    }
}

internal sealed class WebSpaceLookupFacade : IWebSpaceLookup
{
    private readonly PluginConfigFacade _config;
    private readonly Func<Guid, WebSpaceSummary?> _get;
    private readonly Func<IReadOnlyList<WebSpaceSummary>> _list;

    public WebSpaceLookupFacade(
        PluginConfigFacade config,
        Func<Guid, WebSpaceSummary?> get,
        Func<IReadOnlyList<WebSpaceSummary>> list)
    {
        _config = config;
        _get = get;
        _list = list;
    }

    public WebSpaceSummary? Get(Guid uuid)
    {
        _config.Require(PluginCapabilities.WebSpacesRead);
        return _get(uuid);
    }

    public IReadOnlyList<WebSpaceSummary> List()
    {
        _config.Require(PluginCapabilities.WebSpacesRead);
        return _list();
    }
}
