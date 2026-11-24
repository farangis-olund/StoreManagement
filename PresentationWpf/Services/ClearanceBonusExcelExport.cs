using ClosedXML.Excel;
using Infrastructure.Dtos;

namespace PresentationWpf.Services;

public static class ClearanceBonusExcelExport
{
    public static XLWorkbook Create(ClearanceBonusReport report, string shop)
    {
        var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Бонусы за распродажу");
        sheet.Range(1, 1, 1, 5).Merge().Value = "БОНУСЫ ЗА РАСПРОДАЖУ";
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Range(2, 1, 2, 5).Merge().Value = $"Магазин: {shop}";
        sheet.Range(3, 1, 3, 5).Merge().Value = $"Период: {report.FromDate:dd.MM.yyyy} — {report.ToDate:dd.MM.yyyy} · Бонус: {report.Percent:0.##}%";
        sheet.Range(4, 1, 4, 5).Merge().Value = "Продажи без вычета возвратов.";
        var headers = new[] { "Продавец", "Артикул", "Количество", "Сумма", "Бонус" };
        for (var i = 0; i < headers.Length; i++) sheet.Cell(6, i + 1).Value = headers[i];
        sheet.Range(6, 1, 6, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#E9EDF2");
        sheet.Range(6, 1, 6, 5).Style.Font.Bold = true;
        var row = 7;
        foreach (var seller in report.Rows.GroupBy(x => new { x.UserId, x.SellerName }))
        {
            foreach (var line in seller)
            {
                sheet.Cell(row, 1).Value = line.SellerName;
                sheet.Cell(row, 2).Style.NumberFormat.Format = "@";
                sheet.Cell(row, 2).Value = line.ArticleNumber;
                sheet.Cell(row, 3).Value = line.Quantity;
                sheet.Cell(row, 4).Value = line.Amount;
                sheet.Cell(row, 5).Value = line.Bonus;
                row++;
            }
            sheet.Range(row, 1, row, 2).Merge().Value = "Итого: " + seller.Key.SellerName;
            sheet.Cell(row, 3).Value = seller.Sum(x => x.Quantity);
            sheet.Cell(row, 4).Value = seller.Sum(x => x.Amount);
            sheet.Cell(row, 5).Value = seller.Sum(x => x.Bonus);
            sheet.Range(row, 1, row, 5).Style.Font.Bold = true;
            row++;
        }
        sheet.Range(row, 1, row, 2).Merge().Value = "Всего";
        sheet.Cell(row, 3).Value = report.TotalQuantity;
        sheet.Cell(row, 4).Value = report.TotalAmount;
        sheet.Cell(row, 5).Value = report.TotalBonus;
        sheet.Range(row, 1, row, 5).Style.Font.Bold = true;
        sheet.Range(7, 4, row, 5).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(1).Width = 30; sheet.Column(2).Width = 24;
        sheet.Column(3).Width = 16; sheet.Columns(4, 5).Width = 20;
        sheet.SheetView.FreezeRows(6);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        sheet.PageSetup.FitToPages(1, 0);
        return workbook;
    }
}
