namespace Infrastructure.Constants;

public static class PermissionCatalog
{
    public const string Admin = "Admin";
    public const string AdminBrand = "Admin.Brand";
    public const string Customers = "Customers";
    public const string CustomersDebt = "Customers.Debt";
    public const string CustomersRestriction = "Customers.Restriction";
    public const string Expenses = "Expenses";
    public const string ExpensesRemove = "Expenses.Remove";
    public const string ExpensesShowAll = "Expenses.ShowAllExpenses";
    public const string Inventory = "Inventory";
    public const string InventoryCatalog = "Inventory.Catalog";
    public const string InventoryCatalogRemove = "Inventory.Catalog.Remove";
    public const string InventoryCatalogRemoveAll = "Inventory.Catalog.RemoveAll";
    public const string OnlyOfficialCustomer = "OnlyOfficialCustomer";
    public const string Orders = "Orders";
    public const string OrdersBarter = "Orders.Barter";
    public const string OrdersDirectFromStock = "Orders.DirectFromStock";
    public const string OrdersExceedBuyLimit = "Orders.ExceedBuyLimit";
    public const string ProductRequests = "ProductRequests";
    public const string Reports = "Reports";
    public const string Returns = "Returns";
    public const string Settings = "Settings";
    public const string Statistics = "Statistics";

    // Keys are persistent identifiers. Keep them stable when changing descriptions.
    public static IReadOnlyList<PermissionDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new PermissionDefinition(Admin, "Администрация"),
        new PermissionDefinition(AdminBrand, "Администрация.Бренды и фирмы"),
        new PermissionDefinition(Customers, "Клиенты"),
        new PermissionDefinition(CustomersDebt, "Клиенты.Задолженность.Просмотр (старое право)"),
        new PermissionDefinition(CustomersRestriction, "Клиенты.Ограничение.Просмотр (старое право)"),
        new PermissionDefinition(Expenses, "Расходы"),
        new PermissionDefinition(ExpensesRemove, "Расходы.Удалить"),
        new PermissionDefinition(ExpensesShowAll, "Расходы.ПоказатьВсеРасходы"),
        new PermissionDefinition(Inventory, "Инвентарь"),
        new PermissionDefinition(InventoryCatalog, "Инвентарь.Каталог"),
        new PermissionDefinition(InventoryCatalogRemove, "Инвентарь.Каталог.Удалить"),
        new PermissionDefinition(InventoryCatalogRemoveAll, "Инвентарь.Каталог.УдалитьВсё"),
        new PermissionDefinition(OnlyOfficialCustomer, "ТолькоОфициальныеКлиенты"),
        new PermissionDefinition(Orders, "Заказы"),
        new PermissionDefinition(OrdersBarter, "Заказы.Бартер"),
        new PermissionDefinition(OrdersDirectFromStock, "Заказы.Заказ прямо со склада"),
        new PermissionDefinition(OrdersExceedBuyLimit, "Заказы.Продажа сверх лимита суммы заказа"),
        new PermissionDefinition(ProductRequests, "Заявки на товары"),
        new PermissionDefinition(Reports, "Отчеты"),
        new PermissionDefinition(Returns, "Возвраты"),
        new PermissionDefinition(Settings, "Настройки"),
        new PermissionDefinition(Statistics, "Статистика")
    }.Concat(CustomerPermissionCatalog.All).ToArray());
}

public sealed record PermissionDefinition(string Key, string Description);
