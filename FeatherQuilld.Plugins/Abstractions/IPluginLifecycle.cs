namespace FeatherQuilld.Plugins.Abstractions;

/// <summary>
/// Optional lifecycle hooks. Plugins that only implement <see cref="IPlugin"/>
/// still load; Start/Stop are invoked when this interface is present.
/// </summary>
public interface IPluginLifecycle
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
