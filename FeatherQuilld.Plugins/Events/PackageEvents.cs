namespace FeatherQuilld.Plugins.Events;

public sealed class PackageInstallBeforeEvent
{
    public required string PackageId { get; init; }
}

public sealed class PackageInstallAfterEvent
{
    public required string PackageId { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class PackageRemoveBeforeEvent
{
    public required string PackageId { get; init; }
}

public sealed class PackageRemoveAfterEvent
{
    public required string PackageId { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}
