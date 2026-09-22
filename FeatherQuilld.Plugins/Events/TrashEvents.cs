namespace FeatherQuilld.Plugins.Events;

public sealed class TrashRestoreBeforeEvent
{
    public required Guid WebSpaceUuid { get; init; }
    public required string Path { get; init; }
}

public sealed class TrashRestoreAfterEvent
{
    public required Guid WebSpaceUuid { get; init; }
    public required string Path { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class TrashPurgeBeforeEvent
{
    public required Guid WebSpaceUuid { get; init; }
    public required string Path { get; init; }
}

public sealed class TrashPurgeAfterEvent
{
    public required Guid WebSpaceUuid { get; init; }
    public required string Path { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}
