namespace FeatherQuilld.Plugins.Host;

/// <summary>Resolved settings for the current plugin (manifest + host config).</summary>
public interface IPluginConfig
{
    string PluginId { get; }
    IReadOnlyDictionary<string, object?> Settings { get; }
    T? Get<T>(string key);
}
