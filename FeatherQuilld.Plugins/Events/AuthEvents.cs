namespace FeatherQuilld.Plugins.Events;

public sealed class AuthValidateBeforeEvent
{
    public required string Scheme { get; init; }
    public required string Subject { get; init; }
}

public sealed class AuthValidateAfterEvent
{
    public required string Scheme { get; init; }
    public required string Subject { get; init; }
    public bool Valid { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}
