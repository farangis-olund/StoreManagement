using ClosedXML.Excel;
using Infrastructure.Dtos;

namespace PresentationWpf.Services;

public static class ProductRequestExcelExport
{
    public static XLWorkbook Create(string title, string filterDescription, IReadOnlyList<ProductRequestSummary> rows)
    {
        var workbook = new XLWorkbook();
        try
        {
            var summary = CreateSheet(workbook, "Сводка", title, filterDescription,
                ["Артикул", "Наименование", "Бренд", "Количество", "Заявок"]);
            summary.Column(1).Style.NumberFormat.Format = "@";
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var number = i + 5;
                summary.Cell(number, 1).Value = row.ArticleNumber;
                summary.Cell(number, 2).Value = row.ProductName;
                summary.Cell(number, 3).Value = row.BrandName;
                summary.Cell(number, 4).Value = row.Quantity;
                summary.Cell(number, 5).Value = row.RequestCount;
            }
            FinishSheet(summary, rows.Count, 5);
            var totalsRow = rows.Count + 5;
            summary.Cell(totalsRow, 1).Value = "Всего";
            summary.Cell(totalsRow, 4).Value = rows.Sum(row => row.Quantity);
            summary.Cell(totalsRow, 5).Value = rows.Sum(row => (long)row.RequestCount);
            summary.Range(totalsRow, 1, totalsRow, 5).Style.Font.Bold = true;

            var details = CreateSheet(workbook, "Детали заявок", title, filterDescription,
                ["Дата", "Пользователь", "Код клиента", "Клиент", "Артикул", "Количество", "Наименование",
                 "Бренд", "Марка", "Модель", "Группа", "Альтернативный артикул", "Примечание", "Отправлена", "Обработано"]);
            foreach (var column in new[] { 3, 5, 12 }) details.Column(column).Style.NumberFormat.Format = "@";
            foreach (var column in new[] { 1, 14 }) details.Column(column).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
            var detailNumber = 5;
            foreach (var request in rows.SelectMany(row => row.Details))
            {
                details.Cell(detailNumber, 1).Value = request.CreatedAt;
                details.Cell(detailNumber, 2).Value = request.UserName;
                details.Cell(detailNumber, 3).Value = request.CustomerId;
                details.Cell(detailNumber, 4).Value = request.CustomerName;
                details.Cell(detailNumber, 5).Value = request.ArticleNumber;
                details.Cell(detailNumber, 6).Value = request.Quantity;
                details.Cell(detailNumber, 7).Value = request.ProductName;
                details.Cell(detailNumber, 8).Value = request.BrandName;
                details.Cell(detailNumber, 9).Value = request.Marka;
                details.Cell(detailNumber, 10).Value = request.Model;
                details.Cell(detailNumber, 11).Value = request.GroupName;
                details.Cell(detailNumber, 12).Value = request.Alternative;
                details.Cell(detailNumber, 13).Value = request.Notes;
                if (request.SubmittedAt.HasValue) details.Cell(detailNumber, 14).Value = request.SubmittedAt.Value;
                details.Cell(detailNumber, 15).Value = request.IsProcessed ? "Да" : "Нет";
                detailNumber++;
            }
            FinishSheet(details, detailNumber - 5, 15);
            return workbook;
        }
        catch
        {
            workbook.Dispose();
            throw;
        }
    }

    private static IXLWorksheet CreateSheet(XLWorkbook workbook, string name, string title, string filters, string[] headers)
    {
        var sheet = workbook.Worksheets.Add(name);
        sheet.Range(1, 1, 1, headers.Length).Merge();
        sheet.Cell(1, 1).Value = $"Заявки на товары — {title}";
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Range(2, 1, 2, headers.Length).Merge();
        sheet.Cell(2, 1).Value = filters;
        sheet.Range(4, 1, 4, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#566366");
        sheet.Range(4, 1, 4, headers.Length).Style.Font.FontColor = XLColor.White;
        sheet.Range(4, 1, 4, headers.Length).Style.Font.Bold = true;
        for (var i = 0; i < headers.Length; i++) sheet.Cell(4, i + 1).Value = headers[i];
        sheet.SheetView.FreezeRows(4);
        return sheet;
    }

    private static void FinishSheet(IXLWorksheet sheet, int count, int columns)
    {
        sheet.Range(4, 1, count + 4, columns).SetAutoFilter();
        sheet.Columns(1, columns).AdjustToContents(4, count + 4, 12, 45);
        sheet.Range(4, 1, count + 4, columns).Style.Alignment.WrapText = true;
    }
}
