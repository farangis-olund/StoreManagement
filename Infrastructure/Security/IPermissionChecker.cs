namespace Infrastructure.Security;

public interface IPermissionChecker
{
    bool Has(string key);
}
