namespace FeatherQuilld.Plugins.Host;

/// <summary>Curated host facades available to plugins (additive to raw DI).</summary>
public interface IPluginHost
{
    IDaemonInfo Daemon { get; }
    IWebSpaceLookup WebSpaces { get; }
    IPluginConfig Config { get; }
}
