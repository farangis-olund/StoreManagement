namespace Infrastructure.Constants;

public static class PermissionCatalog
{
    public const string Admin = "Admin";
    public const string AdminBrand = "Admin.Brand";
    public const string AdminManagers = "Admin.Managers";
    public const string AdminCoefficients = "Admin.Coefficients";
    public const string AdminBonuses = "Admin.Bonuses";
    public const string AdminClearance = "Admin.Clearance";
    public const string AdminUsers = "Admin.Users";
    public const string AdminReferences = "Admin.References";
    public const string AdminDatabase = "Admin.Database";
    public const string AdminCleanup = "Admin.Cleanup";
    public const string AdminReferenceOrganization = "Admin.References.Organization";
    public const string AdminReferenceRoles = "Admin.References.Roles";
    public const string AdminReferenceCouriers = "Admin.References.Couriers";
    public const string AdminReferencePickers = "Admin.References.Pickers";
    public const string AdminReferenceManagers = "Admin.References.Managers";
    public const string AdminReferencePriceLevels = "Admin.References.PriceLevels";
    public const string AdminReferenceCurrency = "Admin.References.Currency";
    public const string Customers = "Customers";
    public const string CustomersViewAll = "Customers.ViewAll";
    public const string CustomersDelete = "Customers.Delete";
    public const string CustomersDebt = "Customers.Debt";
    public const string CustomersBalanceView = "Customers.Balance.View";
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
    public const string OrdersViewAllManagers = "Orders.ViewAllManagers";
    public const string OrdersDelivery = "Orders.Delivery";
    public const string OrdersDeliveryView = "Orders.Delivery.View";
    public const string OrdersDeliveryUpdate = "Orders.Delivery.Update";
    public const string OrdersDeliveryPrint = "Orders.Delivery.Print";
    public const string OrdersDeliveryReport = "Orders.Delivery.Report";
    public const string OrdersAssignPickers = "Orders.AssignPickers";
    public const string ProductRequests = "ProductRequests";
    public const string OfficialMonthlyPlansManage = "OfficialMonthlyPlans.Manage";
    public const string OfficialMonthlyPlansView = "OfficialMonthlyPlans.View";
    public const string OfficialMonthlyPlansTotal = "OfficialMonthlyPlans.RetailTotal";
    public const string OfficialMonthlyPlansIndividual = "OfficialMonthlyPlans.RetailIndividual";
    public const string Reports = "Reports";
    public const string Returns = "Returns";
    public const string Settings = "Settings";
    public const string Statistics = "Statistics";

    // Keys are persistent identifiers. Keep them stable when changing descriptions.
    public static IReadOnlyList<PermissionDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new PermissionDefinition(Admin, "Администрация"),
        new PermissionDefinition(AdminBrand, "Администрация.Бренды и фирмы"),
        new PermissionDefinition(AdminManagers, "Администрация.Менеджеры"),
        new PermissionDefinition(AdminCoefficients, "Администрация.Коэффициенты"),
        new PermissionDefinition(AdminBonuses, "Администрация.Бонусы"),
        new PermissionDefinition(AdminClearance, "Администрация.Распродажа"),
        new PermissionDefinition(AdminUsers, "Администрация.Пользователи"),
        new PermissionDefinition(AdminReferences, "Администрация.Справочники"),
        new PermissionDefinition(AdminDatabase, "Администрация.База данных"),
        new PermissionDefinition(AdminCleanup, "Администрация.Очистка базы"),
        new PermissionDefinition(AdminReferenceOrganization, "Администрация.Справочники.Организация"),
        new PermissionDefinition(AdminReferenceRoles, "Администрация.Справочники.Управление ролями"),
        new PermissionDefinition(AdminReferenceCouriers, "Администрация.Справочники.Доставщики"),
        new PermissionDefinition(AdminReferencePickers, "Администрация.Справочники.Сборщики"),
        new PermissionDefinition(AdminReferenceManagers, "Администрация.Справочники.Менеджеры"),
        new PermissionDefinition(AdminReferencePriceLevels, "Администрация.Справочники.Уровни цен клиента"),
        new PermissionDefinition(AdminReferenceCurrency, "Администрация.Справочники.Назначение валюты"),
        new PermissionDefinition(Customers, "Клиенты"),
        new PermissionDefinition(CustomersViewAll, "Клиенты.Просмотр всех клиентов"),
        new PermissionDefinition(CustomersDelete, "Клиенты.Удаление клиента"),
        new PermissionDefinition(CustomersDebt, "Клиенты.Задолженность.Просмотр (старое право)"),
        new PermissionDefinition(CustomersBalanceView, "Клиенты.Остаток.Просмотр"),
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
        new PermissionDefinition(OrdersViewAllManagers, "Заказы.Просмотр всех менеджеров"),
        new PermissionDefinition(OrdersDelivery, "Заказы.Доставка заказов"),
        new PermissionDefinition(OrdersDeliveryView, "Заказы.Доставка заказов.Просмотр"),
        new PermissionDefinition(OrdersDeliveryUpdate, "Заказы.Доставка заказов.Обновить"),
        new PermissionDefinition(OrdersDeliveryPrint, "Заказы.Доставка заказов.Печать"),
        new PermissionDefinition(OrdersDeliveryReport, "Заказы.Доставка заказов.Отчёт"),
        new PermissionDefinition(OrdersAssignPickers, "Заказы.Назначение комплектовщиков"),
        new PermissionDefinition(ProductRequests, "Заявки на товары"),
        new PermissionDefinition(OfficialMonthlyPlansManage, "Месячные планы.Управление планами официальных клиентов"),
        new PermissionDefinition(OfficialMonthlyPlansView, "Месячные планы.Просмотр таблицы контроля"),
        new PermissionDefinition(OfficialMonthlyPlansTotal, "Месячные планы.Показ общего остатка в заказах"),
        new PermissionDefinition(OfficialMonthlyPlansIndividual, "Месячные планы.Показ остатка выбранного клиента в заказах"),
        new PermissionDefinition(Reports, "Отчеты"),
        new PermissionDefinition(Returns, "Возвраты"),
        new PermissionDefinition(Settings, "Настройки"),
        new PermissionDefinition(Statistics, "Статистика")
    }.Concat(CustomerPermissionCatalog.All).ToArray());
}

public sealed record PermissionDefinition(string Key, string Description);
