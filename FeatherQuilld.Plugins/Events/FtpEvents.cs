namespace FeatherQuilld.Plugins.Events;

public sealed class FtpAuthBeforeEvent
{
    public required string Username { get; init; }
}

public sealed class FtpAuthAfterEvent
{
    public required string Username { get; init; }
    public bool Authenticated { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class FtpWriteBeforeEvent
{
    public required Guid WebSpaceUuid { get; init; }
    public required string Path { get; init; }
}

public sealed class FtpWriteAfterEvent
{
    public required Guid WebSpaceUuid { get; init; }
    public required string Path { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class FtpDeleteBeforeEvent
{
    public required Guid WebSpaceUuid { get; init; }
    public required string Path { get; init; }
}

public sealed class FtpDeleteAfterEvent
{
    public required Guid WebSpaceUuid { get; init; }
    public required string Path { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class FtpRenameBeforeEvent
{
    public required Guid WebSpaceUuid { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
}

public sealed class FtpRenameAfterEvent
{
    public required Guid WebSpaceUuid { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class FtpMkdirBeforeEvent
{
    public required Guid WebSpaceUuid { get; init; }
    public required string Path { get; init; }
}

public sealed class FtpMkdirAfterEvent
{
    public required Guid WebSpaceUuid { get; init; }
    public required string Path { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}
