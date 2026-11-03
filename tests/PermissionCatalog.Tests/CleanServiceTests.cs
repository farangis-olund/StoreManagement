using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal sealed class CleanServiceTests(Func<DatabaseContext> createContext)
{
    public async Task RunAsync()
    {
        await SeedAsync();
        await using var db = createContext();
        var before = TargetCounts(db);
        var preserved = PreservedCounts(db);
        Assert(before.All(count => count > 0), "Every cleaned table must contain test data.");

        var foreignKeyBlocked = false;
        try
        {
            await db.Orders.ExecuteDeleteAsync();
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            foreignKeyBlocked = ex.Message.Contains("CourierPayments", StringComparison.OrdinalIgnoreCase);
        }
        Assert(foreignKeyBlocked, "Expected the original courier-payment foreign key failure.");
        Console.WriteLine("PASS: reproduced cleanup failure caused by courier payments referencing orders");

        var service = new CleanService(new TestContextFactory(createContext));
        // Fail after several deletes to verify that the entire cleanup rolls back.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER [RejectTestCleanup] ON [Orders] AFTER DELETE AS
            BEGIN
                THROW 51000, 'Forced cleanup failure', 1;
            END
            """);
        var failed = false;
        try
        {
            await service.DeleteAllAsync();
        }
        catch (SqlException ex) when (ex.Number == 51000)
        {
            failed = true;
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER [RejectTestCleanup]");
        }
        Assert(failed && before.SequenceEqual(TargetCounts(db)), "Failed cleanup must restore all deleted rows.");
        Console.WriteLine("PASS: cleanup failure rolls back all preceding deletes");

        await service.DeleteAllAsync();
        Assert(TargetCounts(db).All(count => count == 0), "Cleanup left transaction records behind.");
        Assert(preserved.SequenceEqual(PreservedCounts(db)), "Cleanup removed data outside its intended scope.");
        Assert(await db.Products.Where(p => p.ArticleNumber == "CLEAN-PRODUCT").Select(p => p.Quentity).SingleAsync() == 42,
            "Cleanup must preserve current inventory quantities.");
        await service.DeleteAllAsync();
        Assert(TargetCounts(db).All(count => count == 0) && preserved.SequenceEqual(PreservedCounts(db)),
            "Repeated cleanup must succeed without changing preserved data.");
        Console.WriteLine("PASS: cleanup removes all ten transaction tables, preserves master data and stock, and can run twice");
    }

    private async Task SeedAsync()
    {
        await using var db = createContext();
        var customer = new CustomerEntity { Id = "CLEAN-CUSTOMER", FullName = "Cleanup test" };
        var user = new UserEntity { UserName = "cleanup-test", FirstName = "Test", LastName = "User", Password = "test-only" };
        var courier = new CourierEntity { Id = "CLEAN-COURIER", FullName = "Cleanup test" };
        var product = new ProductEntity
        {
            ArticleNumber = "CLEAN-PRODUCT", ProductName = "Cleanup test", Model = "Test", Marka = "Test",
            Alternative = "", WarehousePlace = "Test", Quentity = 42,
            Brand = new BrandEntity { BrandName = "Cleanup test" },
            Group = new GroupEntity { GroupName = "Cleanup test" }
        };
        var order = new OrderEntity { Id = "CLEAN-ORDER", Customer = customer, User = user, Courier = courier, SuminWords = "Ten" };
        db.OrderDetails.Add(new OrderDetailEntity { Order = order, Product = product, Quentity = 1, Price = 10 });
        db.CourierPayments.Add(new CourierPaymentEntity { Order = order, Courier = courier, Date = DateTime.Today, AmountInEuro = 10 });
        db.Payments.Add(new CustomerPaymentEntity { Order = order, Customer = customer, Date = DateTime.Today, Amount = 10 });
        db.ReturnDetails.Add(new ReturnDetailEntity
        {
            Return = new ReturnEntity { Id = "CLEAN-RETURN", Customer = customer, InvoiceNumber = order.Id },
            Product = product, Quantity = 1, Price = 10, Total = 10
        });
        db.StockUpdateLog.Add(new StockUpdateLogEntity { UpdateDate = DateTime.Today });
        db.StockMovements.Add(new StockMovementEntity { MovementDate = DateTime.Today, MovementType = "Test", ItemCount = 1 });
        db.StockImportErrors.Add(new StockImportErrorEntity { ArticleNumber = product.ArticleNumber, Quantity = 1 });
        db.StoreExchanges.Add(new StoreExchangeEntity
        {
            Store = new StoreEntity { StoreCode = "CLEAN-STORE", Name = "Cleanup test" },
            Product = product, ExchangeType = "Test", Quantity = 1
        });
        db.Expenses.Add(new ExpenseEntity { Date = DateTime.Today, User = user, AmountEuro = 5 });
        db.StoreTransferSummaries.Add(new StoreTransferSummaryEntity { StoreCode = "CLEAN-STORE", User = user, TotalQuantity = 1 });
        await db.SaveChangesAsync();
    }

    private static int[] TargetCounts(DatabaseContext db) =>
    [
        db.CourierPayments.Count(), db.OrderDetails.Count(), db.Payments.Count(), db.ReturnDetails.Count(),
        db.StockUpdateLog.Count(), db.StockMovements.Count(), db.StockImportErrors.Count(),
        db.Returns.Count(), db.Orders.Count(), db.StoreExchanges.Count()
    ];

    private static int[] PreservedCounts(DatabaseContext db) =>
    [
        db.Customers.Count(), db.Products.Count(), db.Brands.Count(), db.Groups.Count(), db.Couriers.Count(),
        db.Stores.Count(), db.Users.Count(), db.Roles.Count(), db.UserRoles.Count(), db.Permissions.Count(),
        db.RolePermissions.Count(), db.PriceLevels.Count(), db.SalesManagers.Count(), db.ManagerCustomers.Count(),
        db.Expenses.Count(), db.StoreTransferSummaries.Count()
    ];

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
