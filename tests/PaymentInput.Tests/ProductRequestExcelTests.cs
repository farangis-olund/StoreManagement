using System.IO;
using ClosedXML.Excel;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;

internal static class ProductRequestExcelTests
{
    public static void Run()
    {
        var created = new DateTime(2026, 11, 10, 12, 30, 0);
        const string filters = "Период: 01.11.2026 — 10.11.2026. Бренд: SACHS";
        for (var tab = 0; tab < 3; tab++)
        {
            var records = new[]
            {
                new ProductRequestEntity
                {
                    Kind = (ProductRequestKind)(tab + 1), ArticleNumber = "0002301765", Quantity = 7,
                    CustomerId = "0007", CustomerName = "Клиент", UserName = "Продавец", BrandName = "SACHS",
                    ProductName = "Новый товар", Marka = "MERC", Model = "W201, W124", GroupName = "Подвеска",
                    Alternative = "00123 456", Notes = "=SUM(1,2)", CreatedAt = created,
                    SubmittedAt = created.AddMinutes(2), IsProcessed = true
                },
                new ProductRequestEntity
                {
                    Kind = (ProductRequestKind)(tab + 1), ArticleNumber = "0002301765", Quantity = 3,
                    CustomerId = "0008", CustomerName = "Другой клиент", UserName = "Продавец 2", BrandName = "SACHS",
                    ProductName = "Новый товар", CreatedAt = created.AddMinutes(-1)
                },
                new ProductRequestEntity
                {
                    Kind = (ProductRequestKind)(tab + 1), ArticleNumber = "170 451", Quantity = 4, BrandName = "SACHS",
                    ProductName = "Амортизатор", CreatedAt = created
                }
            };
            var rows = ProductRequestSummary.Group(records);
            var vm = new ProductRequestsViewModel(null!) { SelectedTab = tab };
            var path = Path.Combine(AppContext.BaseDirectory, $"product-requests-export-{tab + 1}.xlsx");
            using (var original = ProductRequestExcelExport.Create(vm.Title, filters, rows)) original.SaveAs(path);
            using var workbook = new XLWorkbook(path);
            Check(workbook.Worksheets.Count == 2, "Export must include summary and request details.");
            var summary = workbook.Worksheet("Сводка");
            Check(summary.Cell(1, 1).GetString().Contains(vm.Title) && summary.Cell(2, 1).GetString() == filters,
                "Export must identify the active request kind and the applied filters.");
            Check(summary.LastRowUsed()!.RowNumber() == 7 && summary.LastColumnUsed()!.ColumnNumber() == 5,
                "Summary must contain both displayed articles and a totals row, without UI action columns.");
            Check(summary.Cell(5, 1).DataType == XLDataType.Text && summary.Cell(5, 1).GetString() == "0002301765" &&
                summary.Cell(6, 1).GetString() == "170 451", "Export must preserve leading zeros and spaces in articles.");
            Check(summary.Cell(5, 4).DataType == XLDataType.Number && summary.Cell(5, 4).GetValue<long>() == 10 &&
                summary.Cell(5, 5).GetValue<int>() == 2 && summary.Cell(7, 4).GetValue<long>() == 14,
                "Summary quantities and request counts must remain numeric and correctly aggregated.");
            var details = workbook.Worksheet("Детали заявок");
            Check(details.LastRowUsed()!.RowNumber() == 7 && details.LastColumnUsed()!.ColumnNumber() == 15,
                "Details must contain every request behind the displayed summaries.");
            Check(details.Cell(5, 1).DataType == XLDataType.DateTime && details.Cell(5, 1).GetDateTime() == created &&
                details.Cell(5, 3).GetString() == "0007" && details.Cell(5, 6).GetValue<int>() == 7 &&
                details.Cell(5, 10).GetString() == "W201, W124" && details.Cell(5, 12).GetString() == "00123 456",
                "Dates, customer codes, model details, alternative articles and quantities must survive a file round trip.");
            Check(details.Cell(5, 13).GetString() == "=SUM(1,2)" && !details.Cell(5, 13).HasFormula &&
                details.Cell(5, 14).GetDateTime() == created.AddMinutes(2) && details.Cell(5, 15).GetString() == "Да" &&
                details.Cell(6, 14).IsEmpty() && details.Cell(6, 15).GetString() == "Нет",
                "Notes must remain literal text, and submitted dates and processing statuses must be retained.");
        }
        Console.WriteLine("PASS: Excel file round trips for all 3 request kinds, grouped totals, full details, numeric quantities, leading zeros, dates, and literal notes");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
