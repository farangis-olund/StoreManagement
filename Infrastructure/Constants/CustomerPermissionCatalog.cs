namespace Infrastructure.Constants;

public enum CustomerField
{
    Debt,
    Restriction,
    BuyLimit,
    UnpaidPaymentLimit,
    Coefficients,
    ContractDate,
    PriceLevel,
    SalesManager,
    Territory,
    ExcludeDailyRepayment
}

public sealed record CustomerFieldPermissions(
    CustomerField Field, string Prefix, string Description, bool IsRequired = false, string? LegacyViewKey = null)
{
    public string View => Prefix + ".View";
    public string Add => Prefix + ".Add";
    public string Edit => Prefix + ".Edit";
    public string Delete => Prefix + ".Delete";

    public IEnumerable<PermissionDefinition> Definitions => new[]
    {
        new PermissionDefinition(View, Description + ".Просмотр"),
        new PermissionDefinition(Add, Description + ".Добавить значение"),
        new PermissionDefinition(Edit, Description + ".Изменить значение"),
        new PermissionDefinition(Delete, Description + ".Очистить значение")
    };
}

public static class CustomerPermissionCatalog
{
    public static CustomerFieldPermissions Debt { get; } = new(CustomerField.Debt,
        "Customers.Debt", "Клиенты.Задолженность", LegacyViewKey: PermissionCatalog.CustomersDebt);
    public static CustomerFieldPermissions Restriction { get; } = new(CustomerField.Restriction,
        "Customers.Restriction", "Клиенты.Ограничение", LegacyViewKey: PermissionCatalog.CustomersRestriction);
    public static CustomerFieldPermissions BuyLimit { get; } = new(CustomerField.BuyLimit,
        "Customers.BuyLimit", "Клиенты.Лимит суммы заказа");
    public static CustomerFieldPermissions UnpaidPaymentLimit { get; } = new(CustomerField.UnpaidPaymentLimit,
        "Customers.UnpaidPaymentLimit", "Клиенты.Лимит суммы неоплаченных платежей");
    public static CustomerFieldPermissions Coefficients { get; } = new(CustomerField.Coefficients,
        "Customers.Coefficients", "Клиенты.Коэффициенты (все три)");
    public static CustomerFieldPermissions ContractDate { get; } = new(CustomerField.ContractDate,
        "Customers.ContractDate", "Клиенты.Дата контракта");
    public static CustomerFieldPermissions PriceLevel { get; } = new(CustomerField.PriceLevel,
        "Customers.PriceLevel", "Клиенты.Уровень цен", IsRequired: true);
    public static CustomerFieldPermissions SalesManager { get; } = new(CustomerField.SalesManager,
        "Customers.SalesManager", "Клиенты.Менеджер", IsRequired: true);
    public static CustomerFieldPermissions Territory { get; } = new(CustomerField.Territory,
        "Customers.Territory", "Клиенты.Территория", IsRequired: true);
    public static CustomerFieldPermissions ExcludeDailyRepayment { get; } = new(CustomerField.ExcludeDailyRepayment,
        "Customers.ExcludeDailyRepayment", "Клиенты.Исключить ежедневное погашение");

    public static IReadOnlyList<CustomerFieldPermissions> Fields { get; } = Array.AsReadOnly(new[]
    {
        Debt, Restriction, BuyLimit, UnpaidPaymentLimit, Coefficients, ContractDate,
        PriceLevel, SalesManager, Territory, ExcludeDailyRepayment
    });

    public static IReadOnlyList<PermissionDefinition> All { get; } =
        Array.AsReadOnly(Fields.SelectMany(field => field.Definitions).ToArray());

    public static CustomerFieldPermissions For(CustomerField field) => Fields.Single(p => p.Field == field);
}
