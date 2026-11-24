using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using Infrastructure.Security;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ProductExpiryQuantityTests
{
    private sealed class Factory(Func<DatabaseContext> create) : IDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext() => create();
        public Task<DatabaseContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(create());
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static async Task Throws<T>(Func<Task> work) where T : Exception
    {
        try { await work(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name);
    }
    public static async Task RunAsync(Func<DatabaseContext> create, IPermissionChecker permissions)
    {
        var day = DateTime.Today;
        var factory = new Factory(create); var catalog = new ProductExpiryCatalog();
        await using var outer = create();
        var stock = new ProductService(new ProductRepository(factory), null!, null!, NullLogger<ProductService>.Instance, outer, factory);
        var orders = new OrderService(new OrderRepository(factory), new OrderDetailRepository(factory), NullLogger<OrderService>.Instance,
            stock, factory, permissions, expiryCatalog: catalog);
        var batches = new ProductExpiryService(factory, permissions, catalog);
        int user;
        await using (var seed = create())
        {
            var brand = new BrandEntity { BrandName = "LOTS" }; var group = new GroupEntity { GroupName = "Lots test oil" };
            foreach (var article in new[] { "LOT", "RACE", "PLAIN", "FAIL", "MIXED", "SAME-CATALOG", "MIX-RACE", "MAIN-ONLY" }) seed.Products.Add(new()
            {
                ArticleNumber = article, ProductName = "Oil", Brand = brand, Group = group,
                Quentity = article == "RACE" ? 2 : article == "MIX-RACE" ? 5 : article == "MAIN-ONLY" ? 90 : 10,
                WarehousePlace = "CATALOG", Model = "", Marka = "", Alternative = "", PriceLevel1 = 7
            });
            var seller = new UserEntity { UserName = "lot-seller", FirstName = "Lot", LastName = "Seller", Password = "test" };
            seed.Users.Add(seller); seed.Customers.Add(new() { Id = "LOT-C", FullName = "Lot customer" });
            await seed.SaveChangesAsync(); user = seller.Id;
        }
        await batches.SaveAsync(new(Guid.Empty, "LOT", day.AddYears(-1), day, "А1", 2));
        await batches.SaveAsync(new(Guid.Empty, "LOT", day.AddYears(-1), day.AddDays(3), "А2", 3));
        await batches.SaveAsync(new(Guid.Empty, "LOT", day.AddMonths(-8), day.AddDays(5), "А2", 2));
        await batches.SaveAsync(new(Guid.Empty, "LOT", day.AddYears(-2), day.AddDays(-1), "EXPIRED", 1));
        await batches.SaveAsync(new(Guid.Empty, "LOT", day.AddDays(1), day.AddYears(1), "FUTURE", 1));
        await batches.SaveAsync(new(Guid.Empty, "RACE", day.AddYears(-1), day.AddDays(7), "R1", 2));
        await batches.SaveAsync(new(Guid.Empty, "FAIL", day.AddYears(-1), day.AddDays(2), "F1", 1));
        await batches.SaveAsync(new(Guid.Empty, "FAIL", day.AddYears(-2), day.AddDays(-1), "EXPIRED", 9));
        await batches.SaveAsync(new(Guid.Empty, "MIXED", day.AddYears(-1), day, "А1", 2));
        await batches.SaveAsync(new(Guid.Empty, "MIXED", day.AddYears(-1), day.AddDays(3), "А2", 3));
        await batches.SaveAsync(new(Guid.Empty, "MIXED", day.AddYears(-2), day.AddDays(-1), "EXPIRED", 0));
        await batches.SaveAsync(new(Guid.Empty, "MIXED", day.AddDays(1), day.AddYears(1), "FUTURE", 0));
        await batches.SaveAsync(new(Guid.Empty, "SAME-CATALOG", day.AddYears(-1), day.AddDays(7), "CATALOG", 2));
        await batches.SaveAsync(new(Guid.Empty, "MIX-RACE", day.AddYears(-1), day.AddDays(-1), "М1", 2));
        await batches.SaveAsync(new(Guid.Empty, "MAIN-ONLY", day.AddYears(-2), day.AddDays(-1), "EXPIRED", 20));
        await batches.SaveAsync(new(Guid.Empty, "MAIN-ONLY", day.AddDays(1), day.AddYears(1), "FUTURE", 5));
        await Throws<ArgumentException>(() => batches.SaveAsync(new(Guid.Empty, "LOT", day, day, "NEG", -1)));
        await Throws<InvalidOperationException>(() => batches.SaveAsync(new(Guid.Empty, "LOT", day, day, "OVER", 2)));

        OrderEntity Order(string id, string article, int qty, bool barter = false, bool direct = false) => new()
        {
            Id = id, UserId = user, CustomerId = "LOT-C", Date = day, Rate = 1, SuminWords = "", IsBarter = barter, DirectFromStock = direct,
            OrderDetails = [new() { ArticleNumber = article, Quentity = qty, Price = 7 }]
        };
        var order = await orders.AddOrderAsync(Order("LOT-SALE", "LOT", 4));
        Check(order != null && order.OrderDetails.Single().WarehousePlaceSnapshot == "EXPIRED, А1, А2", "Batch places must follow the earliest expiry, including expired batches");
        await using (var db = create())
        {
            var allocations = await db.OrderExpiryAllocations.Where(x => x.OrderId == "LOT-SALE").ToListAsync();
            Check(allocations.Count == 3 && allocations.Sum(x => x.Quantity) == 4, "Sale did not persist per-batch allocations");
            var lotRows = await db.ProductExpiryBatches.Where(x => x.ArticleNumber == "LOT").OrderBy(x => x.ExpiresOn).ToListAsync();
            Check(lotRows.Single(x => x.WarehousePlace == "А1").Quantity == 0 && lotRows.Single(x => x.ExpiresOn == day.AddDays(3)).Quantity == 2 &&
                lotRows.Single(x => x.WarehousePlace == "EXPIRED").Quantity == 0 && lotRows.Single(x => x.WarehousePlace == "FUTURE").Quantity == 1,
                "Batch deduction must follow expiry order without treating dates as a sale restriction");
            Check((await db.Products.SingleAsync(x => x.ArticleNumber == "LOT")).Quentity == 6, "Global stock was not deducted exactly once");
            var used = lotRows.Single(x => x.WarehousePlace == "А1");
            await Throws<InvalidOperationException>(() => batches.DeleteAsync(used.Id, used.Version));
            await batches.SaveAsync(new(used.Id, "LOT", used.ManufacturedOn, used.ExpiresOn, "MOVED", 0, used.Version));
        }
        var persisted = await orders.GetOrderAsync("LOT-SALE");
        Check(persisted!.OrderDetails.Single().WarehousePlaceSnapshot == "EXPIRED, А1, А2", "Reprinting changed historical places after moving a batch");
        persisted.OrderDetails.Single().WarehousePlaceSnapshot = "CORRUPT";
        persisted.IsSent = true; await orders.UpdateOrderAsync(persisted);
        Check((await orders.GetOrderAsync("LOT-SALE"))!.OrderDetails.Single().WarehousePlaceSnapshot == "EXPIRED, А1, А2", "Updating invoice status must preserve saved places");
        Check(!await orders.DeleteOrderByIdAsync(persisted), "Deletion must not erase a batch sale's allocation history");

        // Both rows must roll back only when the main product stock cannot cover the order.
        var failed = Order("LOT-FAIL", "PLAIN", 2);
        failed.OrderDetails.Add(new() { ArticleNumber = "FAIL", Quentity = 11, Price = 7 });
        Check(await orders.AddOrderAsync(failed) == null, "An order exceeding the main stock was accepted");
        await using (var db = create())
            Check(!await db.Orders.AnyAsync(x => x.Id == "LOT-FAIL") && (await db.Products.SingleAsync(x => x.ArticleNumber == "PLAIN")).Quentity == 10 &&
                (await db.Products.SingleAsync(x => x.ArticleNumber == "FAIL")).Quentity == 10 &&
                await db.ProductExpiryBatches.Where(x => x.ArticleNumber == "FAIL").SumAsync(x => x.Quantity) == 10,
                "Failed checkout partially changed stock or saved an invoice");
        var samePlace = await orders.AddOrderAsync(Order("LOT-SAME", "LOT", 3, barter: true));
        Check(samePlace?.OrderDetails.Single().WarehousePlaceSnapshot == "А2", "The same place from two batches must appear only once");
        var direct = await orders.AddOrderAsync(Order("LOT-DIRECT", "LOT", 1, direct: true));
        Check(direct != null && !await outer.OrderExpiryAllocations.AnyAsync(x => x.OrderId == direct.Id), "Direct-from-stock orders must retain their existing stock policy");

        ReturnEntity Return(string id, int qty) => new()
        {
            Id = id, CustomerId = "LOT-C", InvoiceNumber = "LOT-SALE", Date = day, RefundMethod = "cash", Rate = 1,
            ReturnDetails = [new() { ArticleNumber = "LOT", Quantity = qty, SourceOrderId = "LOT-SALE", Price = 7, Total = qty * 7 }]
        };
        await using (var db = create())
        {
            var returns = new ReturnService(new ReturnRepository(factory), db, NullLogger<ReturnService>.Instance, expiryCatalog: catalog);
            Check(await returns.AddReturnAsync(Return("LOT-RETURN", 3)) != null, "Return was not saved");
            Check(await returns.AddReturnAsync(Return("LOT-RETURN", 3)) == null, "Retry duplicated a return");
            Check(await returns.AddReturnAsync(Return("LOT-OVERRETURN", 2)) == null, "Excess return was accepted");
        }
        await using (var db = create())
        {
            var details = await db.OrderExpiryAllocations.Where(x => x.OrderId == "LOT-SALE").ToListAsync();
            Check(details.Sum(x => x.ReturnedQuantity) == 3 && await db.ReturnExpiryAllocations.CountAsync() == 2 &&
                (await db.Products.SingleAsync(x => x.ArticleNumber == "LOT")).Quentity == 6, "Return did not restore the original batches and total exactly once");
            var returns = new ReturnService(new ReturnRepository(factory), db, NullLogger<ReturnService>.Instance, expiryCatalog: catalog);
            Check(await returns.DeleteReturnAsync("LOT-RETURN"), "Deleting an unused return did not undo its stock restoration");
        }
        await using (var db = create())
            Check((await db.Products.SingleAsync(x => x.ArticleNumber == "LOT")).Quentity == 3 && !await db.ReturnExpiryAllocations.AnyAsync(), "Deleting a return left restored quantities");
        // Ordinary articles still use their catalog place.
        var ordinary = await orders.AddOrderAsync(Order("PLAIN-SALE", "PLAIN", 1));
        Check(ordinary?.OrderDetails.Single().WarehousePlaceSnapshot == "CATALOG", "An ordinary article lost its warehouse place");

        var mixed = await orders.AddOrderAsync(Order("MIXED-SALE", "MIXED", 7));
        Check(mixed?.OrderDetails.Single().WarehousePlaceSnapshot == "А1, А2, CATALOG",
            "A mixed sale must list the nearest-expiry places first and the ordinary place last");
        await using (var db = create())
        {
            var allocations = await db.OrderExpiryAllocations.Where(x => x.OrderId == "MIXED-SALE").ToListAsync();
            var rows = await db.ProductExpiryBatches.Where(x => x.ArticleNumber == "MIXED").ToListAsync();
            Check(allocations.Count == 2 && allocations.Sum(x => x.Quantity) == 5 &&
                rows.Where(x => x.WarehousePlace is "А1" or "А2").All(x => x.Quantity == 0) &&
                rows.Single(x => x.WarehousePlace == "EXPIRED").Quantity == 0 && rows.Single(x => x.WarehousePlace == "FUTURE").Quantity == 0 &&
                (await db.Products.SingleAsync(x => x.ArticleNumber == "MIXED")).Quentity == 3,
                "Mixed checkout must deduct five batch units and two ordinary units, leaving empty batches unchanged");
        }
        var fallback = await orders.AddOrderAsync(Order("MIXED-ORDINARY", "MIXED", 1));
        Check(fallback?.OrderDetails.Single().WarehousePlaceSnapshot == "CATALOG", "Exhausted batches must allow selling remaining ordinary stock");
        Check(await orders.AddOrderAsync(Order("MIXED-OVER-STOCK", "MIXED", 3)) == null, "A sale exceeded the remaining main stock");
        await using (var db = create())
            Check((await db.Products.SingleAsync(x => x.ArticleNumber == "MIXED")).Quentity == 2 &&
                !await db.OrderExpiryAllocations.AnyAsync(x => x.OrderId == "MIXED-ORDINARY") && !await db.Orders.AnyAsync(x => x.Id == "MIXED-OVER-STOCK"),
                "Exhausted batches must use the catalog place while the main stock remains the sale limit");
        ReturnEntity MixedReturn(string id, string source, int qty) => new()
        {
            Id = id, CustomerId = "LOT-C", InvoiceNumber = source, Date = day, RefundMethod = "cash", Rate = 1,
            ReturnDetails = [new() { ArticleNumber = "MIXED", Quantity = qty, SourceOrderId = source, Price = 7, Total = qty * 7 }]
        };
        await using (var db = create())
        {
            var returns = new ReturnService(new ReturnRepository(factory), db, NullLogger<ReturnService>.Instance, expiryCatalog: catalog);
            Check(await returns.AddReturnAsync(MixedReturn("ORDINARY-RETURN", "MIXED-ORDINARY", 1)) != null,
                "An ordinary-only fallback return must restore the article stock");
            Check(await returns.DeleteReturnAsync("ORDINARY-RETURN"), "Undoing an ordinary-only fallback return failed");
            await using (var verify = create())
                Check((await verify.Products.SingleAsync(x => x.ArticleNumber == "MIXED")).Quentity == 2,
                    "Ordinary-only fallback return/undo changed the stock twice");
            Check(await returns.AddReturnAsync(MixedReturn("ORDINARY-RETURN", "MIXED-ORDINARY", 1)) != null,
                "Ordinary-only fallback return could not be saved after undo");
            Check(await returns.AddReturnAsync(MixedReturn("MIXED-RETURN", "MIXED-SALE", 6)) != null,
                "A return spanning batches and ordinary stock was rejected");
            Check(await returns.AddReturnAsync(MixedReturn("MIXED-RETURN-REST", "MIXED-SALE", 1)) != null,
                "The ordinary remainder of a mixed sale could not be returned");
            Check(await returns.AddReturnAsync(MixedReturn("MIXED-RETURN-REST", "MIXED-SALE", 1)) == null &&
                await returns.AddReturnAsync(MixedReturn("MIXED-OVERRETURN", "MIXED-SALE", 1)) == null,
                "A mixed return was duplicated or exceeded the original sale");
        }
        await using (var db = create())
        {
            Check((await db.Products.SingleAsync(x => x.ArticleNumber == "MIXED")).Quentity == 10 &&
                await db.ProductExpiryBatches.Where(x => x.ArticleNumber == "MIXED").SumAsync(x => x.Quantity) == 5 &&
                await db.OrderExpiryAllocations.Where(x => x.OrderId == "MIXED-SALE").SumAsync(x => x.ReturnedQuantity) == 5 &&
                !await db.ReturnExpiryAllocations.AnyAsync(x => x.ReturnDetail.ReturnId == "MIXED-RETURN-REST"),
                "Mixed returns must restore the full article quantity but only the actual batch quantities");
            var returns = new ReturnService(new ReturnRepository(factory), db, NullLogger<ReturnService>.Instance, expiryCatalog: catalog);
            Check(await returns.DeleteReturnAsync("MIXED-RETURN") && await returns.DeleteReturnAsync("MIXED-RETURN-REST"),
                "Undoing mixed returns out of order failed");
        }
        await using (var db = create())
            Check((await db.Products.SingleAsync(x => x.ArticleNumber == "MIXED")).Quentity == 3 &&
                await db.ProductExpiryBatches.Where(x => x.ArticleNumber == "MIXED").SumAsync(x => x.Quantity) == 0 &&
                (await db.OrderDetails.SingleAsync(x => x.OrderId == "MIXED-SALE")).ReturnedQty == 0,
                "Undoing mixed returns left incorrect article or batch quantities");
        var sameCatalog = await orders.AddOrderAsync(Order("SAME-CATALOG-SALE", "SAME-CATALOG", 4, barter: true));
        Check(sameCatalog?.OrderDetails.Single().WarehousePlaceSnapshot == "CATALOG",
            "A mixed barter sale must not repeat the catalog place when it matches a batch place");

        Check(await orders.AddOrderAsync(Order("MAIN-OVER-LIMIT", "MAIN-ONLY", 91)) == null,
            "The main product quantity must remain the only upper bound");
        var fullStock = await orders.AddOrderAsync(Order("MAIN-FULL-STOCK", "MAIN-ONLY", 90));
        Check(fullStock?.OrderDetails.Single().WarehousePlaceSnapshot == "EXPIRED, FUTURE, CATALOG",
            "Expired or future production dates must not reduce the main stock limit or lose ordered places");
        await using (var db = create())
        {
            Check((await db.Products.SingleAsync(x => x.ArticleNumber == "MAIN-ONLY")).Quentity == 0 &&
                await db.ProductExpiryBatches.Where(x => x.ArticleNumber == "MAIN-ONLY").SumAsync(x => x.Quantity) == 0 &&
                await db.OrderExpiryAllocations.Where(x => x.OrderId == "MAIN-FULL-STOCK").SumAsync(x => x.Quantity) == 25,
                "Selling all 90 units must deduct 25 batch units and 65 catalog units exactly once");
            var returns = new ReturnService(new ReturnRepository(factory), db, NullLogger<ReturnService>.Instance, expiryCatalog: catalog);
            Check(await returns.AddReturnAsync(new()
            {
                Id = "MAIN-RETURN", CustomerId = "LOT-C", InvoiceNumber = "MAIN-FULL-STOCK", Date = day, RefundMethod = "cash", Rate = 1,
                ReturnDetails = [new() { ArticleNumber = "MAIN-ONLY", Quantity = 90, SourceOrderId = "MAIN-FULL-STOCK", Price = 7, Total = 630 }]
            }) != null, "Returning the full main stock with expired/future batch allocations failed");
        }
        await using (var db = create())
        {
            Check((await db.Products.SingleAsync(x => x.ArticleNumber == "MAIN-ONLY")).Quentity == 90 &&
                await db.ProductExpiryBatches.Where(x => x.ArticleNumber == "MAIN-ONLY").SumAsync(x => x.Quantity) == 25,
                "A full return did not restore the main and batch quantities separately");
            var returns = new ReturnService(new ReturnRepository(factory), db, NullLogger<ReturnService>.Instance, expiryCatalog: catalog);
            Check(await returns.DeleteReturnAsync("MAIN-RETURN"), "Undoing the full main-stock return failed");
        }
        await using (var db = create())
            Check((await db.Products.SingleAsync(x => x.ArticleNumber == "MAIN-ONLY")).Quentity == 0 &&
                await db.ProductExpiryBatches.Where(x => x.ArticleNumber == "MAIN-ONLY").SumAsync(x => x.Quantity) == 0,
                "Undoing the full main-stock return left quantities behind");

        async Task<bool> Race(string id, string article = "RACE", int qty = 2)
        {
            await using var context = create();
            var ps = new ProductService(new ProductRepository(factory), null!, null!, NullLogger<ProductService>.Instance, context, factory);
            var os = new OrderService(new OrderRepository(factory), new OrderDetailRepository(factory), NullLogger<OrderService>.Instance, ps, factory, permissions);
            return await os.AddOrderAsync(Order(id, article, qty)) != null;
        }
        var results = await Task.WhenAll(Race("RACE-1"), Race("RACE-2"));
        await using (var db = create())
            Check(results.Count(x => x) == 1 && (await db.Products.SingleAsync(x => x.ArticleNumber == "RACE")).Quentity == 0 &&
                (await db.ProductExpiryBatches.SingleAsync(x => x.ArticleNumber == "RACE")).Quantity == 0 &&
                await db.OrderExpiryAllocations.CountAsync(x => x.ArticleNumber == "RACE") == 1, "Concurrent checkout oversold or double-deducted a batch");
        var mixedResults = await Task.WhenAll(Race("MIX-RACE-1", "MIX-RACE", 4), Race("MIX-RACE-2", "MIX-RACE", 4));
        await using (var db = create())
            Check(mixedResults.Count(x => x) == 1 && (await db.Products.SingleAsync(x => x.ArticleNumber == "MIX-RACE")).Quentity == 1 &&
                (await db.ProductExpiryBatches.SingleAsync(x => x.ArticleNumber == "MIX-RACE")).Quantity == 0 &&
                await db.OrderExpiryAllocations.Where(x => x.ArticleNumber == "MIX-RACE").SumAsync(x => x.Quantity) == 2,
                "Concurrent mixed checkout oversold ordinary stock or deducted a batch twice");
        Console.WriteLine("PASS: main product stock is the sole sale limit, dates only order batch deductions and places, full-stock sale/return/undo, ordinary fallback, immutable reprints, atomic failure, barter/direct stock and concurrent checkout");
    }
}
