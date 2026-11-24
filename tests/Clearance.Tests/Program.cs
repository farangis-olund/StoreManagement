using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Migrations;
using Infrastructure.Security;
using Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

return args.Contains("--expiry") ? await new ProductExpiryTests().RunAsync(args.Contains("--no-database"))
    : await new ClearanceTests().RunAsync(args.Contains("--no-database"));

internal sealed class ClearanceTests
{
    private readonly string _name = $"ClearanceTests_{Guid.NewGuid():N}";
    private readonly string _master = @"Server=(LocalDB)\MSSQLLocalDB;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15";
    private DatabaseContext Create() => new(new DbContextOptionsBuilder<DatabaseContext>().UseSqlServer(
        new SqlConnectionStringBuilder(_master) { InitialCatalog = _name }.ConnectionString).Options);
    private readonly Grants _grants = new();

    public async Task<int> RunAsync(bool skipDatabase)
    {
        var created = false;
        try
        {
            await CheckAccessAsync();
            await using (var db = Create())
            {
                var model = db.GetService<IDesignTimeModel>().Model;
                var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(
                    db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
                var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot.GetRelationalModel(), model.GetRelationalModel());
                Check(differences.Count == 0, "Migration snapshot differs: " + string.Join(", ", differences.Select(d => d.GetType().Name)));
            }
            Console.WriteLine("PASS: clearance permissions, input validation and model snapshot");
            if (skipDatabase) return 0;
            var directory = Path.Combine(AppContext.BaseDirectory, "test-data"); Directory.CreateDirectory(directory);
            var mdf = Path.Combine(directory, _name + ".mdf").Replace("'", "''");
            var ldf = Path.Combine(directory, _name + "_log.ldf").Replace("'", "''");
            await MasterAsync($"CREATE DATABASE [{_name}] ON PRIMARY (NAME=N'{_name}', FILENAME=N'{mdf}') LOG ON (NAME=N'{_name}_log', FILENAME=N'{ldf}')");
            created = true;
            await CheckMigrationAsync();
            await CheckWorkflowAsync();
            Console.WriteLine("PASS: migration twice, one-time grants, lifetime selection, atomic replacement, empty list, period edges, per-seller bonus, rounding, exact snapshots and duplicate protection");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            if (created)
            {
                SqlConnection.ClearAllPools();
                await MasterAsync($"ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]");
            }
        }
    }

    private async Task CheckAccessAsync()
    {
        var calls = 0;
        var service = new ClearanceService(new Factory(() => { calls++; throw new Exception("Database boundary"); }), new AdminAccessPolicy(_grants));
        foreach (var keys in new[] { Array.Empty<string>(), new[] { PermissionCatalog.Admin }, new[] { PermissionCatalog.AdminClearance },
            new[] { PermissionCatalog.Admin, PermissionCatalog.AdminBonuses } })
        {
            _grants.Keys = keys.ToHashSet();
            foreach (var work in new Func<Task>[] { async () => await service.GetArticleNumbersAsync(), async () => await service.GetProductsAsync(), async () => await service.GenerateProductsAsync(0),
                async () => await service.FindProductAsync("A"), () => service.SaveProductsAsync([]),
                async () => await service.CalculateBonusAsync(DateTime.Today, DateTime.Today, 1),
                () => service.SaveReportAsync(new(Guid.NewGuid(), DateTime.Today, DateTime.Today, 1, DateTime.Now, [])),
                async () => await service.GetHistoryAsync(), async () => await service.GetReportAsync(Guid.NewGuid()) })
                await ThrowsAsync<UnauthorizedAccessException>(work);
        }
        Check(calls == 0, "Unauthorized operation touched a database.");
        foreach (var percent in new[] { -1m, 0m, 100.01m, 1.001m })
            await ThrowsAsync<ArgumentException>(() => { ClearanceService.ValidatePeriod(DateTime.Today, DateTime.Today, percent); return Task.CompletedTask; });
        await ThrowsAsync<ArgumentException>(() => { ClearanceService.ValidatePeriod(DateTime.Today.AddDays(1), DateTime.Today, 1); return Task.CompletedTask; });
        _grants.Keys = [PermissionCatalog.Admin, PermissionCatalog.AdminClearance];
    }

