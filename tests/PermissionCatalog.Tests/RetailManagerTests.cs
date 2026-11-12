using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

internal sealed class RetailManagerTests(Func<DatabaseContext> createContext)
{
    public static async Task CheckAuthorizationAsync()
    {
        var grants = new TestPermissions();
        var session = new TestUserContext { UserId = 1, IsLoggedIn = true };
        var calls = 0;
        var service = new RetailManagerService(new TestContextFactory(() => { calls++; throw new Exception("Database boundary"); }), grants, session);
        grants.Keys.Add(PermissionCatalog.OrdersViewAllManagers);
        await DeniedAsync(() => service.LoadAsync());
        await DeniedAsync(() => service.EnsureCustomerAccessAsync("C1"));
        grants.Keys.Add(PermissionCatalog.Orders); session.IsLoggedIn = false;
        await DeniedAsync(() => service.LoadAsync());
        Check(calls == 0, "A missing Orders permission or logged-out session reached the database.");
    }

    public async Task RunAsync()
    {
        var first = new UserEntity { UserName = "RM-FIRST", FirstName = "Same", LastName = "Name", Password = "test", IsManager = true };
        var second = new UserEntity { UserName = "RM-SECOND", FirstName = "Same", LastName = "Name", Password = "test", IsManager = true };
        var ordinary = new UserEntity { UserName = "RM-ORDINARY", FirstName = "Ordinary", LastName = "User", Password = "test" };
        await using (var db = createContext())
        {
            db.Users.AddRange(first, second, ordinary);
            db.SalesManagers.AddRange(new SalesManagerEntity { Id = "RM-M1", FullName = "Different display name", User = first },
                new() { Id = "RM-M2", FullName = "Same Name", User = second },
                new() { Id = "RM-DISABLED", FullName = "Disabled", User = ordinary, Active = true },
                new() { Id = "RM-LEGACY", FullName = "Unlinked active manager" });
            db.Customers.AddRange(new CustomerEntity { Id = "RM-C1", FullName = "First client", SalesManagerId = "RM-M1" },
                new() { Id = "RM-C2", FullName = "Second client", SalesManagerId = "RM-M2" },
                new() { Id = "RM-C3", FullName = "Unassigned client" });
            await db.SaveChangesAsync();
        }
        var grants = new TestPermissions(); grants.Keys.Add(PermissionCatalog.Orders);
        var session = new TestUserContext { UserId = first.Id, IsLoggedIn = true };
        var service = new RetailManagerService(new TestContextFactory(createContext), grants, session);
        var state = await service.LoadAsync();
        Check(state.UserId == first.Id && state.OwnManagerId == "RM-M1" && state.Managers.Single().UserId == first.Id &&
            state.Customers.Select(c => c.Id).SequenceEqual(["RM-C1"]), "Own manager must be resolved by UserId, independent of names.");
        await service.EnsureCustomerAccessAsync("RM-C1");
        await DeniedAsync(() => service.EnsureCustomerAccessAsync("RM-C2"));
        await DeniedAsync(() => service.EnsureCustomerAccessAsync("RM-C3"));
        grants.Keys.Add(PermissionCatalog.OrdersViewAllManagers);
        state = await service.LoadAsync();
        Check(state.CanViewAllManagers && state.OwnManagerId == "RM-M1" && state.Managers.Any(m => m.Id == "RM-M2") &&
            state.Managers.Any(m => m.Id == "RM-LEGACY") && state.Managers.All(m => m.Id != "RM-DISABLED") &&
            state.Customers.Any(c => c.Id == "RM-C2"), "ViewAllManagers must reveal other active managers while keeping the own default.");
        await service.EnsureCustomerAccessAsync("RM-C2");
        grants.Keys.Remove(PermissionCatalog.OrdersViewAllManagers);
        state = await service.LoadAsync();
        Check(state.Managers.Count == 1 && state.Customers.Count == 1, "A removed permission remained cached.");
        await DeniedAsync(() => service.EnsureCustomerAccessAsync("RM-C2"));
        session.UserId = second.Id;
        state = await service.LoadAsync();
        Check(state.OwnManagerId == "RM-M2" && state.Customers.Single().Id == "RM-C2", "A different login retained another manager's scope.");
        session.UserId = ordinary.Id;
        state = await service.LoadAsync();
        Check(state.OwnManagerId == null && state.Managers.Count == 0 && state.Customers.Count == 0,
            "A user without an active manager or all-client permission received unscoped data.");
        grants.Keys.Add(PermissionCatalog.CustomersViewAll);
        state = await service.LoadAsync();
        Check(state.Managers.Count == 0 && state.Customers.Any(c => c.Id == "RM-C1"), "Existing full-customer access for an ordinary seller was lost.");
        await service.EnsureCustomerAccessAsync("RM-C1");
        session.UserId = first.Id;
        state = await service.LoadAsync();
        Check(state.Managers.Count == 1 && state.Customers.Single().Id == "RM-C1", "Full-customer rights bypassed a fixed own-manager selection.");
        grants.Keys.Remove(PermissionCatalog.CustomersViewAll);
        await using (var db = createContext())
            await db.Users.Where(u => u.Id == first.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsManager, false));
        state = await service.LoadAsync();
        Check(state.Managers.Count == 0 && state.Customers.Count == 0, "An old user-to-manager snapshot survived a profile change.");
        await DeniedAsync(() => service.EnsureCustomerAccessAsync("RM-C1"));
        session.IsLoggedIn = false;
        await DeniedAsync(() => service.LoadAsync());
        Console.WriteLine("PASS: retail manager UserId lookup, own-client scope, separate ViewAllManagers, full-client fallback, disabled managers, fresh revocation and account switching");
    }
    private static async Task DeniedAsync(Func<Task> action)
    {
        try { await action(); } catch (UnauthorizedAccessException) { return; }
        throw new Exception("Expected retail manager access rejection.");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
