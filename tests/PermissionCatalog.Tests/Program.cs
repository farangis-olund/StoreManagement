using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

return new PermissionCatalogTests().Run(args.Contains("--no-database"));

internal sealed class PermissionCatalogTests
{
    private readonly string _databaseName = $"PermissionCatalogTests_{Guid.NewGuid():N}";
    private readonly string _masterConnection = new SqlConnectionStringBuilder
    {
        DataSource = @"(LocalDB)\MSSQLLocalDB",
        InitialCatalog = "master",
        IntegratedSecurity = true,
        TrustServerCertificate = true
    }.ConnectionString;

    private PermissionTestContext CreateContext()
    {
        var connection = new SqlConnectionStringBuilder(_masterConnection) { InitialCatalog = _databaseName };
        var options = new DbContextOptionsBuilder<PermissionTestContext>()
            .UseSqlServer(connection.ConnectionString)
            .Options;
        return new PermissionTestContext(options);
    }

    public int Run(bool skipDatabase = false)
    {
        var created = false;
        try
        {
            CatalogHasUniqueKeys();
            Console.WriteLine("PASS: catalogue keys and constants agree");
            CustomerPermissionTests.CheckPolicy();
            CustomerDeletionTests.CheckAuthorization().GetAwaiter().GetResult();
            AdminPermissionTests.CheckPolicyAsync().GetAwaiter().GetResult();
            RetailManagerTests.CheckAuthorizationAsync().GetAwaiter().GetResult();
            OrderPermissionTests.RunAsync().GetAwaiter().GetResult();
            AvailableRestrictionTests.CheckCalculation();
            OrderAllowanceTests.Run();
            UnpaidDeliveryTests.CheckDisplay();
            if (skipDatabase) return 0;

            // All database files stay in this test project's ignored build output directory.
            var directory = Path.Combine(AppContext.BaseDirectory, "test-data");
            Directory.CreateDirectory(directory);
            var mdf = Path.Combine(directory, _databaseName + ".mdf").Replace("'", "''");
            var ldf = Path.Combine(directory, _databaseName + "_log.ldf").Replace("'", "''");
            ExecuteMaster($"CREATE DATABASE [{_databaseName}] ON PRIMARY " +
                $"(NAME = N'{_databaseName}', FILENAME = N'{mdf}') " +
                $"LOG ON (NAME = N'{_databaseName}_log', FILENAME = N'{ldf}')");
            created = true;

            using (var context = CreateCustomerContext())
                context.Database.EnsureCreated();

            new DeliveryPaymentMigrationTests(CreateCustomerContext).RunAsync().GetAwaiter().GetResult();
            new PaymentReceiptNumberTests(CreateCustomerContext).RunAsync().GetAwaiter().GetResult();

            var tests = new (string Name, Action Run)[]
            {
                ("empty database receives the catalogue, without granting access", EmptyDatabase),
                ("repeated startup keeps IDs and row counts", RepeatedStartup),
                ("upgrade preserves role assignments and unknown keys", Upgrade),
                ("legacy key casing and duplicate assignments are preserved", LegacyKeys),
                ("simultaneous startups do not duplicate permissions", ConcurrentStartup),
                ("failed synchronization rolls back all changes", FailedSynchronization)
            };

            foreach (var test in tests)
            {
                Reset();
                test.Run();
                Console.WriteLine($"PASS: {test.Name}");
            }

            Console.WriteLine("All 7 permission catalogue checks passed.");
            new CustomerPermissionTests(CreateCustomerContext).CheckServices().GetAwaiter().GetResult();
            CustomerDeletionTests.RunAsync(CreateCustomerContext).GetAwaiter().GetResult();
            new AdminPermissionTests(CreateCustomerContext).CheckMigrationAsync().GetAwaiter().GetResult();
            new UserManagerTests(CreateCustomerContext).RunAsync().GetAwaiter().GetResult();
            new RetailManagerTests(CreateCustomerContext).RunAsync().GetAwaiter().GetResult();
            CheckPermissionReloadAsync().GetAwaiter().GetResult();
            new AvailableRestrictionTests(CreateCustomerContext).RunAsync().GetAwaiter().GetResult();
            new UnpaidDeliveryTests(CreateCustomerContext).RunAsync().GetAwaiter().GetResult();
            new CleanServiceTests(CreateCustomerContext).RunAsync().GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            if (created)
            {
                // This generated name never points at the application's database.
                SqlConnection.ClearAllPools();
                ExecuteMaster($"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                    $"DROP DATABASE [{_databaseName}]");
            }
        }
    }

    private async Task CheckPermissionReloadAsync()
    {
        var repository = new Infrastructure.Repositories.RolePermissionRepository(new TestContextFactory(CreateCustomerContext));
        await using var tracked = CreateCustomerContext();
        var user = new UserEntity { UserName = "permission-test", FirstName = "Test", LastName = "User", Password = "test-only" };
        var role = new RoleEntity { RoleName = "Initial", Description = "Test" };
        var other = new RoleEntity { RoleName = "Replacement", Description = "Test" };
        var view = new PermissionEntity { Key = "Test.View", Description = "View" };
        var edit = new PermissionEntity { Key = "Test.Edit", Description = "Edit" };
        tracked.UserRoles.Add(new UserRoleEntity { User = user, Role = role });
        tracked.Roles.Add(other);
        tracked.RolePermissions.Add(new RolePermissionEntity { Role = role, Permission = view });
        tracked.Permissions.Add(edit);
        await tracked.SaveChangesAsync();
        Assert((await repository.GetPermissionKeysForUserAsync(user.Id)).SequenceEqual(new[] { view.Key }), "Initial grants missing.");
        await repository.AddPermissionsAsync(role.Id, new[] { edit.Id });
        Assert((await repository.GetPermissionKeysForUserAsync(user.Id)).Contains(edit.Key), "New grant requires a new login.");
        await repository.ClearPermissionsAsync(role.Id);
        Assert(!(await repository.GetPermissionKeysForUserAsync(user.Id)).Any(), "Revoked grants remained cached.");
        await repository.AddPermissionsAsync(other.Id, new[] { edit.Id });
        await using (var db = CreateCustomerContext())
        {
            await db.UserRoles.Where(r => r.UserId == user.Id).ExecuteDeleteAsync();
            db.UserRoles.Add(new UserRoleEntity { UserId = user.Id, RoleId = other.Id });
            await db.SaveChangesAsync();
        }
        Assert(user.UserRoles.Single().RoleId == role.Id, "Test must retain an old tracked role.");
        Assert((await repository.GetPermissionKeysForUserAsync(user.Id)).SequenceEqual(new[] { edit.Key }),
            "Reload used an old tracked user-role relationship.");
        Assert(!(await repository.GetPermissionKeysForUserAsync(-1)).Any(), "Unknown users must have no grants.");
        Console.WriteLine("PASS: fresh permission reload sees grants, revocations, and reassigned roles despite stale tracked entities");
    }

    private void ExecuteMaster(string sql)
    {
        using var connection = new SqlConnection(_masterConnection);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private DatabaseContext CreateCustomerContext()
    {
        var connection = new SqlConnectionStringBuilder(_masterConnection) { InitialCatalog = _databaseName };
        return new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlServer(connection.ConnectionString).Options);
    }

    private static void CatalogHasUniqueKeys()
    {
        var definitions = PermissionCatalog.All;
        Assert(definitions.All(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Description)),
            "Every permission needs a key and description.");
        var keys = definitions.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert(keys.Count == definitions.Count, "Duplicate catalogue keys.");
        var constants = typeof(PermissionCatalog).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);
        Assert(CustomerPermissionCatalog.Fields.Count == 10 && CustomerPermissionCatalog.All.Count == 40,
            "Each of the ten customer field groups needs four permissions.");
        Assert(keys.SetEquals(constants.Concat(CustomerPermissionCatalog.All.Select(p => p.Key))),
            "Every constant and field operation must have exactly one catalogue entry.");
    }

    private void Reset()
    {
        using var context = CreateContext();
        context.RolePermissions.ExecuteDelete();
        context.Roles.ExecuteDelete();
        context.Permissions.ExecuteDelete();
    }

    private void Synchronize()
    {
        using var context = CreateContext();
        PermissionCatalogSynchronizer.Synchronize(context);
    }

    private void EmptyDatabase()
    {
        Synchronize();
        using var context = CreateContext();
        var permissions = context.Permissions.ToDictionary(p => p.Key);
        Assert(permissions.Count == PermissionCatalog.All.Count, "Missing or extra permissions.");
        foreach (var definition in PermissionCatalog.All)
            Assert(permissions[definition.Key].Description == definition.Description, "Incorrect description.");
        Assert(!context.Roles.Any() && !context.RolePermissions.Any(), "Synchronization must not grant access.");
    }

    private void RepeatedStartup()
    {
        Synchronize();
        using var before = CreateContext();
        var ids = before.Permissions.ToDictionary(p => p.Key, p => p.Id);
        Synchronize();
        using var after = CreateContext();
        var permissions = after.Permissions.ToList();
        Assert(permissions.Count == ids.Count && permissions.All(p => ids[p.Key] == p.Id), "IDs or row count changed.");
    }

    private void Upgrade()
    {
        using var before = CreateContext();
        var orders = new PermissionEntity { Key = PermissionCatalog.Orders, Description = "Old description" };
        var unknown = new PermissionEntity { Key = "Future.Permission", Description = "Keep this" };
        var role = new RoleEntity { RoleName = "Test role", Description = "Keep role" };
        before.RolePermissions.AddRange(
            new RolePermissionEntity { Role = role, Permission = orders },
            new RolePermissionEntity { Role = role, Permission = unknown });
        before.SaveChanges();
        var grantIds = before.RolePermissions.Select(p => p.Id).OrderBy(id => id).ToArray();

        Synchronize();

        using var after = CreateContext();
        Assert(after.Permissions.Count() == PermissionCatalog.All.Count + 1, "Missing upgrade entries.");
        Assert(after.Permissions.Single(p => p.Id == orders.Id).Description ==
            PermissionCatalog.All.Single(p => p.Key == PermissionCatalog.Orders).Description, "Description not updated.");
        Assert(after.Permissions.Single(p => p.Id == unknown.Id).Description == "Keep this", "Unknown permission changed.");
        Assert(after.Roles.Single().Id == role.Id, "Role changed.");
        Assert(after.RolePermissions.OrderBy(p => p.Id).Select(p => p.Id).ToArray().SequenceEqual(grantIds), "Grants changed.");
        Assert(after.RolePermissions.Any(p => p.RoleId == role.Id && p.PermissionId == orders.Id) &&
            after.RolePermissions.Any(p => p.RoleId == role.Id && p.PermissionId == unknown.Id), "Assignments changed.");
    }

    private void LegacyKeys()
    {
        using var before = CreateContext();
        var legacy = new PermissionEntity { Key = "onlyOfficialCustomer", Description = "Legacy" };
        var duplicate = new PermissionEntity { Key = PermissionCatalog.OnlyOfficialCustomer, Description = "Duplicate" };
        var role = new RoleEntity { RoleName = "Restricted role", Description = "Keep restriction" };
        before.RolePermissions.AddRange(
            new RolePermissionEntity { Role = role, Permission = legacy },
            new RolePermissionEntity { Role = role, Permission = duplicate });
        before.SaveChanges();

        Synchronize();
        Synchronize();

        using var after = CreateContext();
        Assert(after.Permissions.Count() == PermissionCatalog.All.Count + 1, "Legacy rows were duplicated or deleted.");
        Assert(after.Permissions.Single(p => p.Id == legacy.Id).Key == PermissionCatalog.OnlyOfficialCustomer, "Key not normalized.");
        Assert(after.RolePermissions.Any(p => p.PermissionId == legacy.Id) &&
            after.RolePermissions.Any(p => p.PermissionId == duplicate.Id), "Legacy assignments lost.");
    }

    private void ConcurrentStartup()
    {
        using var start = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            start.Wait();
            Synchronize();
        })).ToArray();
        start.Set();
        Task.WaitAll(tasks);

        using var context = CreateContext();
        var keys = context.Permissions.Select(p => p.Key).ToList();
        Assert(keys.Count == PermissionCatalog.All.Count && keys.Distinct().Count() == keys.Count,
            "Concurrent startup created duplicate or missing entries.");
    }

    private void FailedSynchronization()
    {
        using (var context = CreateContext())
        {
            context.Permissions.Add(new PermissionEntity { Key = PermissionCatalog.Orders, Description = "Original" });
            context.SaveChanges();
            context.Database.ExecuteSqlRaw("ALTER TABLE [dbo].[Permissions] ADD CONSTRAINT [RejectReports] CHECK ([Key] <> N'Reports')");
        }

        var failed = false;
        try
        {
            Synchronize();
        }
        catch (DbUpdateException)
        {
            failed = true;
        }
        finally
        {
            using var cleanup = CreateContext();
            cleanup.Database.ExecuteSqlRaw("ALTER TABLE [dbo].[Permissions] DROP CONSTRAINT [RejectReports]");
        }

        using var after = CreateContext();
        Assert(failed, "The forced write failure did not occur.");
        Assert(after.Permissions.Count() == 1 && after.Permissions.Single().Description == "Original", "Partial changes were committed.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

internal sealed class PermissionTestContext(DbContextOptions<PermissionTestContext> options) : DbContext(options)
{
    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // This catalogue-only context intentionally excludes the application's relationship graph.
        foreach (var type in typeof(UserEntity).Assembly.GetTypes().Where(t => t.Namespace == typeof(UserEntity).Namespace &&
            t != typeof(PermissionEntity) && t != typeof(RoleEntity) && t != typeof(RolePermissionEntity)))
            modelBuilder.Ignore(type);
        modelBuilder.Entity<PermissionEntity>().ToTable("Permissions", "dbo");
        modelBuilder.Entity<RoleEntity>().Ignore(p => p.UserRoles);
    }
}
