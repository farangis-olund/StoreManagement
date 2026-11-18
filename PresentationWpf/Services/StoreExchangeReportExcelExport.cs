using ClosedXML.Excel;
using Infrastructure.Dtos;

namespace PresentationWpf.Services;

public static class StoreExchangeReportExcelExport
{
    public static XLWorkbook Create(IReadOnlyList<StoreExchangeReportRow> rows, string shop, DateTime asOf)
    {
        var workbook = new XLWorkbook();
        try
        {
            var sheet = workbook.AddWorksheet("Обмен товарами");
            sheet.Range("A1:F1").Merge().Value = "Об обмене товаров магазинами";
            sheet.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16);
            sheet.Range("A2:F2").Merge().Value = $"Магазин: {shop}. Данные на {asOf:dd.MM.yyyy HH:mm}";
            sheet.Range("A3:F3").Merge().Value = "Все товары; незакрытые обмены. Остаток = на складе - приход + расход.";
            string[] headers = ["№", "Артикул", "Кол-во на складе", "Кол-во приходов", "Кол-во расходов", "Остаток"];
            for (var i = 0; i < headers.Length; i++) sheet.Cell(5, i + 1).Value = headers[i];
            sheet.Range("A5:F5").Style.Fill.BackgroundColor = XLColor.FromHtml("#566366");
            sheet.Range("A5:F5").Style.Font.SetBold().Font.FontColor = XLColor.White;
            sheet.Column(2).Style.NumberFormat.Format = "@";
            var line = 6;
            foreach (var row in rows)
            {
                sheet.Cell(line, 1).Value = row.Number;
                sheet.Cell(line, 2).Value = row.ArticleNumber;
                sheet.Cell(line, 3).Value = row.WarehouseQuantity;
                sheet.Cell(line, 4).Value = row.IncomingQuantity;
                sheet.Cell(line, 5).Value = row.OutgoingQuantity;
                sheet.Cell(line, 6).Value = row.RemainingQuantity;
                line++;
            }
            sheet.Cell(line, 2).Value = "Итого";
            sheet.Cell(line, 3).Value = rows.Sum(x => x.WarehouseQuantity);
            sheet.Cell(line, 4).Value = rows.Sum(x => x.IncomingQuantity);
            sheet.Cell(line, 5).Value = rows.Sum(x => x.OutgoingQuantity);
            sheet.Cell(line, 6).Value = rows.Sum(x => x.RemainingQuantity);
            sheet.Range(line, 1, line, 6).Style.Font.SetBold();
            sheet.Range(5, 1, Math.Max(5, line - 1), 6).SetAutoFilter();
            sheet.Range(5, 3, line, 6).Style.NumberFormat.Format = "#,##0";
            sheet.Range(5, 1, line, 6).Style.Alignment.WrapText = true;
            sheet.Columns(1, 6).AdjustToContents(5, line, 8, 28);
            sheet.Column(2).Width = 28;
            sheet.Rows(5, line).AdjustToContents();
            sheet.SheetView.FreezeRows(5);
            sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            sheet.PageSetup.FitToPages(1, 0);
            sheet.PageSetup.SetRowsToRepeatAtTop(5, 5);
            return workbook;
        }
        catch { workbook.Dispose(); throw; }
    }
}
