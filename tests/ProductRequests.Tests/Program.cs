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
using PresentationWpf.Dtos;

return await new ProductRequestTests().RunAsync(args.Contains("--no-database"));

internal sealed class ProductRequestTests
{
    private readonly string _databaseName = $"ProductRequestTests_{Guid.NewGuid():N}";
    private readonly string _master = new SqlConnectionStringBuilder
    {
        DataSource = @"(LocalDB)\MSSQLLocalDB", InitialCatalog = "master",
        IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 15
    }.ConnectionString;
    private readonly TestPermissions _permissions = new();
    private int _userId;
    private int _otherUserId;
    private int _brandId;
    private int _groupId;

    private DatabaseContext CreateContext() => new(new DbContextOptionsBuilder<DatabaseContext>()
        .UseSqlServer(new SqlConnectionStringBuilder(_master) { InitialCatalog = _databaseName }.ConnectionString).Options);

    public async Task<int> RunAsync(bool skipDatabase)
    {
        var created = false;
        try
        {
            CheckQuantityCapture();
            CheckGrouping();
            await CheckPermissionRulesAsync();
            await using (var db = CreateContext())
            {
                var model = db.GetService<IDesignTimeModel>().Model;
                var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(
                    db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
                var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot.GetRelationalModel(), model.GetRelationalModel());
                Check(differences.Count == 0, "Model/snapshot mismatch: " + string.Join(", ", differences.Select(x => x.GetType().Name)));
                Check(db.Model.FindEntityType(typeof(ProductEntity))!.GetDeclaredTriggers().Any(), "Product trigger metadata missing.");
            }
            Console.WriteLine("PASS: cart shortage capture, aggregation and migration snapshot");
            if (skipDatabase) return 0;

            var directory = Path.Combine(AppContext.BaseDirectory, "test-data");
            Directory.CreateDirectory(directory);
            var mdf = Path.Combine(directory, _databaseName + ".mdf").Replace("'", "''");
            var ldf = Path.Combine(directory, _databaseName + "_log.ldf").Replace("'", "''");
            await ExecuteMasterAsync($"CREATE DATABASE [{_databaseName}] ON PRIMARY " +
                $"(NAME = N'{_databaseName}', FILENAME = N'{mdf}') LOG ON " +
                $"(NAME = N'{_databaseName}_log', FILENAME = N'{ldf}')");
            created = true;
            await using (var db = CreateContext())
            {
                await db.Database.EnsureCreatedAsync();
                // Upgrade the preceding schema using the actual startup migrator, twice.
                var generator = db.GetService<IMigrationsSqlGenerator>();
                foreach (var command in generator.Generate(new AddProductRequests().DownOperations,
                    db.GetService<IDesignTimeModel>().Model))
                    await db.Database.ExecuteSqlRawAsync(command.CommandText);
                var history = db.GetService<IHistoryRepository>();
                await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
                foreach (var id in db.GetService<IMigrationsAssembly>().Migrations.Keys.Where(x => x != "20261102000000_AddProductRequests"))
                    await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(id, "8.0.1")));
                await db.Database.MigrateAsync();
                await db.Database.MigrateAsync();
            }
            await SeedAsync();
            var service = new ProductRequestService(new TestFactory(CreateContext), _permissions);
            await CheckCreationAsync(service);
            await CheckDraftsAsync(service);
            await CheckFiltersAndActionsAsync(service);
            await CheckBulkDeletionAsync(service);
            await CheckBulkProcessingAsync(service);
            await CheckTriggerAsync(service);
            Console.WriteLine("PASS: migration, all three request types, ownership, retries, filters, bulk deletion/processing and multi-row trigger");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            if (created)
            {
                SqlConnection.ClearAllPools();
                await ExecuteMasterAsync($"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]");
            }
        }
    }

    private static void CheckQuantityCapture()
    {
        var product = new ProductModel { Quentity = 2 };
        var entered = 0;
        var count = 0;
        product.OrderQuantityExceeded += (_, quantity) => { entered = quantity; count++; };
        product.OrderQuentity = 10;
        Check(product.OrderQuentity == 2 && entered == 10 && count == 1, "Requested quantity was lost before shortage capture.");
        product.OrderQuentity = 1;
        product.OrderQuentity = -1;
        Check(product.OrderQuentity == 0 && count == 1, "Valid or negative input created a shortage.");
    }

    private static void CheckGrouping()
    {
        var grouped = ProductRequestSummary.Group(new[]
        {
            new ProductRequestEntity { ArticleNumber = "AA-1", Quantity = 8, CustomerId = "C1" },
            new ProductRequestEntity { ArticleNumber = "aa-1", Quantity = 3, CustomerId = "C2" },
            new ProductRequestEntity { ArticleNumber = "BB-2", Quantity = int.MaxValue },
            new ProductRequestEntity { ArticleNumber = "BB-2", Quantity = int.MaxValue }
        });
        Check(grouped.Count == 2 && grouped[0].Quantity == 11 && grouped[0].Details.Count == 2,
            "Summaries lost customer-level details or did not combine articles.");
        Check(grouped[1].Quantity == 2L * int.MaxValue, "Summary overflowed integer quantities.");
    }

    private static async Task CheckPermissionRulesAsync()
    {
        var permissions = new TestPermissions();
        var factory = new PermissionProbeFactory();
        var service = new ProductRequestService(factory, permissions);
        Func<Task>[] actions =
        [
            () => service.GetJournalAsync(ProductRequestKind.Shortage, null, null, null, false),
            () => service.GetBrandsAsync(),
            () => service.DeleteAsync([]),
            () => service.SetProcessedAsync([], true)
        ];
        foreach (var admin in new[] { false, true })
        {
            permissions.Admin = admin;
            foreach (var action in actions) await ExpectFailure<UnauthorizedAccessException>(action);
        }
        Check(factory.AccessCount == 0, "Admin permission alone must not allow journal database access.");
        permissions.Admin = false;
        permissions.ProductRequests = true;
        foreach (var action in actions) await ExpectFailure<PermissionProbeException>(action);
        Check(factory.AccessCount == actions.Length, "Journal access must work without Admin when ProductRequests is granted.");
        permissions.ProductRequests = false;
        foreach (var action in actions) await ExpectFailure<UnauthorizedAccessException>(action);
        Check(factory.AccessCount == actions.Length, "Revoked journal access still reached the database.");
        Console.WriteLine("PASS: independent ProductRequests grant, Admin-only denial, and live revocation protect all journal operations");
    }

    private async Task SeedAsync()
    {
        await using var db = CreateContext();
        var user = new UserEntity { UserName = "request-user", FirstName = "Test", LastName = "Seller", Password = "test" };
        var other = new UserEntity { UserName = "other-user", FirstName = "Other", LastName = "Seller", Password = "test" };
        var brand = new BrandEntity { BrandName = "Test brand" };
        var group = new GroupEntity { GroupName = "Test group" };
        db.AddRange(user, other, brand, group,
            new CustomerEntity { Id = "C1", FullName = "First customer" },
            new CustomerEntity { Id = "C2", FullName = "Second customer" });
        await db.SaveChangesAsync();
        _userId = user.Id; _otherUserId = other.Id; _brandId = brand.Id; _groupId = group.Id;
        db.Products.AddRange(Product("SHORT", 2), Product("ZERO", 0));
        await db.SaveChangesAsync();
    }

    private ProductEntity Product(string article, int stock) => new()
    {
        ArticleNumber = article, ProductName = "Test product", BrandId = _brandId, GroupId = _groupId,
        Quentity = stock, Marka = "Make", Model = "Model", Alternative = "", WarehousePlace = ""
    };

    private async Task CheckCreationAsync(ProductRequestService service)
    {
        var automaticId = Guid.NewGuid();
        var shortRequest = await service.CreateAsync(automaticId, "SHORT", 8, _userId, "C1", automaticShortage: true);
        Check(shortRequest.Quantity == 8 && shortRequest.Kind == ProductRequestKind.Shortage && shortRequest.IsSubmitted,
            "Automatic shortage was not submitted.");
        await service.CreateAsync(automaticId, "SHORT", 8, _userId, "C1", automaticShortage: true);
        await service.CreateAsync(automaticId, "SHORT", 9, _userId, "C1", automaticShortage: true);
        await using (var db = CreateContext())
            Check(await db.ProductRequests.CountAsync(x => x.Id == automaticId) == 1 &&
                await db.ProductRequests.Where(x => x.Id == automaticId).Select(x => x.Quantity).SingleAsync() == 9,
                "Automatic retries duplicated demand or lost the last completed quantity.");
        var zero = await service.CreateAsync(Guid.NewGuid(), " ZERO ", 5, _userId, "C2");
        Check(zero.Kind == ProductRequestKind.OutOfStock && zero.CustomerName == "Second customer" && zero.BrandName == "Test brand",
            "Out-of-stock request lost its classification or snapshots.");
        await ExpectFailure<ArgumentException>(() => service.CreateAsync(Guid.NewGuid(), "SHORT", 0, _userId, "C1"));
        await ExpectFailure<InvalidOperationException>(() => service.CreateAsync(Guid.NewGuid(), "SHORT", 1, _userId, ""));
    }

    private async Task CheckDraftsAsync(ProductRequestService service)
    {
        var draft = await service.CreateAsync(Guid.NewGuid(), "NEW", 7, _userId, "C1");
        Check(draft.Kind == ProductRequestKind.NewArticle && !draft.IsSubmitted && draft.CustomerId == "C1",
            "New article was sent before its characteristics were completed.");
        Check((await service.GetDraftsAsync(_otherUserId)).Count == 0, "A user can read another user's drafts.");
        await ExpectFailure<InvalidOperationException>(() => service.SaveDraftAsync(draft, _otherUserId, false));
        await ExpectFailure<ArgumentException>(() => service.SaveDraftAsync(draft, _userId, true));
        draft.ProductName = "New product"; draft.BrandName = "New brand"; draft.Marka = "Make";
        draft.Model = "Model"; draft.GroupName = "Group"; draft.Notes = "Customer request";
        await service.SaveDraftAsync(draft, _userId, false);
        Check((await service.GetDraftsAsync(_userId)).Single().BrandName == "New brand", "Partial data was not saved.");
        await service.SaveDraftAsync(draft, _userId, true);
        Check((await service.GetDraftsAsync(_userId)).Count == 0, "Submitted article remained in editable drafts.");
        await ExpectFailure<InvalidOperationException>(() => service.SaveDraftAsync(draft, _userId, false));
    }

    private async Task CheckFiltersAndActionsAsync(ProductRequestService service)
    {
        await ExpectFailure<UnauthorizedAccessException>(() => service.GetJournalAsync(ProductRequestKind.Shortage, null, null, null, false));
        await ExpectFailure<UnauthorizedAccessException>(() => service.DeleteAsync([]));
        _permissions.ProductRequests = true;
        var today = DateTime.Today;
        var extra = await service.CreateAsync(Guid.NewGuid(), "SHORT", 3, _otherUserId, "C2");
        var historical = await service.CreateAsync(Guid.NewGuid(), "SHORT", 20, _userId, "C1");
        await using (var db = CreateContext())
        {
            await db.ProductRequests.Where(x => x.Id == historical.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, today.AddDays(-2)));
        }
        var rows = await service.GetJournalAsync(ProductRequestKind.Shortage, today, today, "Test brand", false);
        Check(rows.Single().Quantity == 12 && rows.Single().Details.Count == 2, "Date/brand filters were applied after aggregation.");
        Check((await service.GetJournalAsync(ProductRequestKind.Shortage, null, null, "Missing brand", false)).Count == 0,
            "Brand filter was ignored.");
        await ExpectFailure<ArgumentException>(() => service.GetJournalAsync(ProductRequestKind.Shortage, today, today.AddDays(-1), null, false));
        var newRow = (await service.GetJournalAsync(ProductRequestKind.NewArticle, null, null, null, false)).Single();
        await service.SetProcessedAsync(newRow.Details.Select(x => x.Id), true);
        Check((await service.GetJournalAsync(ProductRequestKind.NewArticle, null, null, null, false)).Count == 0,
            "Processed articles were not hidden.");
        Check((await service.GetJournalAsync(ProductRequestKind.NewArticle, null, null, null, true)).Single().IsProcessed,
            "Processed articles cannot be inspected.");
        await service.DeleteAsync(rows.Single().Details.Select(x => x.Id).Append(newRow.Details[0].Id));
        var remaining = await service.GetJournalAsync(ProductRequestKind.Shortage, null, null, null, false);
        Check(remaining.Single().Quantity == 20 && remaining.Single().Details.Single().Id == historical.Id,
            "Deleting a filtered summary deleted demand outside its filters.");
        Check((await service.GetJournalAsync(ProductRequestKind.NewArticle, null, null, null, true)).Count == 1,
            "Delete incorrectly removed new article requests.");
    }

    private async Task CheckBulkDeletionAsync(ProductRequestService service)
    {
        foreach (var kind in new[] { ProductRequestKind.Shortage, ProductRequestKind.OutOfStock })
        {
            var brand = $"Bulk {kind}";
            var article = $"BULK-{(int)kind}";
            var today = DateTime.Today;
            ProductRequestEntity Request(string code, string requestBrand, ProductRequestKind requestKind,
                DateTime date, bool submitted = true) => new()
            {
                Id = Guid.NewGuid(), ArticleNumber = code, Quantity = 2, Kind = requestKind,
                BrandName = requestBrand, CreatedAt = date, IsSubmitted = submitted,
                UserId = _userId, CustomerId = "C1"
            };
            var visible = new[]
            {
                Request(article, brand, kind, today),
                Request(article, brand, kind, today),
                Request(article + "-SECOND", brand, kind, today)
            };
            var protectedNew = Request(article, brand, ProductRequestKind.NewArticle, today);
            var protectedDraft = Request(article, brand, ProductRequestKind.NewArticle, today, false);
            var hidden = new[]
            {
                Request(article, brand + " other", kind, today),
                Request(article, brand, kind, today.AddDays(-1)),
                Request(article, brand, kind == ProductRequestKind.Shortage
                    ? ProductRequestKind.OutOfStock : ProductRequestKind.Shortage, today),
                protectedNew, protectedDraft
            };
            await using (var db = CreateContext())
            {
                db.ProductRequests.AddRange(visible.Concat(hidden));
                await db.SaveChangesAsync();
            }
            var rows = await service.GetJournalAsync(kind, today, today, brand, false);
            Check(rows.Count == 2 && rows.Sum(x => x.RequestCount) == 3,
                "Bulk-delete fixture must display two articles and three requests.");
            var displayedIds = rows.SelectMany(row => row.Details).Select(request => request.Id).ToArray();
            var later = Request(article, brand, kind, today);
            await using (var db = CreateContext())
            {
                db.ProductRequests.Add(later);
                await db.SaveChangesAsync();
            }
            await service.DeleteAsync(displayedIds.Append(protectedNew.Id).Append(protectedDraft.Id));
            await using (var db = CreateContext())
            {
                Check(!await db.ProductRequests.AnyAsync(x => displayedIds.Contains(x.Id)),
                    "Bulk deletion left requests from a displayed article.");
                var preservedIds = hidden.Select(x => x.Id).Append(later.Id).ToArray();
                Check(await db.ProductRequests.CountAsync(x => preservedIds.Contains(x.Id)) == preservedIds.Length,
                    "Bulk deletion removed a hidden date, brand, category, new article, draft or later arrival.");
            }
        }
        Console.WriteLine("PASS: both stock tabs delete all displayed articles and preserve hidden requests and later arrivals");
    }

    private async Task CheckBulkProcessingAsync(ProductRequestService service)
    {
        var today = DateTime.Today;
        const string brand = "Bulk processing";
        ProductRequestEntity Request(string article, bool processed = false) => new()
        {
            Id = Guid.NewGuid(), ArticleNumber = article, Quantity = 2, Kind = ProductRequestKind.NewArticle,
            BrandName = brand, CreatedAt = today, IsSubmitted = true, IsProcessed = processed,
            UserId = _userId, CustomerId = "C1"
        };
        var visible = new[] { Request("PROCESS-A"), Request("PROCESS-A", true), Request("PROCESS-B") };
        var otherBrand = Request("PROCESS-A"); otherBrand.BrandName = "Other processing brand";
        var old = Request("PROCESS-A"); old.CreatedAt = today.AddDays(-1);
        var draft = Request("PROCESS-A"); draft.IsSubmitted = false;
        var shortage = Request("PROCESS-A"); shortage.Kind = ProductRequestKind.Shortage;
        var zero = Request("PROCESS-A"); zero.Kind = ProductRequestKind.OutOfStock;
        var hidden = new[] { otherBrand, old, draft, shortage, zero };
        await using (var db = CreateContext())
        {
            db.ProductRequests.AddRange(visible.Concat(hidden));
            await db.SaveChangesAsync();
        }
        var rows = await service.GetJournalAsync(ProductRequestKind.NewArticle, today, today, brand, true);
        Check(rows.Count == 2 && rows.Sum(x => x.RequestCount) == 3,
            "Bulk-processing fixture must include several articles and a partly processed group.");
        var displayedIds = rows.SelectMany(row => row.Details).Where(request => !request.IsProcessed)
            .Select(request => request.Id).ToArray();
        var later = Request("PROCESS-A");
        await using (var db = CreateContext())
        {
            db.ProductRequests.Add(later);
            await db.SaveChangesAsync();
        }
        // Repeating the action must keep checked requests checked, without touching drafts or stock requests.
        var selectedIds = displayedIds.Concat(new[] { draft.Id, shortage.Id, zero.Id }).ToArray();
        await service.SetProcessedAsync(selectedIds, true);
        await service.SetProcessedAsync(selectedIds, true);
        await using (var db = CreateContext())
        {
            var visibleIds = visible.Select(x => x.Id).ToArray();
            Check(await db.ProductRequests.CountAsync(x => visibleIds.Contains(x.Id) && x.IsProcessed) == visible.Length,
                "Bulk processing did not check every displayed article and its underlying requests.");
            var untouchedIds = hidden.Select(x => x.Id).Append(later.Id).ToArray();
            Check(await db.ProductRequests.CountAsync(x => untouchedIds.Contains(x.Id) && !x.IsProcessed) == untouchedIds.Length,
                "Bulk processing changed a hidden brand, date, draft, stock request or later arrival.");
        }
        var unprocessed = await service.GetJournalAsync(ProductRequestKind.NewArticle, today, today, brand, false);
        Check(unprocessed.Single().Details.Single().Id == later.Id,
            "Processed requests must be hidden by default while a later arrival stays visible.");
        var all = await service.GetJournalAsync(ProductRequestKind.NewArticle, today, today, brand, true);
        Check(all.Sum(x => x.RequestCount) == visible.Length + 1,
            "Processed requests must remain available when showing processed entries.");
        Console.WriteLine("PASS: bulk processing marks displayed new articles, preserves hidden requests and later arrivals, and is idempotent");
    }

    private async Task CheckTriggerAsync(ProductRequestService service)
    {
        await service.CreateAsync(Guid.NewGuid(), "NEW", 4, _otherUserId, "C2");
        await service.CreateAsync(Guid.NewGuid(), "ANOTHER", 2, _userId, "C1");
        await service.CreateAsync(Guid.NewGuid(), "KEEP-DRAFT", 1, _userId, "C1");
        await using var db = CreateContext();
        // Both EF inserts and a single SQL statement with several inserted rows must work.
        db.Products.Add(Product("NEW", 0));
        await db.SaveChangesAsync();
        Check(!await db.ProductRequests.AnyAsync(x => x.ArticleNumber == "NEW"), "Trigger left submitted, processed or draft NEW requests.");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Products (ArticleNumber, Numbering, ProductName, Model, Marka, Alternative, GroupId, BrandId,
                Quentity, WarehousePlace, MinRemainingQuantity, PriceLevel1, PriceLevel2, PriceLevel3, PriceLevel4, PriceLevel5)
            VALUES ('ANOTHER', 0, N'Another', N'', N'', N'', {_groupId}, {_brandId}, 0, N'', 0, 0, 0, 0, 0, 0),
                   ('UNRELATED', 0, N'Unrelated', N'', N'', N'', {_groupId}, {_brandId}, 0, N'', 0, 0, 0, 0, 0, 0);
            """);
        Check(!await db.ProductRequests.AnyAsync(x => x.ArticleNumber == "ANOTHER"), "Multi-row trigger did not clear the matching draft.");
        Check(await db.ProductRequests.AnyAsync(x => x.ArticleNumber == "KEEP-DRAFT") &&
            await db.ProductRequests.AnyAsync(x => x.ArticleNumber == "ZERO") &&
            await db.ProductRequests.AnyAsync(x => x.ArticleNumber == "SHORT"), "Trigger removed unrelated or existing-product demand.");
        await db.Database.ExecuteSqlRawAsync("UPDATE Products SET ArticleNumber = 'KEEP-DRAFT' WHERE ArticleNumber = 'UNRELATED'");
        Check(!await db.ProductRequests.AnyAsync(x => x.ArticleNumber == "KEEP-DRAFT"), "Changing a product article did not clear its former draft.");
    }

    private async Task ExecuteMasterAsync(string sql)
    {
        await using var connection = new SqlConnection(_master);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExpectFailure<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed class TestPermissions : IPermissionChecker
{
    public bool Admin { get; set; }
    public bool ProductRequests { get; set; }
    public bool Has(string key) => key == PermissionCatalog.ProductRequests ? ProductRequests
        : key == PermissionCatalog.Admin && Admin;
}

internal sealed class TestFactory(Func<DatabaseContext> create) : IDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext() => create();
}

internal sealed class PermissionProbeException : Exception;

internal sealed class PermissionProbeFactory : IDbContextFactory<DatabaseContext>
{
    public int AccessCount { get; private set; }
    public DatabaseContext CreateDbContext()
    {
        AccessCount++;
        throw new PermissionProbeException();
    }
}
