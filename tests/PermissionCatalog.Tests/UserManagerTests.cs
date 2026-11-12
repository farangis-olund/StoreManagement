using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Migrations;
using Infrastructure.Repositories;
using Infrastructure.Security;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class TestUserContext : ICurrentUserContext
{
    public int UserId { get; set; }
    public bool IsLoggedIn { get; set; }
}

internal sealed class UserManagerTests(Func<DatabaseContext> createContext)
{
    public async Task RunAsync()
    {
        await CheckMigrationAsync();
        var factory = new TestContextFactory(createContext);
        var users = new UserService(factory);
        var managers = new ManagerService(factory);
        var role = new RoleEntity { RoleName = "UserManager tests", Description = "Test" };
        await using (var db = createContext()) { db.Roles.Add(role); await db.SaveChangesAsync(); }
        UserEntity User(string name, bool manager = true) => new() { UserName = "UM-" + name,
            FirstName = name, LastName = "Test", Password = "test-only", Address = "Address 1", PhoneNumber = "111", IsManager = manager };
        var first = User("first");
        await users.AddAsync(first, role.Id);
        var firstRead = (await users.GetByIdAsync(first.Id))!;
        var managerId = firstRead.SalesManager!.Id;
        Check(firstRead.UserRoles.Single().RoleId == role.Id && firstRead.SalesManager.FullName == "first Test" &&
            firstRead.SalesManager.Address == "Address 1" && firstRead.SalesManager.Contacts == "111" && firstRead.SalesManager.Active,
            "Creating a manager user must create a complete linked profile and role atomically.");
        var normal = User("ordinary", false);
        await users.AddAsync(normal, role.Id);
        Check((await users.GetByIdAsync(normal.Id))!.SalesManager == null, "Ordinary users must not get manager rows.");
        var other = User("other"); await users.AddAsync(other, role.Id);
        var otherId = (await users.GetByIdAsync(other.Id))!.SalesManager!.Id;
        var brand = new BrandEntity { BrandName = "UM-BRAND" };
        await using (var db = createContext())
        {
            db.Brands.Add(brand);
            db.PriceLevels.Add(new() { Level = "UM-LEVEL", PriceType = "Test level" });
            db.SalesManagers.Add(new() { Id = "UM-LEGACY", FullName = "Historical manager", Address = "Legacy address", Contacts = "222" });
            db.Customers.AddRange(Customer("UM-OWN", managerId), Customer("UM-FOREIGN", otherId), Customer("UM-LEGACY-CLIENT", "UM-LEGACY"), Customer("UM-UNASSIGNED", null));
            db.ManagerCustomers.Add(new() { CustomerId = "UM-FOREIGN", ManagerId = managerId }); // A stale duplicate junction must not grant access.
            db.ManagerBrands.Add(new() { ManagerId = "UM-LEGACY", Brand = brand, SalesPercentage = 7 });
            await db.SaveChangesAsync();
        }
        firstRead.FirstName = "Renamed"; firstRead.Address = "Address 2"; firstRead.PhoneNumber = "333";
        await users.UpdateAsync(firstRead, role.Id);
        var renamed = (await users.GetByIdAsync(first.Id))!;
        Check(renamed.SalesManager!.Id == managerId && renamed.SalesManager.FullName == "Renamed Test" &&
            renamed.SalesManager.Address == "Address 2" && renamed.SalesManager.Contacts == "333",
            "Editing the user must update the same manager, not create a duplicate or use stale values.");
        await using (var db = createContext())
            Check(await db.SalesManagers.CountAsync(m => m.UserId == first.Id) == 1, "Duplicate manager profile.");

        await using (var db = createContext())
            Check((await db.Customers.SingleAsync(c => c.Id == "UM-LEGACY-CLIENT")).SalesManagerId == "UM-LEGACY" &&
                await db.ManagerBrands.AnyAsync(b => b.ManagerId == "UM-LEGACY" && b.SalesPercentage == 7), "Profile sync changed unrelated manager assignments.");
        await RejectAsync<ArgumentException>(() => users.AddAsync(User("invalid-role"), -1));
        Check(!await users.ExistsAsync("UM-invalid-role"), "Invalid role left a partially created user.");
        await RejectAsync<InvalidOperationException>(() => users.AddAsync(User("first"), role.Id));
        var concurrent = new[] { User("parallel-a"), User("parallel-b") };
        await Task.WhenAll(concurrent.Select(u => users.AddAsync(u, role.Id)));
        Check((await users.GetByIdAsync(concurrent[0].Id))!.SalesManager!.Id !=
              (await users.GetByIdAsync(concurrent[1].Id))!.SalesManager!.Id, "Simultaneous creation generated duplicate manager codes.");

        var grants = new TestPermissions(); grants.Keys.UnionWith(PermissionCatalog.All.Select(p => p.Key));
        grants.Keys.Remove(PermissionCatalog.CustomersViewAll);
        var session = new TestUserContext { UserId = first.Id, IsLoggedIn = true };
        var access = new CustomerAccessPolicy(grants, session);
        var customers = new CustomerService(new CustomerRepository(factory), factory, new PriceLevelRepository(factory),
            new SalesManagerRepository(factory), NullLogger<CustomerService>.Instance, new CustomerFieldPermissionPolicy(grants), access);
        Check((await customers.GetCustomersForEditorAsync()).Select(c => c.Id).SequenceEqual(["UM-OWN"]), "Manager scope must use the canonical customer assignment, not stale junctions.");
        Check((await customers.GetAllSalesManagersAsync()).Select(m => m.Id).SequenceEqual([managerId]), "Manager selector exposed other managers.");
        await RejectAsync<UnauthorizedAccessException>(() => customers.AddCustomerAsync(Customer("UM-FOREIGN", otherId)));
        await RejectAsync<UnauthorizedAccessException>(() => customers.DeleteCustomerAsync(new() { Id = "UM-FOREIGN" }));
        await RejectAsync<UnauthorizedAccessException>(() => customers.AddCustomerAsync(Customer("", otherId)));
        await RejectAsync<UnauthorizedAccessException>(() => customers.AddCustomerAsync(Customer("UM-OWN", otherId)));
        var ownEdit = Customer("UM-OWN", managerId); ownEdit.FullName = "Changed own client";
        await customers.AddCustomerAsync(ownEdit);
        Check((await customers.GetCustomersForEditorAsync()).Single().FullName == "Changed own client", "A manager could not edit an owned client.");
        var created = await customers.AddCustomerAsync(Customer("", managerId));
        Check(created != null && (await customers.GetCustomersForEditorAsync()).Any(c => c.Id == created.Id), "A manager could not create an owned client.");
        Check(await customers.DeleteCustomerAsync(created!), "A manager with Delete could not remove an owned empty client.");
        session.UserId = normal.Id;
        Check(!(await customers.GetCustomersForEditorAsync()).Any(), "Ordinary users without ViewAll must see an empty list.");
        grants.Keys.Add(PermissionCatalog.CustomersViewAll);
        Check((await customers.GetCustomersForEditorAsync()).Any(c => c.Id == "UM-FOREIGN"), "The separate ViewAll right must grant all clients to an ordinary user.");
        grants.Keys.Remove(PermissionCatalog.CustomersViewAll); session.UserId = first.Id;
        Check((await customers.GetCustomersForEditorAsync()).Count() == 1, "Revoking ViewAll must take effect without a new login.");
        renamed.IsManager = false; await users.UpdateAsync(renamed, role.Id);
        Check(!(await customers.GetCustomersForEditorAsync()).Any(), "A disabled manager must immediately lose scoped access.");
        var disabled = (await users.GetByIdAsync(first.Id))!;
        Check(disabled.SalesManager!.Id == managerId && !disabled.SalesManager.Active, "Clearing the flag must keep a stable inactive manager link.");
        disabled.IsManager = true; await users.UpdateAsync(disabled, role.Id);
        Check((await customers.GetCustomersForEditorAsync()).Single().Id == "UM-OWN", "Reactivating must restore the same assignments.");
        grants.Keys.Remove(PermissionCatalog.Customers);
        await RejectAsync<UnauthorizedAccessException>(() => customers.GetCustomersForEditorAsync());
        grants.Keys.Add(PermissionCatalog.Customers);
        session.IsLoggedIn = false;
        Check(!(await customers.GetCustomersForEditorAsync()).Any(), "A logged-out session cannot use its previous manager ID.");
        session.IsLoggedIn = true;
        await managers.SaveManagerCustomersAsync(managerId, [new() { CustomerId = "UM-FOREIGN", ManagerId = otherId }]);
        Check((await customers.GetCustomersForEditorAsync()).Count() == 2, "Assignment changes must be visible through a fresh context.");
        Check((await managers.GetManagerByIdAsync(managerId))!.FullName == "Renamed Test", "Assignment saves overwrote profile data.");
        var removable = User("removable"); await users.AddAsync(removable, role.Id);
        var removableId = (await users.GetByIdAsync(removable.Id))!.SalesManager!.Id;
        await users.DeleteAsync(removable.Id);
        await using (var db = createContext())
        {
            var preserved = await db.SalesManagers.SingleAsync(m => m.Id == removableId);
            Check(!preserved.Active && preserved.UserId == null, "Deleting a user must detach and preserve the inactive manager.");
            db.Orders.Add(new() { Id = "UM-ORDER", CustomerId = "UM-OWN", UserId = first.Id, SuminWords = "test" });
            await db.SaveChangesAsync();
        }
        await RejectAsync<InvalidOperationException>(() => users.DeleteAsync(first.Id));
        Check(await users.GetByIdAsync(first.Id) != null, "Deletion removed a user with sales history.");
        Console.WriteLine("PASS: user/manager atomic sync, automatic creation, concurrent creation, reactivation, contacts, fresh scope, separate ViewAll, foreign writes, assignment changes and history protection");
    }

