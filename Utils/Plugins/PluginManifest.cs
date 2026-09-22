namespace FeatherQuilld.Utils.Plugins;

/// <summary>Optional <c>plugin.yml</c> manifest inside a plugin folder.</summary>
public sealed class PluginManifest
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? MinHostVersion { get; set; }

    /// <summary>Entry assembly file name (defaults to the only plugin DLL in the folder).</summary>
    public string? Main { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>Declared capabilities (see <see cref="FeatherQuilld.Plugins.Metadata.PluginCapabilities"/>).</summary>
    public List<string> Capabilities { get; set; } = [];

    /// <summary>Default settings; overridden by host <c>plugins.settings.&lt;id&gt;</c>.</summary>
    public Dictionary<string, object?> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
