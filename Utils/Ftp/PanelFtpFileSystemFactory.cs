using FubarDev.FtpServer;
using FubarDev.FtpServer.BackgroundTransfer;
using FubarDev.FtpServer.FileSystem;
using FubarDev.FtpServer.FileSystem.DotNet;
using FeatherQuilld.Plugins.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FeatherQuilld.Utils.Ftp;

internal sealed class PanelFtpFileSystemFactory : IFileSystemClassFactory
{
    private readonly DotNetFileSystemProvider _inner;
    private readonly IEventBus _events;

    public PanelFtpFileSystemFactory(
        IOptions<DotNetFileSystemOptions> options,
        IAccountDirectoryQuery accountDirectoryQuery,
        ILogger<DotNetFileSystemProvider>? logger = null,
        IEventBus? events = null)
    {
        _inner = new DotNetFileSystemProvider(options, accountDirectoryQuery, logger);
        _events = events.OrNoOp();
    }

    public async Task<IUnixFileSystem> Create(IAccountInformation accountInformation)
    {
        var fs = await _inner.Create(accountInformation).ConfigureAwait(false);
        var username = accountInformation.FtpUser.Identity?.Name ?? string.Empty;
        if (!FtpSessionStore.TryGet(username, out var session))
            return fs;
        if (session.ReadOnly)
            return new ReadOnlyUnixFileSystem(fs);
        return new EventedUnixFileSystem(fs, session.WebSpaceUuid, _events);
    }
}

internal sealed class EventedUnixFileSystem : IUnixFileSystem
{
    private readonly IUnixFileSystem _inner;
    private readonly Guid _webSpaceUuid;
    private readonly IEventBus _events;

    public EventedUnixFileSystem(IUnixFileSystem inner, Guid webSpaceUuid, IEventBus events)
    {
        _inner = inner;
        _webSpaceUuid = webSpaceUuid;
        _events = events;
    }

    public bool SupportsAppend => _inner.SupportsAppend;

    public bool SupportsNonEmptyDirectoryDelete => _inner.SupportsNonEmptyDirectoryDelete;

    public StringComparer FileSystemEntryComparer => _inner.FileSystemEntryComparer;

    public IUnixDirectoryEntry Root => _inner.Root;

    public Task<IReadOnlyList<IUnixFileSystemEntry>> GetEntriesAsync(
        IUnixDirectoryEntry directoryEntry,
        CancellationToken cancellationToken) =>
        _inner.GetEntriesAsync(directoryEntry, cancellationToken);

    public Task<IUnixFileSystemEntry?> GetEntryByNameAsync(
        IUnixDirectoryEntry directoryEntry,
        string name,
        CancellationToken cancellationToken) =>
        _inner.GetEntryByNameAsync(directoryEntry, name, cancellationToken);

    public Task<IUnixFileSystemEntry> MoveAsync(
        IUnixDirectoryEntry parent,
        IUnixFileSystemEntry source,
        IUnixDirectoryEntry target,
        string fileName,
        CancellationToken cancellationToken) =>
        _events.WithHooksAsync(
            new FtpRenameBeforeEvent
            {
                WebSpaceUuid = _webSpaceUuid,
                From = source.Name,
                To = fileName,
            },
            (result, err) => new FtpRenameAfterEvent
            {
                WebSpaceUuid = _webSpaceUuid,
                From = source.Name,
                To = fileName,
                Error = err,
            },
            ct => _inner.MoveAsync(parent, source, target, fileName, ct),
            cancellationToken);

    public Task UnlinkAsync(IUnixFileSystemEntry entry, CancellationToken cancellationToken) =>
        _events.WithHooksAsync(
            new FtpDeleteBeforeEvent { WebSpaceUuid = _webSpaceUuid, Path = entry.Name },
            err => new FtpDeleteAfterEvent { WebSpaceUuid = _webSpaceUuid, Path = entry.Name, Error = err },
            ct => _inner.UnlinkAsync(entry, ct),
            cancellationToken);

    public Task<IUnixDirectoryEntry> CreateDirectoryAsync(
        IUnixDirectoryEntry parent,
        string name,
        CancellationToken cancellationToken) =>
        _events.WithHooksAsync(
            new FtpMkdirBeforeEvent { WebSpaceUuid = _webSpaceUuid, Path = name },
            (result, err) => new FtpMkdirAfterEvent { WebSpaceUuid = _webSpaceUuid, Path = name, Error = err },
            ct => _inner.CreateDirectoryAsync(parent, name, ct),
            cancellationToken);

    public Task<Stream> OpenReadAsync(
        IUnixFileEntry fileEntry,
        long startPosition,
        CancellationToken cancellationToken) =>
        _inner.OpenReadAsync(fileEntry, startPosition, cancellationToken);

