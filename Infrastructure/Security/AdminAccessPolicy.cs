using Infrastructure.Constants;

namespace Infrastructure.Security;

public sealed class AdminAccessPolicy(IPermissionChecker permissions)
{
    public bool CanOpen(string key) => permissions.Has(PermissionCatalog.Admin) && permissions.Has(key) &&
        (!key.StartsWith("Admin.References.", StringComparison.Ordinal) || permissions.Has(PermissionCatalog.AdminReferences));

    public void EnsureCanOpen(string key)
    {
        if (!CanOpen(key)) throw new UnauthorizedAccessException("Нет права доступа к этому разделу администрации.");
    }
}
