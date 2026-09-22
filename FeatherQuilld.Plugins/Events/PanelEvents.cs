namespace FeatherQuilld.Plugins.Events;

public sealed class PanelSyncBeforeEvent
{
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class PanelSyncAfterEvent
{
    public DateTimeOffset StartedAt { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}
