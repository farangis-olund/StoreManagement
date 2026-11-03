using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Infrastructure.Security;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class CustomerPermissionTests(Func<DatabaseContext> createContext)
{
    private static int _assertions;

    public static void CheckPolicy()
    {
        foreach (var keys in CustomerPermissionCatalog.Fields)
        {
            var grants = new TestPermissions();
            var policy = new CustomerFieldPermissionPolicy(grants);
            var empty = Value(keys.Field, 0);
            var first = Value(keys.Field, 1);
            var changed = Value(keys.Field, 2);
            grants.Keys.UnionWith(new[] { keys.View, keys.Add, keys.Edit, keys.Delete });
            Assert(!policy.CanView(keys.Field), "Field rights cannot bypass module access.");
            Denied(() => policy.EnsureCanChange(keys.Field, empty, first));
            grants.Keys.Add(PermissionCatalog.Customers);
            grants.Keys.Remove(keys.View);
            Denied(() => policy.EnsureCanChange(keys.Field, empty, first));
            grants.Keys.Add(keys.View);
            grants.Keys.Remove(keys.Add);
            Denied(() => policy.EnsureCanChange(keys.Field, empty, first));
            grants.Keys.Add(keys.Add);
            policy.EnsureCanChange(keys.Field, empty, first);
            grants.Keys.Remove(keys.Edit);
            if (keys.Field != CustomerField.ExcludeDailyRepayment)
            {
                Denied(() => policy.EnsureCanChange(keys.Field, first, changed));
                grants.Keys.Add(keys.Edit);
                policy.EnsureCanChange(keys.Field, first, changed);
            }
            grants.Keys.Remove(keys.Delete);
            if (keys.IsRequired)
            {
                Required(() => policy.EnsureCanChange(keys.Field, first, empty));
                grants.Keys.Add(keys.Delete);
                Required(() => policy.EnsureCanChange(keys.Field, first, empty));
                Assert(!policy.CanDelete(keys.Field), "Required fields cannot be cleared.");
            }
            else
            {
                Denied(() => policy.EnsureCanChange(keys.Field, first, empty));
                grants.Keys.Add(keys.Delete);
                policy.EnsureCanChange(keys.Field, first, empty);
            }
            grants.Keys.Clear();
            grants.Keys.Add(PermissionCatalog.Customers);
            policy.EnsureCanSave(first, first);
            if (keys.LegacyViewKey != null)
            {
                grants.Keys.Add(keys.LegacyViewKey);
                Assert(policy.CanView(keys.Field), "Legacy view permission was lost.");
                Denied(() => policy.EnsureCanChange(keys.Field, empty, first));
                Denied(() => policy.EnsureCanChange(keys.Field, first, changed));
            }
        }

        var coefficientKeys = CustomerPermissionCatalog.Coefficients;
        var mixedGrants = new TestPermissions();
        mixedGrants.Keys.UnionWith(new[] { PermissionCatalog.Customers, coefficientKeys.View, coefficientKeys.Edit });
        var mixedPolicy = new CustomerFieldPermissionPolicy(mixedGrants);
        var mixed = new CustomerEntity { DailyPurchaseCoefficient = 1, ContractDate = null };
        var all = Value(CustomerField.Coefficients, 2);
        Denied(() => mixedPolicy.EnsureCanChange(CustomerField.Coefficients, mixed, all));
        mixedGrants.Keys.Add(coefficientKeys.Add);
        mixedPolicy.EnsureCanChange(CustomerField.Coefficients, mixed, all);

        foreach (var field in new[] { CustomerField.BuyLimit, CustomerField.UnpaidPaymentLimit })
        {
            var grants = new TestPermissions();
            var keys = CustomerPermissionCatalog.For(field);
            grants.Keys.UnionWith(new[] { PermissionCatalog.Customers, keys.View, keys.Edit });
            var policy = new CustomerFieldPermissionPolicy(grants);
            var unlimited = Value(field, 0);
            if (field == CustomerField.BuyLimit) unlimited.BuyLimit = -1;
            else unlimited.UnpaidPaymentLimit = 0;
            Denied(() => policy.EnsureCanChange(field, Value(field, 1), unlimited));
            grants.Keys.Add(keys.Delete);
            policy.EnsureCanChange(field, Value(field, 1), unlimited);
        }

        var requiredGrants = new TestPermissions();
        requiredGrants.Keys.Add(PermissionCatalog.Customers);
        var createPolicy = new CustomerFieldPermissionPolicy(requiredGrants);
        Assert(!createPolicy.CanCreateCustomer, "Creation requires all required field grants.");
        foreach (var keys in CustomerPermissionCatalog.Fields.Where(p => p.IsRequired))
            requiredGrants.Keys.UnionWith(new[] { keys.View, keys.Add });
        Assert(createPolicy.CanCreateCustomer, "Required field add grants should allow creation.");
        Console.WriteLine($"PASS: customer field policy ({_assertions} assertions across all 10 field groups)");
    }

    public async Task CheckServices()
    {
        var grants = new TestPermissions();
        grants.Keys.UnionWith(PermissionCatalog.All.Select(p => p.Key));
        var policy = new CustomerFieldPermissionPolicy(grants);
        var factory = new TestContextFactory(createContext);
        var service = new CustomerService(new CustomerRepository(factory), factory,
            new PriceLevelRepository(factory), new SalesManagerRepository(factory),
            NullLogger<CustomerService>.Instance, policy);
        await using (var db = createContext())
        {
            db.PriceLevels.Add(new PriceLevelEntity { Level = "test-level", PriceType = "Test" });
            db.SalesManagers.AddRange(new SalesManagerEntity { Id = "test-manager", FullName = "Test" },
                new SalesManagerEntity { Id = "other-manager", FullName = "Other" });
            await db.SaveChangesAsync();
        }

        var customer = new CustomerEntity
        {
            FullName = "Permission test", MobilePhone = "1", Address = "Test", City = "Test", Region = "Test",
            PriceLevelId = "test-level", SalesManagerId = "test-manager", Territory = "Test", ContractDate = null,
            Debt = 1200, BuyLimit = 1000, UnpaidPaymentLimit = 1300,
            DailyPurchaseCoefficient = 1, DailyRepaymentCoefficient = 1, DailyPlannedPurchaseCoefficient = 1
        };
        var saved = await service.AddCustomerAsync(customer);
        Assert(saved != null && !string.IsNullOrEmpty(saved.Id), "New customer did not save.");
        var id = saved!.Id;
        await using var staleContext = createContext();
        var stale = await staleContext.Customers.SingleAsync(c => c.Id == id);
        var edited = await Load(id);
        edited.UnpaidPaymentLimit = 1500;
        edited.SalesManagerId = "other-manager";
        grants.Keys.Remove(CustomerPermissionCatalog.UnpaidPaymentLimit.Edit);
        await DeniedAsync(() => service.AddCustomerAsync(edited));
        Assert((await Load(id)).UnpaidPaymentLimit == 1300, "Denied update changed a saved limit.");
        await using (var db = createContext())
            Assert(await db.ManagerCustomers.AnyAsync(l => l.CustomerId == id && l.ManagerId == "test-manager"),
                "Denied update changed the manager link.");
        grants.Keys.Add(CustomerPermissionCatalog.UnpaidPaymentLimit.Edit);
        await service.AddCustomerAsync(edited);
        Assert(stale.UnpaidPaymentLimit == 1300 && (await Load(id)).UnpaidPaymentLimit == 1500,
            "Save must use a fresh context, not the stale customer tracked by another view.");
        await using (var db = createContext())
            Assert(await db.ManagerCustomers.CountAsync(l => l.CustomerId == id) == 1 &&
                await db.ManagerCustomers.AnyAsync(l => l.CustomerId == id && l.ManagerId == "other-manager"),
                "Customer and manager link must be saved together.");
        Console.WriteLine("PASS: customer save permissions, manager links, and stale-context regression");

        edited = await Load(id);
        edited.BuyLimit = null;
        grants.Keys.Remove(CustomerPermissionCatalog.BuyLimit.Edit);
        await service.AddCustomerAsync(edited);
        Assert((await Load(id)).BuyLimit == null, "Delete-only permission must allow clearing a limit.");
        edited = await Load(id);
        edited.BuyLimit = 1000;
        grants.Keys.Remove(CustomerPermissionCatalog.BuyLimit.Add);
        await DeniedAsync(() => service.AddCustomerAsync(edited));
        grants.Keys.Add(CustomerPermissionCatalog.BuyLimit.Add);
        await service.AddCustomerAsync(edited);
        Assert((await Load(id)).BuyLimit == 1000, "Add must allow filling a cleared limit.");
        grants.Keys.Add(CustomerPermissionCatalog.BuyLimit.Edit);

        foreach (var property in new[] { "FullName", "MobilePhone", "Address", "City", "Region", "PriceLevelId", "SalesManagerId", "Territory" })
        {
            edited = await Load(id);
            typeof(CustomerEntity).GetProperty(property)!.SetValue(edited, "");
            await InvalidAsync(() => service.AddCustomerAsync(edited));
            Assert(!string.IsNullOrWhiteSpace((string?)typeof(CustomerEntity).GetProperty(property)!.GetValue(await Load(id))),
                "Required field was cleared: " + property);
        }
        edited = await Load(id);
        edited.PriceLevelId = "missing-level";
        await InvalidAsync(() => service.AddCustomerAsync(edited));
        edited = await Load(id);
        edited.SalesManagerId = "missing-manager";
        await InvalidAsync(() => service.AddCustomerAsync(edited));
        Console.WriteLine("PASS: all required fields and selected reference values remain validated");

        edited = await Load(id);
        edited.Debt = 1300;
        grants.Keys.Remove(CustomerPermissionCatalog.Debt.Edit);
        await DeniedAsync(() => service.AddCustomerAsync(edited));
        Assert((await Load(id)).Debt == 1200, "Add cannot change an existing saved value.");
        grants.Keys.Add(CustomerPermissionCatalog.Debt.Edit);

        await using var coefficientContext = createContext();
        var coefficients = new CoefficientService(coefficientContext, factory, policy);
        await coefficients.GetOrCreateAsync();
        grants.Keys.Remove(CustomerPermissionCatalog.Coefficients.Delete);
        await DeniedAsync(() => coefficients.CalculateForCustomerAsync(id));
        var unchanged = await Load(id);
        Assert(unchanged.DailyPurchaseCoefficient == 1 && unchanged.DailyRepaymentCoefficient == 1 &&
            unchanged.DailyPlannedPurchaseCoefficient == 1, "Calculation partially saved before denial.");
        grants.Keys.Add(CustomerPermissionCatalog.Coefficients.Delete);
        await coefficients.CalculateForCustomerAsync(id);
        var calculated = await Load(id);
        Assert(calculated.DailyPurchaseCoefficient == 16.67 && calculated.DailyRepaymentCoefficient == 4.17 &&
            calculated.DailyPlannedPurchaseCoefficient == 0, "Coefficient formulas changed.");
        Assert(calculated.UnpaidPaymentLimit == 1500 && calculated.BuyLimit == 1000,
            "Coefficient calculation overwrote customer limits.");
        await using (var db = createContext())
        {
            var config = await db.RaschetKoefficenta.FirstAsync();
            config.KoefEzhPogashDin1 = 0;
            await db.SaveChangesAsync();
        }
        await InvalidAsync(() => coefficients.CalculateForCustomerAsync(id));
        Assert((await Load(id)).DailyRepaymentCoefficient == 4.17, "Invalid configuration changed coefficients.");
        Console.WriteLine("PASS: atomic coefficient calculation, unchanged formulas, and invalid configuration handling");
    }

    private async Task<CustomerEntity> Load(string id)
    {
        await using var db = createContext();
        return await db.Customers.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    private static CustomerEntity Value(CustomerField field, int value)
    {
        var c = new CustomerEntity { ContractDate = null };
        double? number = value == 0 ? null : value;
        decimal? amount = value == 0 ? null : value;
        string? text = value == 0 ? null : value.ToString();
        switch (field)
        {
            case CustomerField.Debt: c.Debt = number; break;
            case CustomerField.Restriction: c.Restriction = number; break;
            case CustomerField.BuyLimit: c.BuyLimit = amount; break;
            case CustomerField.UnpaidPaymentLimit: c.UnpaidPaymentLimit = amount; break;
            case CustomerField.Coefficients:
                c.DailyPurchaseCoefficient = c.DailyRepaymentCoefficient = c.DailyPlannedPurchaseCoefficient = number; break;
            case CustomerField.ContractDate: c.ContractDate = value == 0 ? null : new DateTime(2026, 1, value); break;
            case CustomerField.PriceLevel: c.PriceLevelId = text; break;
            case CustomerField.SalesManager: c.SalesManagerId = text; break;
            case CustomerField.Territory: c.Territory = text; break;
            case CustomerField.ExcludeDailyRepayment: c.ExcludeDailyRepayment = value != 0; break;
        }
        return c;
    }

    private static void Denied(Action action)
    {
        try { action(); }
        catch (UnauthorizedAccessException) { _assertions++; return; }
        throw new InvalidOperationException("Expected permission denial.");
    }
    private static void Required(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { _assertions++; return; }
        throw new Exception("Expected required-field rejection.");
    }
    private static async Task DeniedAsync(Func<Task> action)
    {
        try { await action(); }
        catch (UnauthorizedAccessException) { return; }
        throw new InvalidOperationException("Expected service permission denial.");
    }
    private static async Task InvalidAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { return; }
        throw new Exception("Expected validation rejection.");
    }
    private static void Assert(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed class TestPermissions : IPermissionChecker
{
    public HashSet<string> Keys { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Has(string key) => Keys.Contains(key);
}

internal sealed class TestContextFactory(Func<DatabaseContext> createContext) : IDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext() => createContext();
}
