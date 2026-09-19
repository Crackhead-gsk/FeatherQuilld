using FeatherQuilld.Utils.Logger;
using Microsoft.Extensions.Hosting;
using AppLogger = FeatherQuilld.Utils.Logger.Logger;

namespace FeatherQuilld.Utils.WebSpaces;

/// <summary>
/// Periodically verifies that every WebSpace still has a live quota mount and repairs the
/// ones that do not. A dead FUSE mount (daemon restarted or killed) otherwise leaves the
/// site answering 404/502 until somebody notices and remounts by hand.
/// </summary>
public sealed class WebSpaceMountHealthHostedService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly WebSpaceStore _store;
    private readonly AppLogger? _logger;

    public WebSpaceMountHealthHostedService(WebSpaceStore store, AppLogger? logger = null)
    {
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (true)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                    return;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                var repaired = _store.RepairUnhealthyMounts();
                if (repaired > 0)
                {
                    _logger?.Warning(LoggerTypes.Disk,
                        $"Mount health check repaired {repaired} WebSpace mount(s)");
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning(LoggerTypes.Disk, $"Mount health check failed: {ex.Message}");
            }
        }
    }
}
