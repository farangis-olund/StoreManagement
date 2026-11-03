using Infrastructure.Repositories;

namespace PresentationWpf.Services;

public sealed class SessionPermissionRefresher(
    UserSessionService session, RolePermissionRepository repository, PermissionService permissions)
{
    private long _refreshVersion;

    public async Task RefreshAsync()
    {
        var version = ++_refreshVersion;
        var userId = session.UserId;
        if (!session.IsLoggedIn)
        {
            permissions.SetPermissions(Array.Empty<string>());
            return;
        }

        try
        {
            var keys = await repository.GetPermissionKeysForUserAsync(userId);
            if (version == _refreshVersion && session.IsLoggedIn && session.UserId == userId)
                permissions.SetPermissions(keys);
        }
        catch
        {
            // Do not keep stale grants if the current user's refresh fails.
            if (version == _refreshVersion && session.UserId == userId)
                permissions.SetPermissions(Array.Empty<string>());
            throw;
        }
    }
}
