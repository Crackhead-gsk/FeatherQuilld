namespace FeatherQuilld.Plugins.Metadata;

/// <summary>Identity and version information for a plugin.</summary>
public sealed class PluginMetadata
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public string? Author { get; init; }

    /// <summary>Minimum FeatherQuilld host version required (semver).</summary>
    public string? MinHostVersion { get; init; }

    /// <summary>Declared capabilities (see <see cref="PluginCapabilities"/>).</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>Default settings from code; overridden by <c>plugin.yml</c> then host config.</summary>
    public IReadOnlyDictionary<string, object?> Settings { get; init; } =
        new Dictionary<string, object?>();
}
