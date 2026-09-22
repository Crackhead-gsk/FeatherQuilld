namespace FeatherQuilld.Plugins.Events;

public sealed class DnsZoneCreateBeforeEvent
{
    public required string ZoneName { get; init; }
}

public sealed class DnsZoneCreateAfterEvent
{
    public required string ZoneName { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class DnsRecordCreateBeforeEvent
{
    public required string ZoneId { get; init; }
}

public sealed class DnsRecordCreateAfterEvent
{
    public required string ZoneId { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class DnsRecordUpdateBeforeEvent
{
    public required string ZoneId { get; init; }
    public required string RecordId { get; init; }
}

public sealed class DnsRecordUpdateAfterEvent
{
    public required string ZoneId { get; init; }
    public required string RecordId { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class DnsRecordDeleteBeforeEvent
{
    public required string ZoneId { get; init; }
    public required string RecordId { get; init; }
}

public sealed class DnsRecordDeleteAfterEvent
{
    public required string ZoneId { get; init; }
    public required string RecordId { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class DnsUpsertABeforeEvent
{
    public required string ZoneId { get; init; }
    public required string Name { get; init; }
    public required string Ip { get; init; }
}

public sealed class DnsUpsertAAfterEvent
{
    public required string ZoneId { get; init; }
    public required string Name { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class DnsTxtCreateBeforeEvent
{
    public required string ZoneId { get; init; }
    public required string Name { get; init; }
}

public sealed class DnsTxtCreateAfterEvent
{
    public required string ZoneId { get; init; }
    public required string Name { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}

public sealed class DnsTxtDeleteBeforeEvent
{
    public required string ZoneId { get; init; }
    public required string Name { get; init; }
}

public sealed class DnsTxtDeleteAfterEvent
{
    public required string ZoneId { get; init; }
    public required string Name { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error is null;
}