    private static CustomerEntity Customer(string id, string? managerId) => new() { Id = id, FullName = string.IsNullOrEmpty(id) ? "New client" : id,
        Address = "Address", MobilePhone = "100", City = "City", Region = "Region", Territory = "Area", SalesManagerId = managerId, PriceLevelId = "UM-LEVEL" };
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task RejectAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new Exception("Expected rejection: " + typeof(T).Name);
    }
    private async Task CheckMigrationAsync()
    {
        await using var db = createContext();
        db.Users.Add(new() { UserName = "UM-migration", FirstName = "Old", LastName = "User", Password = "test-only" });
        db.SalesManagers.Add(new() { Id = "UM-MIGRATION", FullName = "Old manager", Address = "Old address", Contacts = "123" });
        db.Customers.Add(new() { Id = "UM-MIGRATION-CLIENT", FullName = "Old client", SalesManagerId = "UM-MIGRATION" });
        await db.SaveChangesAsync();
        var oldUserId = await db.Users.Where(u => u.UserName == "UM-migration").Select(u => u.Id).SingleAsync();
        db.ChangeTracker.Clear();
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var command in generator.Generate(new LinkUserManagers().DownOperations, db.GetService<IDesignTimeModel>().Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId]='20261110020000_LinkUserManagers'");
        await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        var oldUser = await db.Users.SingleAsync(u => u.Id == oldUserId);
        var oldManager = await db.SalesManagers.SingleAsync(m => m.Id == "UM-MIGRATION");
        Check(!oldUser.IsManager && oldUser.Address == null && oldManager.UserId == null && oldManager.Address == "Old address" &&
            (await db.Customers.SingleAsync(c => c.Id == "UM-MIGRATION-CLIENT")).SalesManagerId == "UM-MIGRATION",
            "Migration changed legacy identities, contacts, assignments or guessed a user link.");
        Console.WriteLine("PASS: user/manager migration upgrades existing data and is safe on repeated startup");
    }
}
