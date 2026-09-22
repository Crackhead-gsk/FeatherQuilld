namespace FeatherQuilld.Plugins.Host;

public interface IDaemonInfo
{
    string Version { get; }
    string Uuid { get; }
    long UptimeSeconds { get; }
}
