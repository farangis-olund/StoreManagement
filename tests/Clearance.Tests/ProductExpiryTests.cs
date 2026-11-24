using ClosedXML.Excel;
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

internal sealed class ProductExpiryTests
{
    private readonly string _name = "ProductExpiryTests_" + Guid.NewGuid().ToString("N");
    private const string Master = @"Server=(LocalDB)\MSSQLLocalDB;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15";
    private DatabaseContext Create() => new(new DbContextOptionsBuilder<DatabaseContext>().UseSqlServer(
        new SqlConnectionStringBuilder(Master) { InitialCatalog = _name }.ConnectionString).Options);
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
    private readonly Grants _grants = new();
    private static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
    private static async Task Throws<T>(Func<Task> work) where T : Exception
    {
        try { await work(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name);
    }
    public async Task<int> RunAsync(bool skipDatabase)
    {
        var created = false;
        try
        {
            await Access(); Excel(); Boundaries();
            await using (var db = Create())
            {
                var model = db.GetService<IDesignTimeModel>().Model;
                var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
                var diff = db.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot.GetRelationalModel(), model.GetRelationalModel());
                Check(diff.Count == 0, "Snapshot mismatch: " + string.Join(", ", diff.Select(x => x.GetType().Name)));
            }
            Console.WriteLine("PASS: expiry permissions, calendar-year boundaries, Excel headers/dates/leading zeros/errors/template and migration snapshot");
            if (skipDatabase) return 0;
            var dir = Path.Combine(AppContext.BaseDirectory, "test-data"); Directory.CreateDirectory(dir);
            await MasterSql($"CREATE DATABASE [{_name}] ON PRIMARY (NAME=N'{_name}', FILENAME=N'{Path.Combine(dir, _name + ".mdf").Replace("'", "''")}') LOG ON (NAME=N'{_name}_log', FILENAME=N'{Path.Combine(dir, _name + "_log.ldf").Replace("'", "''")}')");
            created = true;
            await Migrate(); await Workflow();
            Console.WriteLine("PASS: expiry SQL migration twice, multiple batches, availability, priority selection, concurrency, duplicate-safe import, live annotations, revoked writes and unchanged stock/prices");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            if (created) { SqlConnection.ClearAllPools(); await MasterSql($"ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}]"); }
        }
    }
    private async Task Access()
    {
        var calls = 0;
        var service = new ProductExpiryService(new Factory(() => { calls++; throw new Exception("Unexpected DB access"); }), _grants);
        foreach (var keys in new[] { Array.Empty<string>(), new[] { PermissionCatalog.Orders }, new[] { PermissionCatalog.InventoryCatalog } })
        {
            _grants.Keys = keys.ToHashSet();
            foreach (var work in new Func<Task>[] { async () => await service.GetProductsAsync(), async () => await service.GetBatchesAsync(false),
                () => service.SaveAsync(new(Guid.Empty, "A", DateTime.Today, DateTime.Today, "A1", 3)),
                () => service.DeleteAsync(Guid.NewGuid(), []), async () => await service.ImportAsync(new([], [])) })
                await Throws<UnauthorizedAccessException>(work);
            if (!keys.Contains(PermissionCatalog.Orders)) await Throws<UnauthorizedAccessException>(async () => await service.GetBatchesAsync(true));
        }
        Check(calls == 0, "Unauthorized operation reached the database");
        _grants.Keys = [PermissionCatalog.Inventory];
        foreach (var args in new[] { ("", DateTime.Today, DateTime.Today, "A"), ("A", DateTime.Today.AddDays(1), DateTime.Today, "A"), ("A", DateTime.Today, DateTime.Today, "") })
            await Throws<ArgumentException>(() => { ProductExpiryService.Validate(args.Item1, args.Item2, args.Item3, args.Item4, 0); return Task.CompletedTask; });
    }
    private static void Boundaries()
    {
        var today = new DateTime(2024, 2, 29);
        ExpiryBatchRow Row(DateTime expiry) => new(Guid.NewGuid(), "A", "Oil", 1, "B", 5, today.AddYears(-1), expiry, "A1", 5, [], today);
        Check(Row(today.AddYears(1)).IsNearExpiry && !Row(today.AddYears(1).AddDays(1)).IsNearExpiry, "Inclusive calendar year boundary failed");
        Check(Row(today).IsNearExpiry && !Row(today).IsExpired && Row(today.AddDays(-1)).IsExpired, "Today/expired boundary failed");
        Check(!(Row(today) with { Quantity = 0 }).IsNearExpiry, "Completed batch still warns");
        foreach (var day in new[] { today, new DateTime(2025, 1, 31), new DateTime(2026, 11, 24) })
        {
            ExpiryBatchRow At(DateTime expiry) => Row(expiry) with { AsOf = day };
            var month = day.AddMonths(1);
            Check(At(month.AddDays(-1)).IsCriticalExpiry && At(month.AddDays(-1)).Status == "Меньше месяца",
                "The day before the calendar-month boundary must have the critical warning");
            Check(!At(month).IsCriticalExpiry && At(month).Status == "Осталось не больше года" && !At(month.AddDays(1)).IsCriticalExpiry,
                "Exactly one calendar month must keep the year warning instead of the critical warning");
            Check(At(day).IsCriticalExpiry && At(day).Status == "Меньше месяца" && !At(day).IsExpired,
                "Expiry today must remain usable with the critical warning");
            Check(At(day.AddDays(-1)).IsCriticalExpiry && At(day.AddDays(-1)).Status == "Срок истёк",
                "Expired batches must retain their expired status with the critical warning");
            Check(!(At(day) with { Quantity = 0 }).IsCriticalExpiry &&
                (At(day.AddDays(-1)) with { Quantity = 0 }).Status == "Партия закончилась",
                "Completed batches must not show a critical expiry warning");
        }
    }
    private static void Excel()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "expiry-import-test.xlsx");
        try
        {
            using (var book = new XLWorkbook())
            {
                var s = book.AddWorksheet("Партии");
                s.Cell(1, 1).Value = "Бренд"; s.Cell(1, 2).Value = "Годен до"; s.Cell(1, 3).Value = "Артикул";
                s.Cell(1, 4).Value = "Место на складе"; s.Cell(1, 5).Value = "Дата производства"; s.Cell(1, 6).Value = "В наличии";
                s.Cell(2, 3).Value = 123; s.Cell(2, 3).Style.NumberFormat.Format = "000000";
                s.Cell(2, 2).Value = new DateTime(2027, 11, 24); s.Cell(2, 5).Value = "24.11.2025";
                s.Cell(2, 4).Value = "А-01"; s.Cell(2, 6).Value = 0;
                s.Cell(3, 3).Value = "000124"; s.Cell(3, 2).Value = "not a date"; s.Cell(3, 4).Value = "A1"; s.Cell(3, 5).Value = "2025-01-01";
                s.Cell(4, 3).Value = "A"; s.Cell(4, 2).Value = "2025-01-01"; s.Cell(4, 5).Value = "2026-01-01"; s.Cell(4, 4).Value = "A2";
                book.SaveAs(path);
            }
            var file = ProductExpiryExcel.Read(path);
            Check(file.Rows.Count == 1 && file.Rows[0].ArticleNumber == "000123" && file.Rows[0].Quantity == 0 && file.Rows[0].RowNumber == 2,
                "Excel reordered headers / formatted leading zeros / optional active failed");
            Check(file.Issues.Select(x => x.RowNumber).SequenceEqual([3, 4]), "Excel must report invalid rows with actual row numbers");
            using (var book = new XLWorkbook())
            {
                var s = book.AddWorksheet(); s.Cell(1, 1).Value = "0007"; s.Cell(1, 2).Value = 45000; s.Cell(1, 3).Value = "2027-12-31"; s.Cell(1, 4).Value = "A1"; s.Cell(1, 5).Value = 7; book.SaveAs(path);
            }
            file = ProductExpiryExcel.Read(path);
            Check(file.Rows.Count == 1 && file.Rows[0].Quantity == 7 && file.Rows[0].ManufacturedOn == DateTime.FromOADate(45000).Date, "Headerless/OA-date import failed");
            ProductExpiryExcel.SaveTemplate(path);
            file = ProductExpiryExcel.Read(path); Check(file.Rows.Count == 0 && file.Issues.Count == 0, "Blank template is not importable");
            using var template = new XLWorkbook(path);
            Check(template.Worksheet(1).Column(1).Style.NumberFormat.Format == "@", "Template does not preserve textual articles");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
    private async Task Migrate()
    {
        await using var db = Create(); await db.Database.EnsureCreatedAsync();
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var sql in generator.Generate(new AddExpiryBatchQuantities().DownOperations, db.GetService<IDesignTimeModel>().Model)) await db.Database.ExecuteSqlRawAsync(sql.CommandText);
        foreach (var sql in generator.Generate(new AddProductExpiryBatches().DownOperations, db.GetService<IDesignTimeModel>().Model)) await db.Database.ExecuteSqlRawAsync(sql.CommandText);
        var history = db.GetService<IHistoryRepository>(); await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
        foreach (var id in db.GetService<IMigrationsAssembly>().Migrations.Keys.Where(x => x != "20261124000000_AddProductExpiryBatches" && x != "20261124010000_AddExpiryBatchQuantities"))
            await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(id, "8.0.1")));
        await db.GetService<IMigrator>().MigrateAsync("20261124000000_AddProductExpiryBatches");
        var legacy = new ProductEntity { ArticleNumber = "LEGACY", ProductName = "Legacy oil", Quentity = 7,
            Brand = new() { BrandName = "Legacy brand" }, Group = new() { GroupName = "Legacy group" },
            WarehousePlace = "BASE", Model = "", Marka = "", Alternative = "" };
        db.Products.Add(legacy); await db.SaveChangesAsync();
        var legacyId = Guid.NewGuid(); var made = DateTime.Today.AddYears(-1); var expires = DateTime.Today.AddYears(1);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ProductExpiryBatches (Id, ArticleNumber, ManufacturedOn, ExpiresOn, WarehousePlace, IsActive, UpdatedAt) VALUES ({legacyId}, {legacy.ArticleNumber}, {made}, {expires}, {"L1"}, {true}, {DateTime.UtcNow})");
        await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        var upgraded = await db.ProductExpiryBatches.SingleAsync(x => x.Id == legacyId);
        Check(upgraded.Quantity == 0 && upgraded.ExpiresOn == expires && upgraded.WarehousePlace == "L1" &&
            (await db.Products.AsNoTracking().SingleAsync(x => x.ArticleNumber == "LEGACY")).Quentity == 7,
            "Upgrade must preserve legacy dates/places and keep unknown batch counts zero without changing article stock");
        db.ProductExpiryBatches.Remove(upgraded); db.Products.Remove(legacy); await db.SaveChangesAsync();
        Check(await db.ProductExpiryBatches.CountAsync() == 0, "Migration did not create an empty batch table");
    }
    private async Task Workflow()
    {
        var day = DateTime.Today; var made = day.AddYears(-1);
        await using (var db = Create())
        {
            var brand = new BrandEntity { BrandName = "Expiry" }; var group = new GroupEntity { GroupName = "Oil" };
            foreach (var article in new[] { "000123", "EXPIRED", "ZERO" }) db.Products.Add(new()
            {
                ArticleNumber = article, ProductName = "Oil " + article, Brand = brand, Group = group,
                Model = "", Marka = "", Alternative = "", WarehousePlace = "OLD", Quentity = article == "ZERO" ? 0 : 10, PriceLevel1 = 7
            });
            await db.SaveChangesAsync();
        }
        var catalog = new ProductExpiryCatalog(); var notifications = 0; catalog.Changed += (_, _) => notifications++;
        var service = new ProductExpiryService(new Factory(Create), _grants, catalog);
        await service.SaveAsync(new(Guid.Empty, "000123", made, day.AddYears(1), "A1", 3));
        await service.SaveAsync(new(Guid.Empty, "000123", made, day.AddYears(1).AddDays(1), "A2", 2));
        await service.SaveAsync(new(Guid.Empty, "000123", made, day.AddDays(-1), "OLD", 0));
        await service.SaveAsync(new(Guid.Empty, "EXPIRED", made, day.AddDays(-1), "E1", 2));
        await service.SaveAsync(new(Guid.Empty, "ZERO", made, day, "Z1", 0));
        var rows = await service.GetBatchesAsync(false, day);
        Check(rows.Count == 5 && rows.Count(x => x.ArticleNumber == "000123") == 3 && rows.Single(x => x.WarehousePlace == "OLD").IsActive == false,
            "Multiple batches or explicit false availability was lost");
        _grants.Keys = [PermissionCatalog.Orders];
        var due = await service.GetBatchesAsync(true, day);
        Check(due.Count == 2 && due[0].ArticleNumber == "EXPIRED" && due[1].WarehousePlace == "A1", "Priority list has wrong bounds, order or availability");
        _grants.Keys = [PermissionCatalog.Inventory];
        await Throws<InvalidOperationException>(() => service.SaveAsync(new(Guid.Empty, "000123", made, day.AddYears(1), "A1", 3)));
        var original = rows.Single(x => x.WarehousePlace == "A1");
        await service.SaveAsync(new(original.Id, "000123", made, original.ExpiresOn, "A1", 0, original.Version));
        await Throws<InvalidOperationException>(() => service.SaveAsync(new(original.Id, "000123", made, original.ExpiresOn, "A1", 3, original.Version)));
        await Throws<InvalidOperationException>(() => service.DeleteAsync(original.Id, original.Version));
        Check(catalog.Get("000123")!.ExpiresOn == day.AddYears(1).AddDays(1), "Completed batch still supplies warning");
        var result = await service.ImportAsync(new([
            new(2, "000123", made, day.AddYears(1), "A1", 3), new(3, "000123", made, day.AddYears(1), "A1", 0),
            new(4, "000123", made, day.AddDays(3), "A3", 2), new(5, "MISSING", made, day, "X1", 2), new(6, "000123", day.AddDays(1), day, "BAD", 2)
        ], []));
        Check(result.AddedCount == 1 && result.UpdatedCount == 1 && result.DuplicateCount == 1 && result.Issues.Count == 2, "Import counts/validation incorrect");
        Check(catalog.Get("000123")!.ExpiresOn == day.AddDays(3) && catalog.Get("000123")!.Hint.Contains("A3"), "Nearest batch annotation was not published");
        var repository = new Infrastructure.Repositories.ProductRepository(new Factory(Create));
        var product = await repository.GetOneAsync(x => x.ArticleNumber == "000123");
        Check(product?.NearestExpiryDate == day.AddDays(3) && ((Product)product!).ExpiryHint.Contains("A3"), "Single-product DTO lost expiry metadata");
        Check((await repository.GetAllAsync()).Single(x => x.ArticleNumber == "000123").NearestExpiryDate == day.AddDays(3), "Product catalog lost expiry metadata");
        Check((await repository.SearchProductsAsync("000123")).Single().NearestExpiryDate == day.AddDays(3), "Product search lost expiry metadata");
        rows = await service.GetBatchesAsync(false);
        Check(rows.Count == 6 && !rows.Single(x => x.WarehousePlace == "A1").IsActive, "Import duplicated a batch or ignored last occurrence");
        var batch = rows.Single(x => x.WarehousePlace == "A3"); await service.DeleteAsync(batch.Id, batch.Version);
        await Throws<InvalidOperationException>(() => service.DeleteAsync(batch.Id, batch.Version));
        var revoked = new ProductExpiryService(new Factory(() => { _grants.Keys.Clear(); return Create(); }), _grants);
        await Throws<UnauthorizedAccessException>(() => revoked.SaveAsync(new(Guid.Empty, "000123", made, day, "REVOKED", 2)));
        await using var verify = Create();
        Check(!await verify.ProductExpiryBatches.AnyAsync(x => x.WarehousePlace == "REVOKED"), "Revoked write was committed");
        Check(await verify.Products.AllAsync(x => x.PriceLevel1 == 7 && x.WarehousePlace == "OLD" && x.Quentity == (x.ArticleNumber == "ZERO" ? 0 : 10)), "Batch changes altered stock, prices or catalog location");
        Check(notifications == 8, "Live update notifications missing");
        _grants.Keys = [PermissionCatalog.Inventory, PermissionCatalog.OrdersBarter, PermissionCatalog.OrdersDirectFromStock];
        await ProductExpiryQuantityTests.RunAsync(Create, _grants);
    }
    private static async Task MasterSql(string sql)
    {
        await using var conn = new SqlConnection(Master); await conn.OpenAsync();
        await using var command = conn.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync();
    }
}
