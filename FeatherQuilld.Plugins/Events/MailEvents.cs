namespace FeatherQuilld.Plugins.Events;

public sealed class MailDomainAddBeforeEvent
{
    public required string Domain { get; init; }
}

public sealed class MailDomainAddAfterEvent
{
    public required string Domain { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class MailDomainRemoveBeforeEvent
{
    public required string Domain { get; init; }
}

public sealed class MailDomainRemoveAfterEvent
{
    public required string Domain { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class MailProvisionBeforeEvent
{
    public required string Email { get; init; }
}

public sealed class MailProvisionAfterEvent
{
    public required string Email { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class MailSpamFilterBeforeEvent
{
    public required string Email { get; init; }
    public required bool Enabled { get; init; }
}

public sealed class MailSpamFilterAfterEvent
{
    public required string Email { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}
