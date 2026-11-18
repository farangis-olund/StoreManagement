using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Security;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class CustomerBalanceTests
{
    public static async Task CheckAuthorizationAsync()
    {
        var grants = new TestPermissions();
        var calls = 0;
        var factory = new TestContextFactory(() => { calls++; throw new Exception("Database boundary"); });
        var policy = new CustomerFieldPermissionPolicy(grants);
        var service = new CustomerService(null!, factory, null!, null!, NullLogger<CustomerService>.Instance,
            policy, new CustomerAccessPolicy(grants, new TestUserContext()));
        foreach (var rights in new[]
        {
            Array.Empty<string>(), new[] { PermissionCatalog.CustomersBalanceView },
            new[] { PermissionCatalog.Customers },
            new[] { PermissionCatalog.Customers, PermissionCatalog.CustomersDebt, CustomerPermissionCatalog.Debt.View }
        })
        {
            grants.Keys.Clear(); grants.Keys.UnionWith(rights);
            Check(!policy.CanViewBalance, "Balance must require its own View grant and Customers module.");
            await Denied(() => service.GetBalanceForEditorAsync("TEST"));
            Check(calls == 0, "Balance permission must be checked before database access.");
        }
        grants.Keys.UnionWith([PermissionCatalog.Customers, PermissionCatalog.CustomersBalanceView]);
        Check(policy.CanViewBalance, "The separate balance View grant must enable reading.");
        Check(!PermissionCatalog.All.Any(p => p.Key is "Customers.Balance.Add" or "Customers.Balance.Edit" or "Customers.Balance.Delete"),
            "A calculated read-only balance must not have write permissions.");
        Console.WriteLine("PASS: customer balance has an independent read-only View grant, requires Customers, and rejects unauthorized reads before database access");
    }

    public static async Task RunAsync(Func<DatabaseContext> createContext)
    {
        await using var db = createContext();
        var prefix = "BAL-" + Guid.NewGuid().ToString("N")[..8];
        var user = new UserEntity { UserName = prefix, FirstName = "Balance", LastName = "Test", Password = "test", IsManager = true };
        var own = new SalesManagerEntity { Id = prefix + "-M", FullName = "Own", User = user, Active = true };
        var other = new SalesManagerEntity { Id = prefix + "-OTHER", FullName = "Other", Active = true };
        var customer = new CustomerEntity { Id = prefix + "-A", FullName = "Own customer", Debt = 1000, SalesManager = own };
        var foreign = new CustomerEntity { Id = prefix + "-B", FullName = "Other customer", Debt = 300, SalesManager = other };
        db.Customers.AddRange(customer, foreign);
        await db.SaveChangesAsync();
        var grants = new TestPermissions(); grants.Keys.UnionWith([PermissionCatalog.Customers, PermissionCatalog.CustomersBalanceView]);
        var session = new TestUserContext { UserId = user.Id, IsLoggedIn = true };
        var factory = new TestContextFactory(createContext);
        var finance = new CustomerFinanceService(factory);
        CustomerService Service(TestContextFactory contextFactory) => new(null!, contextFactory, null!, null!,
            NullLogger<CustomerService>.Instance, new CustomerFieldPermissionPolicy(grants), new CustomerAccessPolicy(grants, session));
        var service = Service(factory);
        async Task CheckBalance(decimal expected)
        {
            var summary = await finance.GetFinanceInfoAsync(customer.Id, orderId: null!);
            Check(summary.Balance == expected && await service.GetBalanceForEditorAsync(customer.Id) == expected,
                "The customer card must use the exact customer-summary balance.");
        }
        await CheckBalance(1000);
        await Denied(() => service.GetBalanceForEditorAsync(foreign.Id));
        grants.Keys.Add(PermissionCatalog.CustomersViewAll);
        Check(await service.GetBalanceForEditorAsync(foreign.Id) == 300, "ViewAll must allow the balance of another manager's client.");
        grants.Keys.Remove(PermissionCatalog.CustomersViewAll);

        var product = new ProductEntity
        {
            ArticleNumber = prefix, ProductName = "Balance fixture", Model = "Test", Marka = "Test",
            Alternative = "", WarehousePlace = "A1/1", Quentity = 10,
            Brand = new BrandEntity { BrandName = prefix }, Group = new GroupEntity { GroupName = prefix }
        };
        var order = new OrderEntity
        {
            Id = prefix + "-ORDER", Customer = customer, User = user, SuminWords = "Test",
            OrderDetails = [new OrderDetailEntity { Product = product, Price = 125.25m, Quentity = 2 }]
        };
        db.Orders.Add(order);
        db.Payments.Add(new CustomerPaymentEntity { Customer = customer, Amount = 200, Date = DateTime.Today });
        db.Returns.AddRange(
            new ReturnEntity { Id = prefix + "-BAL", Customer = customer, TotalAmount = 80, RefundMethod = RefundMethodConstants.Balance },
            new ReturnEntity { Id = prefix + "-CASH", Customer = customer, TotalAmount = 50, RefundMethod = RefundMethodConstants.Cash });
        await db.SaveChangesAsync();
        await CheckBalance(970.50m);
        customer.Debt = -1000;
        await db.SaveChangesAsync();
        await CheckBalance(-1029.50m);
        Check((await db.Customers.AsNoTracking().SingleAsync(c => c.Id == customer.Id)).Debt == -1000,
            "Reading a balance must not overwrite the saved initial debt.");

        var contextCalls = 0;
        var revoking = Service(new TestContextFactory(() =>
        {
            if (++contextCalls == 2) grants.Keys.Remove(PermissionCatalog.CustomersBalanceView);
            return createContext();
        }));
        await Denied(() => revoking.GetBalanceForEditorAsync(customer.Id));
        grants.Keys.Add(PermissionCatalog.CustomersBalanceView);
        customer.SalesManager = other;
        await db.SaveChangesAsync();
        await Denied(() => service.GetBalanceForEditorAsync(customer.Id));

        var customerIds = new[] { customer.Id, foreign.Id };
        await db.Payments.Where(p => customerIds.Contains(p.CustomerId)).ExecuteDeleteAsync();
        await db.Returns.Where(r => customerIds.Contains(r.CustomerId)).ExecuteDeleteAsync();
        await db.OrderDetails.Where(d => d.OrderId == order.Id).ExecuteDeleteAsync();
        await db.Orders.Where(o => o.Id == order.Id).ExecuteDeleteAsync();
        await db.Customers.Where(c => customerIds.Contains(c.Id)).ExecuteDeleteAsync();
        await db.SalesManagers.Where(m => m.Id == own.Id || m.Id == other.Id).ExecuteDeleteAsync();
        await db.Products.Where(p => p.ArticleNumber == product.ArticleNumber).ExecuteDeleteAsync();
        await db.Brands.Where(b => b.Id == product.BrandId).ExecuteDeleteAsync();
        await db.Groups.Where(g => g.Id == product.GroupId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
        Console.WriteLine("PASS: customer-card balance matches summary sales/debt/payments/returns and credit; fresh manager scope, ViewAll and mid-read permission revocation are enforced");
    }

    private static async Task Denied(Func<Task<decimal>> read)
    {
        try { await read(); }
        catch (UnauthorizedAccessException) { return; }
        throw new InvalidOperationException("Expected balance access denial.");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
