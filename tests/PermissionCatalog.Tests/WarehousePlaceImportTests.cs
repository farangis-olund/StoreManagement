using ClosedXML.Excel;
using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Helpers;
using Infrastructure.Security;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

internal static class WarehousePlaceImportTests
{
    public static void CheckExcelReader()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "test-excel"); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "warehouse-places-with-headers.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Места");
            sheet.Cell(1, 1).Value = "Артикул"; sheet.Cell(1, 2).Value = "Место на складе";
            sheet.Cell(2, 1).Value = "000123"; sheet.Cell(2, 2).Value = " A12/1 ";
            sheet.Cell(3, 1).Value = 123; sheet.Cell(3, 1).Style.NumberFormat.Format = "0000000000"; sheet.Cell(3, 2).Value = "B1/2";
            sheet.Cell(4, 1).Value = "170 451"; sheet.Cell(4, 2).Value = "C2/3";
            sheet.Cell(6, 1).Value = "MISSING-PLACE";
            sheet.Cell(7, 2).Value = "A2/2";
            sheet.Cell(8, 1).Value = new string('X', 51); sheet.Cell(8, 2).Value = "A3/1";
            sheet.Cell(9, 1).Value = "ERROR-CELL"; sheet.Cell(9, 2).FormulaA1 = "1/0";
            sheet.Cell(500, 1).Style.Fill.BackgroundColor = XLColor.Red;
            workbook.SaveAs(path);
        }
        var file = WarehousePlaceExcelReader.Read(path);
        Check(file.HasHeader && file.RowCount == 7 && file.Rows.Count == 3 && file.Issues.Count == 4,
            "Headers and blank/styled rows must not become data; malformed and Excel-error rows must be collected.");
        Check(file.Rows[0] == new WarehousePlaceImportRow(2, "000123", "A12/1") &&
            file.Rows[1].ArticleNumber == "0000000123" && file.Rows[2].ArticleNumber == "170 451",
            "Text/numeric-formatted leading zeros, internal spaces and Excel row numbers must survive parsing.");
        var plainPath = Path.Combine(folder, "warehouse-places-no-headers.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Места");
            sheet.Cell(1, 1).Value = "артикул"; sheet.Cell(1, 2).Value = "A1/1";
            sheet.Cell(2, 1).Value = "000456"; sheet.Cell(2, 2).Value = "B3/2";
            workbook.SaveAs(plainPath);
        }
        var plain = WarehousePlaceExcelReader.Read(plainPath);
        Check(!plain.HasHeader && plain.Rows.Count == 2 && plain.Rows[0].RowNumber == 1 && plain.Rows[0].ArticleNumber == "артикул",
            "A headerless file must start at row 1; one header-like article must not be silently skipped.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { WarehousePlaceExcelReader.Read(path, cancelled.Token); throw new Exception("Cancelled file read proceeded."); }
        catch (OperationCanceledException) { }
        Console.WriteLine("PASS: warehouse-place Excel headers/headerless files, formatted leading zeros, spaces, original row numbers, blank/styled rows, invalid cells and cancellation");
    }

    public static async Task RunAsync(Func<DatabaseContext> createContext)
    {
        var calls = 0;
        var grants = new TestPermissions();
        var denied = new WarehousePlaceService(new TestContextFactory(() => { calls++; throw new Exception("DB boundary"); }), grants);
        try { await denied.ImportAsync([new(1, "X", "A1/1")]); throw new Exception("Unprivileged import was accepted."); }
        catch (UnauthorizedAccessException) { }
        Check(calls == 0, "Inventory access must be checked before database access.");
        grants.Keys.Add(PermissionCatalog.Inventory);
        try { await denied.ImportAsync([new(1, "X", " ")]); throw new Exception("Empty location was accepted."); }
        catch (ArgumentException) { }
        Check(calls == 0, "Bad input must never erase a location or reach the database.");

        await using var db = createContext();
        var prefix = "LOC-" + Guid.NewGuid().ToString("N")[..8];
        var brand = new BrandEntity { BrandName = prefix };
        var group = new GroupEntity { GroupName = prefix };
        var products = Enumerable.Range(0, 6000).Select(i => new ProductEntity
        {
            ArticleNumber = i == 0 ? "000" + prefix : i == 1 ? prefix + " SPACE 1" : prefix + "-" + i.ToString("00000"),
            WarehousePlace = "OLD", Quentity = i + 7, Numbering = i, PriceLevel1 = 12.34m,
            ProductName = "Location fixture", Model = "Test", Marka = "Test", Alternative = "", Brand = brand, Group = group
        }).ToArray();
        db.Products.AddRange(products); await db.SaveChangesAsync();
        var service = new WarehousePlaceService(new TestContextFactory(createContext), grants);
        var input = products.Select((p, i) => new WarehousePlaceImportRow(i + 2, p.ArticleNumber, "A" + i + "/1")).ToList();
        input.Add(new(6002, products[0].ArticleNumber, "LAST"));
        input.Add(new(6003, prefix + "'; DROP TABLE Products;--", "MISS-1"));
        input.Add(new(6004, "未知-" + prefix, "MISS-2"));
        var clock = Stopwatch.StartNew();
        var result = await service.ImportAsync(input);
        clock.Stop();
        Check(result.UpdatedCount == 6000 && result.DuplicateCount == 1 && result.Issues.Count == 2 &&
            result.Issues[0].RowNumber == 6003 && result.Issues[1].ArticleNumber == "未知-" + prefix &&
            result.Issues.All(i => i.Kind == WarehousePlaceImportIssueKind.ArticleNotFound),
            "Bulk import must exceed SQL's parameter limit, deduplicate with last-row precedence, preserve unknown text and collect missing articles.");
        Check(result.UpdatedProducts.Count == 6000 && result.UpdatedProducts[0].ArticleNumber == products[1].ArticleNumber &&
            result.UpdatedProducts[^1].ArticleNumber == products[0].ArticleNumber && result.UpdatedProducts[^1].WarehousePlace == "LAST" &&
            result.UpdatedProducts.All(p => p.ProductName == "Location fixture" && p.GroupName == prefix && p.BrandName == prefix &&
                p.Marka == "Test" && p.Model == "Test"),
            "Import must return every updated product with current places and display details, in Excel order, without missing articles or duplicate rows.");
        var changed = await db.Products.AsNoTracking().Where(p => p.BrandId == brand.Id).ToArrayAsync();
        Check(changed.Length == 6000 && changed.Single(p => p.ArticleNumber == products[0].ArticleNumber).WarehousePlace == "LAST" &&
            changed.Single(p => p.ArticleNumber == products[^1].ArticleNumber).WarehousePlace == "A5999/1" &&
            changed.All(p => p.Quentity == p.Numbering + 7 && p.PriceLevel1 == 12.34m && p.ProductName == "Location fixture"),
            "Only WarehousePlace may change; quantities, prices and other product data must remain intact.");
        Check((await service.GetProductAsync(products[0].ArticleNumber))!.WarehousePlace == "LAST",
            "The manual-location panel must read the fresh imported place.");
        await service.UpdatePlaceAsync(products[1].ArticleNumber, "MANUAL");
        Check((await service.GetProductAsync(products[1].ArticleNumber))!.WarehousePlace == "MANUAL", "Manual updates must keep working.");

        var revoked = new WarehousePlaceService(new TestContextFactory(createContext), new RevokeBeforeCommit());
        try { await revoked.ImportAsync([new(1, products[0].ArticleNumber, "NO-COMMIT")]); throw new Exception("Revocation was ignored."); }
        catch (UnauthorizedAccessException) { }
        Check((await service.GetProductAsync(products[0].ArticleNumber))!.WarehousePlace == "LAST",
            "Permission revocation before commit must roll back the entire location update.");

        var trigger = "WarehousePlaceFailure_" + Guid.NewGuid().ToString("N");
        await db.Database.ExecuteSqlRawAsync($"CREATE TRIGGER [{trigger}] ON dbo.Products AFTER UPDATE AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE WarehousePlace = N'TEST-FAIL') THROW 51000, 'Test failure', 1; END;");
        try
        {
            try { await service.ImportAsync([new(1, products[0].ArticleNumber, "MUST-ROLL-BACK"), new(2, products[1].ArticleNumber, "TEST-FAIL")]); throw new Exception("Database failure was ignored."); }
            catch (Microsoft.Data.SqlClient.SqlException) { }
            Check((await service.GetProductAsync(products[0].ArticleNumber))!.WarehousePlace == "LAST" &&
                (await service.GetProductAsync(products[1].ArticleNumber))!.WarehousePlace == "MANUAL",
                "A database error must roll back all matched rows.");
        }
        finally { await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER [{trigger}];"); }
        await db.Products.Where(p => p.BrandId == brand.Id).ExecuteDeleteAsync();
        await db.Brands.Where(b => b.Id == brand.Id).ExecuteDeleteAsync();
        await db.Groups.Where(g => g.Id == group.Id).ExecuteDeleteAsync();
        Console.WriteLine($"PASS: 6,000 warehouse locations imported in {clock.Elapsed.TotalSeconds:F2}s with a bulk upload and one UPDATE; missing articles, duplicates, untouched stock/prices, manual refresh, permission revocation and SQL failures checked");
    }

    private sealed class RevokeBeforeCommit : IPermissionChecker
    {
        private int _calls;
        public bool Has(string key) => ++_calls < 4;
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
