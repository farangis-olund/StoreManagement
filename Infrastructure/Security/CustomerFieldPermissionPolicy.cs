using Infrastructure.Constants;
using Infrastructure.Entities;

namespace Infrastructure.Security;

public sealed class CustomerFieldPermissionPolicy(IPermissionChecker permissions)
{
    public bool CanView(CustomerField field)
    {
        var keys = CustomerPermissionCatalog.For(field);
        return permissions.Has(PermissionCatalog.Customers) &&
            (permissions.Has(keys.View) || (keys.LegacyViewKey != null && permissions.Has(keys.LegacyViewKey)));
    }

    public bool CanAdd(CustomerField field) => CanView(field) && permissions.Has(CustomerPermissionCatalog.For(field).Add);
    public bool CanEdit(CustomerField field) => CanView(field) && permissions.Has(CustomerPermissionCatalog.For(field).Edit);
    public bool CanDelete(CustomerField field) => CanView(field) &&
        !CustomerPermissionCatalog.For(field).IsRequired && permissions.Has(CustomerPermissionCatalog.For(field).Delete);

    public bool CanEnterValue(CustomerField field, object? originalValue) =>
        IsEmpty(field, originalValue) ? CanAdd(field) : CanEdit(field);

    public bool CanCreateCustomer => CustomerPermissionCatalog.Fields.Where(p => p.IsRequired).All(p => CanAdd(p.Field));

    public bool CanDeleteCustomer => permissions.Has(PermissionCatalog.Customers) && permissions.Has(PermissionCatalog.CustomersDelete);

    public bool CanViewBalance => permissions.Has(PermissionCatalog.Customers) && permissions.Has(PermissionCatalog.CustomersBalanceView);

    public void EnsureCanViewBalance()
    {
        if (!CanViewBalance) throw new UnauthorizedAccessException("Нет права на просмотр остатка клиента.");
    }

    public void EnsureCanDeleteCustomer()
    {
        if (!CanDeleteCustomer) throw new UnauthorizedAccessException("Нет права на удаление клиента.");
    }

    public void EnsureCanSave(CustomerEntity? original, CustomerEntity edited)
    {
        if (!permissions.Has(PermissionCatalog.Customers))
            throw new UnauthorizedAccessException("Нет права доступа к клиентам.");

        foreach (var field in CustomerPermissionCatalog.Fields)
            EnsureCanChange(field.Field, original, edited);
    }

    public void EnsureCanChange(CustomerField field, CustomerEntity? original, CustomerEntity edited)
    {
        var keys = CustomerPermissionCatalog.For(field);
        var oldValues = Values(field, original);
        var newValues = Values(field, edited);
        for (var i = 0; i < oldValues.Length; i++)
        {
            var oldValue = oldValues[i];
            var newValue = newValues[i];
            if (Equals(oldValue, newValue) || (IsEmpty(field, oldValue) && IsEmpty(field, newValue)))
                continue;

            if (keys.IsRequired && IsEmpty(field, newValue))
                throw new InvalidOperationException($"Поле '{keys.Description}' обязательно и не может быть очищено.");

            var allowed = IsEmpty(field, newValue) ? CanDelete(field)
                : IsEmpty(field, oldValue) ? CanAdd(field) : CanEdit(field);
            if (!allowed)
            {
                var action = IsEmpty(field, newValue) ? "Очистить значение"
                    : IsEmpty(field, oldValue) ? "Добавить значение" : "Изменить значение";
                throw new UnauthorizedAccessException($"Нет права: {keys.Description}.{action}.");
            }
        }
    }

    public static bool IsEmpty(CustomerField field, object? value) => value switch
    {
        null => true,
        string text => string.IsNullOrWhiteSpace(text),
        bool flag => !flag,
        decimal amount when field is CustomerField.BuyLimit or CustomerField.UnpaidPaymentLimit => amount <= 0,
        decimal amount => amount == 0,
        double number => number == 0,
        _ => false
    };

    public static object?[] Values(CustomerField field, CustomerEntity? customer) => field switch
    {
        CustomerField.Debt => new object?[] { customer?.Debt },
        CustomerField.Restriction => new object?[] { customer?.Restriction },
        CustomerField.BuyLimit => new object?[] { customer?.BuyLimit },
        CustomerField.UnpaidPaymentLimit => new object?[] { customer?.UnpaidPaymentLimit },
        CustomerField.Coefficients => new object?[]
        {
            customer?.DailyPurchaseCoefficient, customer?.DailyRepaymentCoefficient, customer?.DailyPlannedPurchaseCoefficient
        },
        CustomerField.ContractDate => new object?[] { customer?.ContractDate },
        CustomerField.PriceLevel => new object?[] { customer?.PriceLevelId },
        CustomerField.SalesManager => new object?[] { customer?.SalesManagerId },
        CustomerField.Territory => new object?[] { customer?.Territory },
        CustomerField.ExcludeDailyRepayment => new object?[] { customer?.ExcludeDailyRepayment },
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };
}