    public Task<IBackgroundTransfer?> AppendAsync(
        IUnixFileEntry fileEntry,
        long? startPosition,
        Stream content,
        CancellationToken cancellationToken) =>
        _events.WithHooksAsync(
            new FtpWriteBeforeEvent { WebSpaceUuid = _webSpaceUuid, Path = fileEntry.Name },
            (result, err) => new FtpWriteAfterEvent { WebSpaceUuid = _webSpaceUuid, Path = fileEntry.Name, Error = err },
            ct => _inner.AppendAsync(fileEntry, startPosition, content, ct),
            cancellationToken);

    public Task<IBackgroundTransfer?> CreateAsync(
        IUnixDirectoryEntry parent,
        string name,
        Stream content,
        CancellationToken cancellationToken) =>
        _events.WithHooksAsync(
            new FtpWriteBeforeEvent { WebSpaceUuid = _webSpaceUuid, Path = name },
            (result, err) => new FtpWriteAfterEvent { WebSpaceUuid = _webSpaceUuid, Path = name, Error = err },
            ct => _inner.CreateAsync(parent, name, content, ct),
            cancellationToken);

    public Task<IBackgroundTransfer?> ReplaceAsync(
        IUnixFileEntry fileEntry,
        Stream content,
        CancellationToken cancellationToken) =>
        _events.WithHooksAsync(
            new FtpWriteBeforeEvent { WebSpaceUuid = _webSpaceUuid, Path = fileEntry.Name },
            (result, err) => new FtpWriteAfterEvent { WebSpaceUuid = _webSpaceUuid, Path = fileEntry.Name, Error = err },
            ct => _inner.ReplaceAsync(fileEntry, content, ct),
            cancellationToken);

    public Task<IUnixFileSystemEntry> SetMacTimeAsync(
        IUnixFileSystemEntry entry,
        DateTimeOffset? modify,
        DateTimeOffset? access,
        DateTimeOffset? create,
        CancellationToken cancellationToken) =>
        _inner.SetMacTimeAsync(entry, modify, access, create, cancellationToken);
}

internal sealed class ReadOnlyUnixFileSystem : IUnixFileSystem
{
    private readonly IUnixFileSystem _inner;

    public ReadOnlyUnixFileSystem(IUnixFileSystem inner) => _inner = inner;

    public bool SupportsAppend => false;

    public bool SupportsNonEmptyDirectoryDelete => _inner.SupportsNonEmptyDirectoryDelete;

    public StringComparer FileSystemEntryComparer => _inner.FileSystemEntryComparer;

    public IUnixDirectoryEntry Root => _inner.Root;

    public Task<IReadOnlyList<IUnixFileSystemEntry>> GetEntriesAsync(
        IUnixDirectoryEntry directoryEntry,
        CancellationToken cancellationToken) =>
        _inner.GetEntriesAsync(directoryEntry, cancellationToken);

    public Task<IUnixFileSystemEntry?> GetEntryByNameAsync(
        IUnixDirectoryEntry directoryEntry,
        string name,
        CancellationToken cancellationToken) =>
        _inner.GetEntryByNameAsync(directoryEntry, name, cancellationToken);

    public Task<IUnixFileSystemEntry> MoveAsync(
        IUnixDirectoryEntry parent,
        IUnixFileSystemEntry source,
        IUnixDirectoryEntry target,
        string fileName,
        CancellationToken cancellationToken) =>
        throw new UnauthorizedAccessException("FTP account is read-only.");

    public Task UnlinkAsync(IUnixFileSystemEntry entry, CancellationToken cancellationToken) =>
        throw new UnauthorizedAccessException("FTP account is read-only.");

    public Task<IUnixDirectoryEntry> CreateDirectoryAsync(
        IUnixDirectoryEntry parent,
        string name,
        CancellationToken cancellationToken) =>
        throw new UnauthorizedAccessException("FTP account is read-only.");

    public Task<Stream> OpenReadAsync(
        IUnixFileEntry fileEntry,
        long startPosition,
        CancellationToken cancellationToken) =>
        _inner.OpenReadAsync(fileEntry, startPosition, cancellationToken);

    public Task<IBackgroundTransfer?> AppendAsync(
        IUnixFileEntry fileEntry,
        long? startPosition,
        Stream content,
        CancellationToken cancellationToken) =>
        throw new UnauthorizedAccessException("FTP account is read-only.");

    public Task<IBackgroundTransfer?> CreateAsync(
        IUnixDirectoryEntry parent,
        string name,
        Stream content,
        CancellationToken cancellationToken) =>
        throw new UnauthorizedAccessException("FTP account is read-only.");

    public Task<IBackgroundTransfer?> ReplaceAsync(
        IUnixFileEntry fileEntry,
        Stream content,
        CancellationToken cancellationToken) =>
        throw new UnauthorizedAccessException("FTP account is read-only.");

    public Task<IUnixFileSystemEntry> SetMacTimeAsync(
        IUnixFileSystemEntry entry,
        DateTimeOffset? modify,
        DateTimeOffset? access,
        DateTimeOffset? create,
        CancellationToken cancellationToken) =>
        _inner.SetMacTimeAsync(entry, modify, access, create, cancellationToken);
}