    private async Task CheckMigrationAsync()
    {
        await using var db = Create();
        await db.Database.EnsureCreatedAsync();
        var admin = new PermissionEntity { Key = PermissionCatalog.Admin, Description = "Admin" };
        var role = new RoleEntity { RoleName = "Administrator", Description = "Test" };
        var ordinary = new RoleEntity { RoleName = "Ordinary", Description = "Test" };
        db.Roles.Add(ordinary); db.RolePermissions.Add(new() { Role = role, Permission = admin });
        await db.SaveChangesAsync();
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var sql in generator.Generate(new AddClearance().DownOperations, db.GetService<IDesignTimeModel>().Model))
            await db.Database.ExecuteSqlRawAsync(sql.CommandText);
        var history = db.GetService<IHistoryRepository>();
        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
        foreach (var id in db.GetService<IMigrationsAssembly>().Migrations.Keys.Where(x => x != "20261123000000_AddClearance"))
            await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(id, "8.0.1")));
        await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        Check(await db.RolePermissions.CountAsync(x => x.RoleId == role.Id && x.Permission.Key == PermissionCatalog.AdminClearance) == 1,
            "Migration must grant the new tab once to existing admin roles.");
        Check(!await db.RolePermissions.AnyAsync(x => x.RoleId == ordinary.Id), "Migration granted a non-admin role.");
        await db.RolePermissions.Where(x => x.Permission.Key == PermissionCatalog.AdminClearance).ExecuteDeleteAsync();
        await db.Database.MigrateAsync(); PermissionCatalogSynchronizer.Synchronize(db);
        Check(!await db.RolePermissions.AnyAsync(x => x.Permission.Key == PermissionCatalog.AdminClearance), "Startup restored a revoked grant.");
    }

    private async Task CheckWorkflowAsync()
    {
        await using var db = Create();
        var brand = new BrandEntity { BrandName = "Clearance" };
        var group = new GroupEntity { GroupName = "Clearance" };
        ProductEntity Product(string article, int stock) => new()
        {
            ArticleNumber = article, ProductName = "Test " + article, Brand = brand, Group = group,
            Quentity = stock, Model = "", Marka = "", Alternative = "", WarehousePlace = ""
        };
        var never = Product("0001", 5); var slow = Product("A'SLOW", 10); var fast = Product("FAST", 10); var empty = Product("EMPTY", 0);
        db.Products.AddRange(never, slow, fast, empty);
        var userA = new UserEntity { UserName = "seller-a", FirstName = "Иван", LastName = "Иванов", Password = "test" };
        var userB = new UserEntity { UserName = "seller-b", FirstName = "Иван", LastName = "Иванов", Password = "test" };
        db.Users.AddRange(userA, userB); await db.SaveChangesAsync();
        var day = new DateTime(2026, 10, 15);
        OrderEntity Sale(string id, UserEntity user, DateTime date, ProductEntity product, int qty, decimal price) => new()
        {
            Id = id, Date = date, UserId = user.Id, SuminWords = "", Rate = 1,
            OrderDetails = [new() { ArticleNumber = product.ArticleNumber, Quentity = qty, ReturnedQty = qty, Price = price }]
        };
        db.Orders.AddRange(Sale("PRE", userA, day.AddTicks(-1), slow, 1, 2),
            Sale("FIRST", userA, day, slow, 1, 10.01m), Sale("LAST", userB, day.AddDays(1).AddTicks(-1), slow, 1, 10.20m),
            Sale("NEXT", userA, day.AddDays(1), slow, 1, 2), Sale("FAST", userA, day, fast, 50, 10));
        await db.SaveChangesAsync();
        var catalog = new ClearanceCatalog(); var changes = 0; catalog.Changed += (_, _) => changes++;
        var service = new ClearanceService(new Factory(Create), new AdminAccessPolicy(_grants), catalog);
        var zero = await service.GenerateProductsAsync(0);
        Check(zero.Count == 1 && zero[0].ArticleNumber == never.ArticleNumber, "Never-sold selection includes wrong stock/history.");
        var selected = await service.GenerateProductsAsync(4);
        Check(selected.Count == 2 && selected.Single(x => x.ArticleNumber == slow.ArticleNumber).SoldQuantity == 4, "Lifetime threshold is incorrect.");
        Check((await service.GetProductsAsync()).Count == 0, "Generating must not change saved data.");
        await service.SaveProductsAsync([slow.ArticleNumber, never.ArticleNumber]);
        Check(changes == 1 && catalog.Contains(slow.ArticleNumber), "Saving must publish the new list.");
        var products = (await new Infrastructure.Repositories.ProductRepository(new Factory(Create)).GetAllAsync()).ToArray();
        Check(products.Single(x => x.ArticleNumber == slow.ArticleNumber).IsClearance && !products.Single(x => x.ArticleNumber == fast.ArticleNumber).IsClearance,
            "Order catalogue does not expose the saved clearance flags.");
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER [RejectClearance] ON [ClearanceProducts] AFTER INSERT AS BEGIN THROW 51000, 'Test failure', 1; END;");
        try { await ThrowsAsync<DbUpdateException>(() => service.SaveProductsAsync([fast.ArticleNumber])); }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER [RejectClearance]"); }
        Check((await service.GetProductsAsync()).Count == 2 && changes == 1, "A database failure did not roll back the complete replacement.");
        await ThrowsAsync<InvalidOperationException>(() => service.SaveProductsAsync(["MISSING"]));
        await ThrowsAsync<ArgumentException>(() => service.SaveProductsAsync(["0001", "0001"]));
        Check((await service.GetProductsAsync()).Count == 2, "Failed replacement cleared the saved list.");
        Check((await service.FindProductAsync(" a'slow ")).ArticleNumber == slow.ArticleNumber, "Article lookup must handle quotes and case safely.");
        var report = await service.CalculateBonusAsync(day, day, 2.5m);
        Check(report.Rows.Count == 2 && report.Rows.Select(r => r.UserId).Distinct().Count() == 2,
            "Two users with the same name must remain separate.");
        Check(report.TotalQuantity == 2 && report.TotalAmount == 20.21m && report.TotalBonus == .51m && report.Rows.Single(x => x.UserId == userB.Id).Bonus == .26m,
            "Date boundary or percent/rounding is incorrect.");
        await db.OrderDetails.Where(x => x.OrderId == "FIRST").ExecuteUpdateAsync(s => s.SetProperty(x => x.Price, 999m));
        await service.SaveProductsAsync([]);
        Check((await service.GetProductsAsync()).Count == 0 && (await service.CalculateBonusAsync(day, day, 2.5m)).Rows.Count == 0,
            "Clearing all articles must be supported.");
        await service.SaveReportAsync(report); await service.SaveReportAsync(report);
        var read = await service.GetReportAsync(report.Id);
        Check(read.Rows.SequenceEqual(report.Rows) && read.TotalAmount == 20.21m && read.TotalBonus == .51m,
            "Saving recalculated instead of preserving the displayed report.");
        Check((await service.GetHistoryAsync()).Count == 1, "Retry duplicated the report.");
        await ThrowsAsync<InvalidOperationException>(() => service.SaveReportAsync(report with { Id = Guid.NewGuid() }));
        var revoked = new ClearanceService(new Factory(() => { _grants.Keys.Clear(); return Create(); }), new AdminAccessPolicy(_grants));
        await ThrowsAsync<UnauthorizedAccessException>(async () => await revoked.GetProductsAsync());
    }
    private async Task MasterAsync(string sql)
    {
        await using var conn = new SqlConnection(_master); await conn.OpenAsync();
        await using var command = conn.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync();
    }
    private static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
    private static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    private sealed class Grants : IPermissionChecker
    {
        public HashSet<string> Keys = [];
        public bool Has(string key) => Keys.Contains(key);
    }
    private sealed class Factory(Func<DatabaseContext> create) : IDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext() => create();
        public Task<DatabaseContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(create());
    }
}
