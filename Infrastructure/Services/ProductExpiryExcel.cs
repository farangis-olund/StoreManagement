using ClosedXML.Excel;
using Infrastructure.Dtos;
using System.Globalization;

namespace Infrastructure.Services;

public static class ProductExpiryExcel
{
    private static string Header(string text) => string.Concat(text.ToLowerInvariant().Where(char.IsLetterOrDigit)).Replace('ё', 'е');
    private static readonly string[][] Aliases =
    [
        ["артикул", "articlenumber", "article", "sku"],
        ["датапроизводства", "датапроизводство", "производство", "manufacturedon", "manufacturedate"],
        ["годендо", "срокгодности", "датаистечения", "датаистечениясрока", "expireson", "expirydate", "expirationdate"],
        ["местонаскладе", "место", "складскоеместо", "warehouseplace", "location"],
        ["вналичии", "наличие", "количество", "остаток", "quantity", "qty"]
    ];

    public static ExpiryImportFile Read(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new ArgumentException("В файле нет листов.");
        var first = sheet.FirstRowUsed();
        if (first == null) throw new ArgumentException("Файл пуст.");
        var columns = Aliases.Select(a => first.CellsUsed().FirstOrDefault(c => a.Contains(Header(c.GetString())))?.Address.ColumnNumber ?? 0).ToArray();
        var hasHeader = columns.Any(x => x > 0);
        if (hasHeader && columns.Any(x => x == 0))
            throw new ArgumentException("Нужны столбцы: Артикул, Дата производства, Годен до, Место на складе, В наличии (количество). Скачайте новый шаблон.");
        if (!hasHeader) columns = [1, 2, 3, 4, 5];
        var rows = new List<ExpiryImportRow>(); var issues = new List<ExpiryImportIssue>();
        foreach (var row in sheet.RowsUsed().Where(x => x.RowNumber() >= first.RowNumber() + (hasHeader ? 1 : 0)))
        {
            var article = row.Cell(columns[0]).GetFormattedString(CultureInfo.InvariantCulture).Trim();
            try
            {
                var made = Date(row.Cell(columns[1])); var expires = Date(row.Cell(columns[2]));
                var place = row.Cell(columns[3]).GetString().Trim();
                var quantityCell = row.Cell(columns[4]);
                int quantity;
                if (quantityCell.DataType == XLDataType.Number && quantityCell.GetDouble() is var n && n >= 0 && n <= int.MaxValue && n == Math.Truncate(n))
                    quantity = (int)n;
                else if (!int.TryParse(quantityCell.GetString().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) || quantity < 0)
                    throw new ArgumentException("В столбце «В наличии» укажите целое количество не меньше 0.");
                ProductExpiryService.Validate(article, made, expires, place, quantity);
                rows.Add(new(row.RowNumber(), article, made, expires, place, quantity));
            }
            catch (ArgumentException ex) { issues.Add(new(row.RowNumber(), article, ex.Message)); }
        }
        return new(rows, issues);
    }

    private static DateTime Date(IXLCell cell)
    {
        if (cell.DataType == XLDataType.DateTime) return cell.GetDateTime().Date;
        if (cell.DataType == XLDataType.Number)
        {
            var n = cell.GetDouble();
            if (n >= 1 && n <= 2958465) return DateTime.FromOADate(n).Date;
        }
        var value = cell.GetString().Trim();
        if (DateTime.TryParseExact(value, ["d.M.yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "yyyy/MM/dd"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date.Date;
        throw new ArgumentException($"Некорректная дата в столбце {cell.Address.ColumnLetter}. Используйте дату Excel или ДД.ММ.ГГГГ.");
    }

    public static void SaveTemplate(string path)
    {
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Сроки годности");
        string[] headers = ["Артикул", "Дата производства", "Годен до", "Место на складе", "В наличии"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Range(1, 1, 1, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#343344");
        sheet.Range(1, 1, 1, 5).Style.Font.FontColor = XLColor.White;
        sheet.Column(1).Style.NumberFormat.Format = "@";
        sheet.Columns(2, 3).Style.DateFormat.Format = "dd.MM.yyyy";
        sheet.Column(5).Style.NumberFormat.Format = "0";
        sheet.Columns(1, 5).Width = 24; sheet.Column(4).Width = 30; sheet.SheetView.FreezeRows(1);
        workbook.SaveAs(path);
    }
}
