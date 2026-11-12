using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Security;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class CustomerDeletionTests
{
    public static async Task CheckAuthorization()
    {
        var grants = new TestPermissions();
        var calls = 0;
        var factory = new TestContextFactory(() => { calls++; throw new Exception("Database boundary."); });
        var policy = new CustomerFieldPermissionPolicy(grants);
        var service = new CustomerService(null!, factory, null!, null!, NullLogger<CustomerService>.Instance, policy, new CustomerAccessPolicy(grants, new TestUserContext()));
        foreach (var mask in new[] { 0, 1, 3, 2, 1 })
        {
            grants.Keys.Clear();
            if ((mask & 1) != 0) grants.Keys.Add(PermissionCatalog.Customers);
            if ((mask & 2) != 0) grants.Keys.Add(PermissionCatalog.CustomersDelete);
            grants.Keys.UnionWith(CustomerPermissionCatalog.Fields.Select(f => f.Delete));
            calls = 0;
            try
            {
                await service.DeleteCustomerAsync(new() { Id = "DELETE-EMPTY" });
                Check(mask == 3 && calls == 1, "Deletion must require both module access and the separate record-deletion grant.");
            }
            catch (UnauthorizedAccessException)
            {
                Check(mask != 3 && calls == 0, "Unauthorized deletion must be rejected before database access.");
            }
        }
        Console.WriteLine("PASS: customer deletion grant, independent field-clear rights, module access and live revocation before database access");
    }

    public static async Task RunAsync(Func<DatabaseContext> createContext)
    {
        var grants = new TestPermissions();
        grants.Keys.UnionWith([PermissionCatalog.Customers, PermissionCatalog.CustomersDelete, PermissionCatalog.CustomersViewAll]);
        var factory = new TestContextFactory(createContext);
        var plans = new OfficialMonthlyPlanService(factory, grants);
        var changes = 0;
        plans.Changed += (_, _) => changes++;
        var service = new CustomerService(null!, factory, null!, null!, NullLogger<CustomerService>.Instance,
            new CustomerFieldPermissionPolicy(grants), new CustomerAccessPolicy(grants, new TestUserContext()), plans);
        var ids = new[] { "DELETE-EMPTY", "DELETE-ORDER", "DELETE-PAYMENT", "DELETE-RETURN", "DELETE-PLAN", "DELETE-REQUEST" };
        await using (var db = createContext())
        {
            var user = new UserEntity { UserName = "delete-test", FirstName = "Delete", LastName = "Test", Password = "test-only" };
            db.Users.Add(user);
            db.SalesManagers.Add(new() { Id = "DELETE-MANAGER", FullName = "Delete test manager" });
            foreach (var id in ids) db.Customers.Add(new() { Id = id, FullName = id });
            await db.SaveChangesAsync();
            db.ManagerCustomers.Add(new() { CustomerId = ids[0], ManagerId = "DELETE-MANAGER" });
            db.Orders.Add(new() { Id = "DELETE-ORDER", CustomerId = ids[1], UserId = user.Id, SuminWords = "test", Rate = 1 });
            db.Payments.Add(new() { CustomerId = ids[2], Date = DateTime.Today, Amount = 100, Rate = 1 });
            db.Returns.Add(new() { Id = "DELETE-RETURN", CustomerId = ids[3], TotalAmount = 100, IsManual = true });
            db.OfficialMonthlyPlans.Add(new() { CustomerId = ids[4], Month = new(2026, 11, 1), Amount = 100 });
            db.ProductRequests.Add(new() { Id = Guid.NewGuid(), CustomerId = ids[5], CustomerName = ids[5],
                UserId = user.Id, ArticleNumber = "DELETE-ARTICLE", Quantity = 1, Kind = ProductRequestKind.NewArticle });
            await db.SaveChangesAsync();
        }
        foreach (var id in ids.Skip(1))
        {
            try { await service.DeleteCustomerAsync(new() { Id = id }); throw new Exception("A linked customer was deleted."); }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Нельзя удалить клиента:")) { }
            await using var db = createContext();
            Check(await db.Customers.AnyAsync(c => c.Id == id), "Rejected deletion removed the customer.");
        }
        Check(changes == 0, "Rejected deletions must not publish data changes.");
        Check(await service.DeleteCustomerAsync(new() { Id = ids[0], FullName = "Unsaved stale form value" }),
            "An authorized user must be able to delete a persisted client using a fresh context.");
        Check(!await service.DeleteCustomerAsync(new() { Id = ids[0] }), "A missing client must not report a successful deletion.");
        await using (var db = createContext())
        {
            Check(!await db.Customers.AnyAsync(c => c.Id == ids[0]) && !await db.ManagerCustomers.AnyAsync(c => c.CustomerId == ids[0]),
                "Deleting a client must remove its manager assignment in the same operation.");
            Check(await db.Orders.AnyAsync(o => o.Id == ids[1]) && await db.Payments.AnyAsync(p => p.CustomerId == ids[2]) &&
                await db.Returns.AnyAsync(r => r.Id == ids[3]) && await db.OfficialMonthlyPlans.AnyAsync(p => p.CustomerId == ids[4]) &&
                await db.ProductRequests.AnyAsync(r => r.CustomerId == ids[5]), "Deletion attempts changed linked data.");
            await db.Payments.Where(p => ids.Contains(p.CustomerId)).ExecuteDeleteAsync();
            await db.Returns.Where(r => ids.Contains(r.CustomerId)).ExecuteDeleteAsync();
            await db.Orders.Where(o => o.CustomerId != null && ids.Contains(o.CustomerId)).ExecuteDeleteAsync();
            await db.OfficialMonthlyPlans.Where(p => ids.Contains(p.CustomerId)).ExecuteDeleteAsync();
            await db.ProductRequests.Where(r => ids.Contains(r.CustomerId)).ExecuteDeleteAsync();
            await db.Customers.Where(c => ids.Contains(c.Id)).ExecuteDeleteAsync();
            await db.SalesManagers.Where(m => m.Id == "DELETE-MANAGER").ExecuteDeleteAsync();
            await db.Users.Where(u => u.UserName == "delete-test").ExecuteDeleteAsync();
        }
        Check(changes == 1, "Only a committed deletion may notify open data views.");
        Console.WriteLine("PASS: customer deletion, fresh context, manager-link cleanup, linked-record protection, missing clients and commit notifications");
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
