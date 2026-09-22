using System.Security.Claims;
using FubarDev.FtpServer.AccountManagement;
using FeatherQuilld.Plugins.Events;
using FeatherQuilld.Utils.Config.Ftp;
using FeatherQuilld.Utils.Logger;
using FeatherQuilld.Utils.Remote;
using FeatherQuilld.Utils.WebSpaces;
using AppLogger = FeatherQuilld.Utils.Logger.Logger;

namespace FeatherQuilld.Utils.Ftp;

internal sealed class PanelFtpMembershipProvider : IMembershipProvider
{
    private readonly FtpConfig _config;
    private readonly WebSpaceStore _spaces;
    private readonly IPanelClient _panel;
    private readonly AppLogger? _logger;
    private readonly IEventBus _events;

    public PanelFtpMembershipProvider(
        FtpConfig config,
        WebSpaceStore spaces,
        IPanelClient panel,
        AppLogger? logger = null,
        IEventBus? events = null)
    {
        _config = config;
        _spaces = spaces;
        _panel = panel;
        _logger = logger;
        _events = events.OrNoOp();
    }

    public Task<MemberValidationResult> ValidateUserAsync(string username, string password) =>
        Task.FromResult(_events.WithHooks(
            new FtpAuthBeforeEvent { Username = username },
            (result, err) => new FtpAuthAfterEvent
            {
                Username = username,
                Authenticated = result?.FtpUser is not null,
                Error = err,
            },
            () => ValidateUserCore(username, password)));

    private MemberValidationResult ValidateUserCore(string username, string password)
    {
        if (_config.DisablePasswordAuth)
            return new MemberValidationResult(MemberValidationStatus.InvalidLogin);

        var auth = WebSpaceAccessRoot.Resolve(_panel, _spaces, "password", username, password, logger: _logger);
        if (auth is null || string.IsNullOrWhiteSpace(auth.RootPath))
        {
            _logger?.Debug(LoggerTypes.Application, $"FTP auth failed for user={username}");
            return new MemberValidationResult(MemberValidationStatus.InvalidLogin);
        }

        Guid.TryParse(auth.Server, out var webSpaceUuid);
        var claims = new List<Claim>
        {
            new(ClaimsIdentity.DefaultNameClaimType, username),
            new(FtpAuthClaims.RootPath, auth.RootPath),
            new(FtpAuthClaims.ReadOnly, auth.IsReadOnly ? "1" : "0"),
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "panel"));
        FtpSessionStore.Set(username, new FtpSessionContext(auth.RootPath, auth.IsReadOnly, webSpaceUuid));
        _logger?.Info(LoggerTypes.Application, $"FTP auth ok user={username} webspace={auth.Server}");
        return new MemberValidationResult(MemberValidationStatus.AuthenticatedUser, principal);
    }

    public Task LogOutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var username = principal.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(username))
            FtpSessionStore.Remove(username);
        return Task.CompletedTask;
    }
}
