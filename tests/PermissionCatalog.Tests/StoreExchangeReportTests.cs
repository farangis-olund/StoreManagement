using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

internal static class StoreExchangeReportTests
{
    public static async Task RunAsync(Func<DatabaseContext> createContext)
    {
        var grants = new TestPermissions();
        var calls = 0;
        var denied = new StoreExchangeReportService(new TestContextFactory(() => { calls++; throw new Exception("DB boundary"); }), grants);
        try { await denied.GetAsync(); throw new Exception("Missing Reports grant was accepted."); }
        catch (UnauthorizedAccessException) { }
        Check(calls == 0, "Report access must be checked before database access.");
        grants.Keys.Add(PermissionCatalog.Reports);
        var prefix = "EX-" + Guid.NewGuid().ToString("N")[..8];
        await using var db = createContext();
        var brand = new BrandEntity { BrandName = prefix };
        var group = new GroupEntity { GroupName = prefix };
        ProductEntity Product(string article, int stock) => new()
        {
            ArticleNumber = article, Quentity = stock, ProductName = "Exchange fixture", Model = "Test", Marka = "Test",
            Alternative = "", WarehousePlace = "A1/1", Brand = brand, Group = group
        };
        var first = Product("000" + prefix, 20);
        var second = Product(prefix + " SPACE 1", 0);
        var returnedOnly = Product(prefix + "-RETURN", 10);
        var noExchange = Product(prefix + "-NONE", 30);
        var noHistory = Product(prefix + "-NO-HISTORY", 40);
        var emptyStock = Product(prefix + "-ZERO-STOCK", 0);
        var storeA = new StoreEntity { StoreCode = prefix + "A" };
        var storeB = new StoreEntity { StoreCode = prefix + "B" };
        StoreExchangeEntity Exchange(StoreEntity store, ProductEntity product, string type, int qty) =>
            new() { Store = store, Product = product, ExchangeType = type, Quantity = qty };
        db.Products.AddRange(first, second, returnedOnly, noExchange, noHistory, emptyStock);
        db.StoreExchanges.AddRange(
            Exchange(storeA, first, "получение_товара", 3), Exchange(storeB, first, "получение_товара", 4),
            Exchange(storeA, first, "передача_товара", 5), Exchange(storeB, first, "передача_товара", 2),
            Exchange(storeA, second, "получение_товара", 5),
            Exchange(storeA, first, "получение_возврата", 99), Exchange(storeA, first, "погащение_долга", 88),
            Exchange(storeA, returnedOnly, "получение_возврата", 7), Exchange(storeA, noExchange, "получение_товара", 0));
        await db.SaveChangesAsync();
        var service = new StoreExchangeReportService(new TestContextFactory(createContext), grants);
        var before = await db.StoreExchanges.CountAsync();
        var allRows = await service.GetAsync();
        var rows = allRows.Where(r => r.ArticleNumber.Contains(prefix)).ToArray();
        Check(allRows.Count == await db.Products.CountAsync() &&
            allRows.Select(r => r.ArticleNumber).SequenceEqual(allRows.Select(r => r.ArticleNumber).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) &&
            allRows.Select(r => r.Number).SequenceEqual(Enumerable.Range(1, allRows.Count)),
            "Every product must appear exactly once, sorted by article with consecutive numbering.");
        Check(rows.Length == 6 && rows[0].ArticleNumber == first.ArticleNumber && rows[0].Number < rows[1].Number,
            "All products must be included, even without any exchange history or with zero stock.");
        Check(rows[0].WarehouseQuantity == 20 && rows[0].IncomingQuantity == 7 && rows[0].OutgoingQuantity == 7 &&
            rows[0].RemainingQuantity == 20 && rows[1].RemainingQuantity == -5,
            "Warehouse stock must not be multiplied by stores; own balance must be warehouse minus incoming plus outgoing, including negatives.");
        foreach (var product in new[] { returnedOnly, noExchange, noHistory, emptyStock })
        {
            var row = rows.Single(r => r.ArticleNumber == product.ArticleNumber);
            Check(row.WarehouseQuantity == product.Quentity && row.IncomingQuantity == 0 && row.OutgoingQuantity == 0 &&
                row.RemainingQuantity == product.Quentity,
                "Products without outstanding exchanges must show zero incoming/outgoing and their unchanged warehouse balance.");
        }
        Check(await db.StoreExchanges.CountAsync() == before &&
            (await db.Products.AsNoTracking().SingleAsync(p => p.ArticleNumber == first.ArticleNumber)).Quentity == 20,
            "Reading the report must not change exchanges or warehouse stock.");
        await db.StoreExchanges.Where(x => x.ArticleNumber == first.ArticleNumber && x.StoreCode == storeA.StoreCode &&
            x.ExchangeType == "получение_товара").ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, 1));
        await db.Products.Where(p => p.ArticleNumber == first.ArticleNumber).ExecuteUpdateAsync(s => s.SetProperty(p => p.Quentity, 18));
        var refreshed = (await service.GetAsync()).Single(r => r.ArticleNumber == first.ArticleNumber);
        Check(refreshed.IncomingQuantity == 5 && refreshed.WarehouseQuantity == 18 && refreshed.RemainingQuantity == 20,
            "Refreshing after a repayment must use reduced outstanding records and fresh warehouse quantities.");
        var revoked = new StoreExchangeReportService(new TestContextFactory(() =>
        {
            grants.Keys.Clear(); return createContext();
        }), grants);
        try { await revoked.GetAsync(); throw new Exception("Mid-query revocation was ignored."); }
        catch (UnauthorizedAccessException) { }
        db.StoreExchanges.Add(Exchange(storeA, first, "получение_товара", 2));
        await db.SaveChangesAsync();
        var debtService = new StoreExchangeService(db, null!);
        var allDebts = (await debtService.GetExchangeProductsAsync(null, "получение_товара"))
            .Where(x => x.ArticleNumber.Contains(prefix)).ToArray();
        Check(allDebts.Length == 3 && allDebts.Single(x => x.StoreCode == storeA.StoreCode && x.ArticleNumber == first.ArticleNumber).Debt == 3 &&
            allDebts.Single(x => x.StoreCode == storeA.StoreCode && x.ArticleNumber == first.ArticleNumber).Quantity == 18 &&
            allDebts.Single(x => x.StoreCode == storeB.StoreCode).Debt == 4,
            "All-store debts must keep each store/article distinct, sum repeated debts, and count warehouse stock once");
        Check(allDebts.Select(x => (x.StoreCode, x.ArticleNumber)).SequenceEqual(allDebts
            .OrderBy(x => x.StoreCode, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase)
            .Select(x => (x.StoreCode, x.ArticleNumber))), "All-store debts must sort by store, then article");
        Check((await debtService.GetExchangeProductsAsync(storeA.StoreCode, "получение_товара")).All(x => x.StoreCode == storeA.StoreCode),
            "Single-store debt query included other stores");
        Check((await debtService.GetExchangeProductsAsync(null, "передача_товара")).Where(x => x.ArticleNumber.Contains(prefix)).Count() == 2,
            "The return query must include only outgoing debts for all stores");
        await db.StoreExchanges.Where(x => x.StoreCode.StartsWith(prefix)).ExecuteDeleteAsync();
        await db.Stores.Where(s => s.StoreCode.StartsWith(prefix)).ExecuteDeleteAsync();
        var ids = new[] { first.ArticleNumber, second.ArticleNumber, returnedOnly.ArticleNumber, noExchange.ArticleNumber,
            noHistory.ArticleNumber, emptyStock.ArticleNumber };
        await db.Products.Where(p => ids.Contains(p.ArticleNumber)).ExecuteDeleteAsync();
        await db.Brands.Where(b => b.Id == brand.Id).ExecuteDeleteAsync();
        await db.Groups.Where(g => g.Id == group.Id).ExecuteDeleteAsync();
        Console.WriteLine("PASS: exchange report includes every product, including no exchange history and zero stock, with article sorting and consecutive numbering; groups stores without multiplying stock, ignores closed/return quantities and enforces access");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
