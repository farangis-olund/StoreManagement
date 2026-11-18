using ClosedXML.Excel;
using Infrastructure.Dtos;
using System.Globalization;

namespace Infrastructure.Helpers;

public static class WarehousePlaceExcelReader
{
    public static WarehousePlaceImportFile Read(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("В файле Excel нет листов.");
        var rows = new List<WarehousePlaceImportRow>();
        var issues = new List<WarehousePlaceImportIssue>();
        var first = true;
        var hasHeader = false;
        foreach (var row in sheet.RowsUsed(XLCellsUsedOptions.Contents))
        {
            ct.ThrowIfCancellationRequested();
            var article = "";
            var place = "";
            try
            {
                article = row.Cell(1).GetFormattedString(CultureInfo.InvariantCulture).Trim();
                place = row.Cell(2).GetFormattedString(CultureInfo.InvariantCulture).Trim();
                if (article.Length == 0 && place.Length == 0) continue;
                if (first)
                {
                    first = false;
                    if (IsArticleHeader(article) && IsPlaceHeader(place)) { hasHeader = true; continue; }
                }
                var reason = row.Cell(1).DataType == XLDataType.Error || row.Cell(2).DataType == XLDataType.Error ? "Ячейка содержит ошибку Excel."
                    : article.Length == 0 ? "Не указан артикул."
                    : article.Length > 50 ? "Артикул длиннее 50 символов."
                    : place.Length == 0 ? "Не указано место. Текущее место не изменено." : null;
                if (reason != null) issues.Add(new(row.RowNumber(), article, place, reason));
                else rows.Add(new(row.RowNumber(), article, place));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                first = false;
                issues.Add(new(row.RowNumber(), article, place, "Не удалось прочитать значения ячеек."));
            }
        }
        if (rows.Count == 0 && issues.Count == 0)
            throw new InvalidDataException("В первых двух столбцах Excel нет данных: артикул, место.");
        return new(rows, issues, hasHeader);
    }

    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static bool IsArticleHeader(string value) => Normalize(value) is "артикул" or "артикултовара" or "кодтовара" or "article" or "articlenumber";
    private static bool IsPlaceHeader(string value) => Normalize(value) is "место" or "местонаскладе" or "складскоеместо" or "warehouseplace" or "location";
}
