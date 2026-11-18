using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Security;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class PendingOrderTests(Func<DatabaseContext> createContext)
{
    private static OrderService Service(IDbContextFactory<DatabaseContext> factory, IPermissionChecker permissions,
        ICurrentUserContext? session) => new(null!, null!, NullLogger<OrderService>.Instance, null!, factory, permissions, session: session);

    public static async Task CheckAuthorizationAsync()
    {
        var calls = 0;
        var factory = new TestContextFactory(() => { calls++; throw new Exception("Database boundary"); });
        var grants = new TestPermissions();
        var session = new TestUserContext { UserId = 1, IsLoggedIn = true };
        grants.Keys.Add(PermissionCatalog.OrdersViewAllManagers);
        await DeniedAsync(() => Service(factory, grants, session).GetUnsentOrdersAsync());
        grants.Keys.Add(PermissionCatalog.Orders);
        session.IsLoggedIn = false;
        await DeniedAsync(() => Service(factory, grants, session).GetUnsentOrdersAsync());
        await DeniedAsync(() => Service(factory, grants, null).GetUnsentOrdersAsync());
        Check(calls == 0, "Pending orders must require a logged-in session and Orders access before any database query.");
        Console.WriteLine("PASS: pending-order access denied before database queries for missing Orders, logout and missing session");
    }

    public async Task RunAsync()
    {
        var prefix = "PEND-" + Guid.NewGuid().ToString("N")[..8];
        var first = new UserEntity { UserName = prefix + "-1", FirstName = "Same", LastName = "Name", Password = "test", IsManager = true };
        var second = new UserEntity { UserName = prefix + "-2", FirstName = "Same", LastName = "Name", Password = "test", IsManager = true };
        var ordinary = new UserEntity { UserName = prefix + "-3", FirstName = "Seller", LastName = "Test", Password = "test" };
        var own = new SalesManagerEntity { Id = prefix + "-M1", FullName = "Different display name", User = first };
        var other = new SalesManagerEntity { Id = prefix + "-M2", FullName = "Other manager", User = second };
        var inactive = new SalesManagerEntity { Id = prefix + "-M3", FullName = "Inactive", User = ordinary, Active = false };
        var legacy = new SalesManagerEntity { Id = prefix + "-M4", FullName = "Unlinked" };
        var c1 = new CustomerEntity { Id = prefix + "-C1", FullName = "Own", SalesManager = own };
        var c2 = new CustomerEntity { Id = prefix + "-C2", FullName = "Other", SalesManager = other };
        var c3 = new CustomerEntity { Id = prefix + "-C3", FullName = "Unassigned" };
        var c4 = new CustomerEntity { Id = prefix + "-C4", FullName = "Inactive manager", SalesManager = inactive };
        var c5 = new CustomerEntity { Id = prefix + "-C5", FullName = "Unlinked manager", SalesManager = legacy };
        OrderEntity Row(string suffix, CustomerEntity? client, UserEntity seller, int day, bool sent = false, bool barter = false) => new()
        {
            Id = prefix + "-" + suffix, Customer = client, User = seller, Date = new(2026, 11, day),
            IsSent = sent, IsBarter = barter, Rate = 11, SuminWords = "Test"
        };
        await using (var db = createContext())
        {
            db.Orders.AddRange(Row("OWN-EARLY", c1, second, 1), Row("OWN-LATE", c1, ordinary, 3),
                Row("OTHER", c2, first, 2), Row("UNASSIGNED", c3, first, 4), Row("INACTIVE", c4, first, 5),
                Row("LEGACY", c5, first, 6), Row("NO-CLIENT", null, first, 7),
                Row("SENT", c1, first, 8, sent: true), Row("BARTER", c1, first, 9, barter: true));
            await db.SaveChangesAsync();
        }
        var grants = new TestPermissions(); grants.Keys.Add(PermissionCatalog.Orders);
        var session = new TestUserContext { UserId = first.Id, IsLoggedIn = true };
        var service = Service(new TestContextFactory(createContext), grants, session);
        var rows = await service.GetUnsentOrdersAsync();
        Check(rows.Select(o => o.InvoiceNumber).SequenceEqual([prefix + "-OWN-LATE", prefix + "-OWN-EARLY"]) &&
            rows.All(o => o.CustomerId == c1.Id && !o.IsSent),
            "Orders must follow the customer's manager via UserId, regardless of creator or display name, and retain newest-first ordering.");
        Check((await service.GetUnsentOrdersAsync(other.Id)).Count == 0,
            "Passing a foreign manager filter must never bypass the logged-in manager's access scope.");
        grants.Keys.Add(PermissionCatalog.CustomersViewAll);
        Check((await service.GetUnsentOrdersAsync()).Count == 2,
            "Full customer access must not grant access to every manager's pending orders.");
        grants.Keys.Add(PermissionCatalog.OrdersViewAllManagers);
        rows = (await service.GetUnsentOrdersAsync()).Where(o => o.InvoiceNumber.StartsWith(prefix)).ToList();
        Check(rows.Count == 7 && rows.Any(o => o.InvoiceNumber == prefix + "-UNASSIGNED") &&
            rows.All(o => o.InvoiceNumber != prefix + "-SENT" && o.InvoiceNumber != prefix + "-BARTER"),
            "Only ViewAllManagers must expose all unsent non-barter orders, including unassigned clients.");
        Check((await service.GetUnsentOrdersAsync(own.Id)).Select(o => o.InvoiceNumber)
                .SequenceEqual([prefix + "-OWN-LATE", prefix + "-OWN-EARLY"]) &&
            (await service.GetUnsentOrdersAsync(other.Id)).Single().InvoiceNumber == prefix + "-OTHER",
            "With ViewAllManagers, a selected manager must show only that manager's clients, independently of the order's creator.");
        Check((await service.GetUnsentOrdersAsync(legacy.Id)).Single().CustomerId == c5.Id &&
            (await service.GetUnsentOrdersAsync(inactive.Id)).Single().CustomerId == c4.Id &&
            (await service.GetUnsentOrdersAsync(prefix + "-MISSING")).Count == 0,
            "An explicit manager filter must match stored customer assignments and never fall back to all orders if it has no matches.");
        Check((await service.GetUnsentOrdersAsync(null)).Count(o => o.InvoiceNumber.StartsWith(prefix)) == 7 &&
            (await service.GetUnsentOrdersAsync(" ")).Count(o => o.InvoiceNumber.StartsWith(prefix)) == 7,
            "Clearing the manager filter with ViewAllManagers must restore all unsent orders, including unassigned clients.");
        grants.Keys.Remove(PermissionCatalog.OrdersViewAllManagers);
        Check((await service.GetUnsentOrdersAsync()).Count == 2, "Revoking all-manager access must immediately restore own scope.");
        session.UserId = second.Id;
        Check((await service.GetUnsentOrdersAsync()).Single().InvoiceNumber == prefix + "-OTHER",
            "Switching login must not retain the previous manager's scope.");
        session.UserId = ordinary.Id;
        Check((await service.GetUnsentOrdersAsync()).Count == 0,
            "An ordinary seller without an active manager must not receive unscoped orders, even with CustomersViewAll.");
        grants.Keys.Add(PermissionCatalog.OrdersViewAllManagers);
        Check((await service.GetUnsentOrdersAsync()).Any(o => o.InvoiceNumber == prefix + "-OTHER"),
            "An ordinary seller with the explicit all-manager grant must see other managers' orders.");
        session.UserId = first.Id;
        var changedGrant = Service(new TestContextFactory(() =>
        {
            grants.Keys.Remove(PermissionCatalog.OrdersViewAllManagers);
            return createContext();
        }), grants, session);
        grants.Keys.Add(PermissionCatalog.OrdersViewAllManagers);
        await DeniedAsync(() => changedGrant.GetUnsentOrdersAsync(other.Id));
        var changedLogin = Service(new TestContextFactory(() => { session.UserId = second.Id; return createContext(); }), grants, session);
        await DeniedAsync(() => changedLogin.GetUnsentOrdersAsync());
        session.UserId = first.Id;
        await using (var db = createContext())
        {
            await db.Customers.Where(c => c.Id == c1.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.SalesManagerId, other.Id));
            Check((await service.GetUnsentOrdersAsync()).Count == 0, "A fresh customer-manager reassignment must change order visibility.");
            await db.Customers.Where(c => c.Id == c1.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.SalesManagerId, own.Id));
            await db.SalesManagers.Where(m => m.Id == own.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Active, false));
            Check((await service.GetUnsentOrdersAsync()).Count == 0, "An inactive manager must not keep order access.");
            await db.SalesManagers.Where(m => m.Id == own.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Active, true));
            await db.Users.Where(u => u.Id == first.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsManager, false));
            Check((await service.GetUnsentOrdersAsync()).Count == 0, "Removing the manager flag must revoke manager scope.");
            await db.Orders.Where(o => o.Id.StartsWith(prefix)).ExecuteDeleteAsync();
            await db.Customers.Where(c => c.Id.StartsWith(prefix)).ExecuteDeleteAsync();
            await db.SalesManagers.Where(m => m.Id.StartsWith(prefix)).ExecuteDeleteAsync();
            await db.Users.Where(u => u.UserName.StartsWith(prefix)).ExecuteDeleteAsync();
        }
        Console.WriteLine("PASS: pending orders follow the selected manager with ViewAllManagers, clearing restores all; own-manager security, sent/barter exclusion, account/grant changes and fresh assignments remain enforced");
    }

    private static async Task DeniedAsync(Func<Task> action)
    {
        try { await action(); } catch (UnauthorizedAccessException) { return; }
        throw new InvalidOperationException("Expected pending-order access denial.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
