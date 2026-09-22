using FeatherQuilld.Plugins.Events;
using FeatherQuilld.Utils.Plugins.Events;
using FeatherQuilld.Utils.WebSpaces;

namespace FeatherQuilld.Tests.Plugins;

public class DomainHookCancelTests
{
    [Fact]
    public void FileWrite_Cancel_DoesNotWrite()
    {
        var root = Path.Combine(Path.GetTempPath(), "fq-hook-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var uuid = Guid.NewGuid();
            var bus = new EventBus();
            bus.On<FileWriteBeforeEvent>(_ => HookResult.Cancel());
            var files = new WebSpaceFileService(new FakeFs(uuid, root), events: bus);

            Assert.Throws<PluginHookCancelledException>(() =>
                files.WriteText(uuid, "/blocked.txt", "nope"));

            Assert.False(File.Exists(Path.Combine(root, "blocked.txt")));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void AccessDeauthorize_Cancel_LeavesState()
    {
        var bus = new EventBus();
        bus.On<AccessDeauthorizeBeforeEvent>(_ => HookResult.Cancel());
        var access = new WebSpaceUserAccessService(bus);
        var user = Guid.NewGuid();
        var ws = Guid.NewGuid();

        Assert.Throws<PluginHookCancelledException>(() =>
            access.Deauthorize(user, [ws]));

        Assert.False(access.IsJwtRevoked(user, ws, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
    }

    [Fact]
    public void FileSymlink_Cancel_Throws()
    {
        var root = Path.Combine(Path.GetTempPath(), "fq-hook-sym-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var uuid = Guid.NewGuid();
            var bus = new EventBus();
            bus.On<FileSymlinkBeforeEvent>(_ => HookResult.Cancel());
            var files = new WebSpaceFileService(new FakeFs(uuid, root), events: bus);

            Assert.Throws<PluginHookCancelledException>(() =>
                files.CreateSymlink(uuid, "/link", "/target"));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void TrashRestore_Cancel_Throws()
    {
        var root = Path.Combine(Path.GetTempPath(), "fq-hook-trash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var uuid = Guid.NewGuid();
            var bus = new EventBus();
            bus.On<TrashRestoreBeforeEvent>(_ => HookResult.Cancel());
            var trash = new WebSpaceTrashService(new FakeFs(uuid, root), bus);

            Assert.Throws<PluginHookCancelledException>(() =>
                trash.RestoreTrash(uuid, ["missing"], overwrite: false));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void DnsZoneCreate_Cancel_ThrowsWithoutCallingApi()
    {
        var bus = new EventBus();
        bus.On<DnsZoneCreateBeforeEvent>(_ => HookResult.Cancel());
        var config = new FeatherQuilld.Utils.Config.Config();
        config.System.Dns.PowerDnsApiKey = "test-key";
        config.System.Dns.PowerDnsApiUrl = "http://127.0.0.1:9";
        var mgr = new FeatherQuilld.Utils.Dns.PowerDnsManager(config, bus);

        Assert.Throws<PluginHookCancelledException>(() => mgr.CreateZone("example.test"));
    }

    [Fact]
    public void PanelSync_Cancel_Propagates()
    {
        var bus = new EventBus();
        bus.On<PanelSyncBeforeEvent>(_ => HookResult.Cancel());
        Assert.Throws<PluginHookCancelledException>(() =>
            bus.WithHooks(
                new PanelSyncBeforeEvent(),
                err => new PanelSyncAfterEvent { Error = err },
                () => { }));
    }

    private sealed class FakeFs(Guid uuid, string root) : IWebSpaceFsAccess
    {
        public WebSpace? Get(Guid id) =>
            id == uuid
                ? new WebSpace { Uuid = id, Name = "test", Status = WebSpaceStatus.Installed }
                : null;

        public string EffectiveFsPath(Guid id) =>
            id == uuid ? root : throw new InvalidOperationException("missing");
    }
}
