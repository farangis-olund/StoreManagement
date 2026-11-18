using Infrastructure.Constants;

namespace Infrastructure.Security;

public sealed class OrderOperationsAccessPolicy(IPermissionChecker permissions)
{
    public bool Can(string key)
    {
        if (!permissions.Has(PermissionCatalog.Orders)) return false;
        return key switch
        {
            PermissionCatalog.OrdersAssignPickers => permissions.Has(key),
            PermissionCatalog.OrdersDelivery => permissions.Has(key),
            PermissionCatalog.OrdersDeliveryView => permissions.Has(PermissionCatalog.OrdersDelivery) && permissions.Has(key),
            PermissionCatalog.OrdersDeliveryUpdate or PermissionCatalog.OrdersDeliveryPrint or PermissionCatalog.OrdersDeliveryReport =>
                permissions.Has(PermissionCatalog.OrdersDelivery) && permissions.Has(PermissionCatalog.OrdersDeliveryView) && permissions.Has(key),
            _ => false
        };
    }

    public void Ensure(string key)
    {
        if (!Can(key)) throw new UnauthorizedAccessException("Нет права доступа к этому действию с заказами.");
    }
}
