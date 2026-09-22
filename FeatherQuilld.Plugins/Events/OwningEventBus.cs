namespace FeatherQuilld.Plugins.Events;

/// <summary>
/// Wraps an <see cref="IEventBus"/> and tracks subscriptions so a plugin unload
/// can dispose them in bulk.
/// </summary>
public sealed class OwningEventBus : IEventBus, IDisposable
{
    private readonly IEventBus _inner;
    private readonly List<IDisposable> _subscriptions = [];
    private readonly object _gate = new();
    private bool _disposed;

    public OwningEventBus(IEventBus inner) =>
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public IDisposable On<TEvent>(Func<TEvent, HookResult> handler, int priority = 0)
        where TEvent : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var sub = _inner.On(handler, priority);
        Track(sub);
        return sub;
    }

    public IDisposable On<TEvent>(Func<TEvent, CancellationToken, Task<HookResult>> handler, int priority = 0)
        where TEvent : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var sub = _inner.On(handler, priority);
        Track(sub);
        return sub;
    }

    public HookResult Emit<TEvent>(TEvent evt) where TEvent : class =>
        _inner.Emit(evt);

    public Task<HookResult> EmitAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default)
        where TEvent : class =>
        _inner.EmitAsync(evt, cancellationToken);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        IDisposable[] snapshot;
        lock (_gate)
        {
            snapshot = _subscriptions.ToArray();
            _subscriptions.Clear();
        }

        foreach (var sub in snapshot)
        {
            try { sub.Dispose(); } catch { /* ignore */ }
        }
    }

    private void Track(IDisposable sub)
    {
        lock (_gate)
            _subscriptions.Add(sub);
    }
}
