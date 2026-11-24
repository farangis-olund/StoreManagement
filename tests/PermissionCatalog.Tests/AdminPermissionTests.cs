using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Security;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

internal sealed class AdminPermissionTests(Func<DatabaseContext> createContext)
{
    public static async Task CheckPolicyAsync()
    {
        var grants = new TestPermissions();
        var policy = new AdminAccessPolicy(grants);
        foreach (var permission in PermissionCatalog.All.Where(p => p.Key.StartsWith("Admin.")))
        {
            grants.Keys.Clear(); grants.Keys.Add(permission.Key);
            Check(!policy.CanOpen(permission.Key), "A tab right cannot bypass the Admin module.");
            grants.Keys.Add(PermissionCatalog.Admin);
            if (permission.Key.StartsWith("Admin.References."))
            {
                Check(!policy.CanOpen(permission.Key), "A nested right cannot bypass References.");
                grants.Keys.Add(PermissionCatalog.AdminReferences);
            }
            Check(policy.CanOpen(permission.Key), "Matching module and tab rights must allow navigation.");
            grants.Keys.Remove(permission.Key);
            Check(!policy.CanOpen(permission.Key), "Admin must not bypass a missing tab right.");
        }
        var calls = 0;
        var factory = new TestContextFactory(() => { calls++; throw new Exception("Database boundary"); });
        var cleanup = new CleanService(factory, policy);
        foreach (var rights in new[] { Array.Empty<string>(), new[] { PermissionCatalog.Admin }, new[] { PermissionCatalog.AdminCleanup } })
        {
            grants.Keys.Clear(); grants.Keys.UnionWith(rights);
            try { await cleanup.DeleteAllAsync(); throw new Exception("Unauthorized cleanup was allowed."); }
            catch (UnauthorizedAccessException) { }
        }
        Check(calls == 0, "Unauthorized cleanup reached the database.");
        Console.WriteLine("PASS: all administration tab rights, required parent modules and cleanup rejection before database access");
    }

    public async Task CheckMigrationAsync()
    {
        await using var db = createContext();
        // Reconstruct an old installation: Admin was assigned, while Brand and an already known
        // granular permission were explicitly left unchecked. Only newly introduced keys may inherit access.
        var admin = await db.Permissions.FirstOrDefaultAsync(p => p.Key == PermissionCatalog.Admin);
        if (admin == null) { admin = new() { Key = PermissionCatalog.Admin, Description = "Admin" }; db.Permissions.Add(admin); }
        var brand = await db.Permissions.FirstOrDefaultAsync(p => p.Key == PermissionCatalog.AdminBrand);
        if (brand == null) { brand = new() { Key = PermissionCatalog.AdminBrand, Description = "Brand" }; db.Permissions.Add(brand); }
        var bonus = await db.Permissions.FirstOrDefaultAsync(p => p.Key == PermissionCatalog.AdminBonuses);
        if (bonus == null) { bonus = new() { Key = PermissionCatalog.AdminBonuses, Description = "Bonus" }; db.Permissions.Add(bonus); }
        var role = new RoleEntity { RoleName = "ADMIN-MIGRATION", Description = "Test" };
        var ordinary = new RoleEntity { RoleName = "ADMIN-MIGRATION-ORDINARY", Description = "Test" };
        db.Roles.Add(ordinary);
        db.RolePermissions.Add(new() { Role = role, Permission = admin });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId]='20261110030000_AddAdminTabPermissions'");
        await db.Database.MigrateAsync();
        var introduced = PermissionCatalog.All.Where(p => p.Key.StartsWith("Admin.") && p.Key != PermissionCatalog.AdminBrand && p.Key != PermissionCatalog.AdminBonuses && p.Key != PermissionCatalog.AdminClearance)
            .Select(p => p.Key).ToHashSet();
        var granted = await db.RolePermissions.Where(p => p.RoleId == role.Id).Select(p => p.Permission.Key).ToListAsync();
        Check(introduced.All(granted.Contains), "Upgrade lost previously available administration sections, including access to role settings.");
        Check(!granted.Contains(PermissionCatalog.AdminBrand) && !granted.Contains(PermissionCatalog.AdminBonuses), "Upgrade restored an explicitly unchecked permission.");
        Check(!await db.RolePermissions.AnyAsync(p => p.RoleId == ordinary.Id), "Upgrade granted admin access to an ordinary role.");
        await db.RolePermissions.Where(p => p.RoleId == role.Id && p.Permission.Key == PermissionCatalog.AdminManagers).ExecuteDeleteAsync();
        PermissionCatalogSynchronizer.Synchronize(db);
        var count = await db.Permissions.CountAsync();
        await db.Database.MigrateAsync();
        PermissionCatalogSynchronizer.Synchronize(db);
        Check(!await db.RolePermissions.AnyAsync(p => p.RoleId == role.Id && p.Permission.Key == PermissionCatalog.AdminManagers),
            "A later startup restored a revoked administrator tab.");
        Check(count == await db.Permissions.CountAsync(), "Repeated migration or catalogue sync duplicated permissions.");
        Console.WriteLine("PASS: administration upgrade preserves access once, respects unchecked Brand/existing rights, excludes ordinary roles and never restores revoked grants");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
