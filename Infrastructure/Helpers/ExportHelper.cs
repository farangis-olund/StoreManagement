
using ClosedXML.Excel;
using Infrastructure.Services;
using System.Data;
using System.Globalization;


namespace Infrastructure.Helpers;

public class ExportHelper
{
    private readonly OrganizationInfoService _organization;

    public ExportHelper(OrganizationInfoService organization)
    {
        _organization = organization;
    }

    public async Task<bool> ExportExcel(DataTable dt, string date)
    {
        if (!DateTime.TryParse(date, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsedDate))
            return false;

        return await ExportExcel(dt, parsedDate);
    }

    // Prefer this overload when you already have a DateTime
    public async Task<bool> ExportExcel(DataTable dt, DateTime date)
    {
        if (dt is null || dt.Columns.Count == 0) return false;

        var org = await _organization.GetAsync();
        if (org is null || string.IsNullOrWhiteSpace(org.ExportPath)) return false;

        var exportDir = org.ExportPath;
        Directory.CreateDirectory(exportDir);

        var baseName = $"{org.OrganizationCode}_{date:dd.MM.yyyy}.xlsx";
        var fullPath = GetUniquePath(Path.Combine(exportDir, baseName));

        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(dt, "Лист1");   // creates a Table automatically

            // Header/freeze/size
            ws.Row(1).Style.Font.Bold = true;          // optional (table already bolds header)
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();

            // Use the table's own AutoFilter instead of RangeUsed().SetAutoFilter()
            var tbl = ws.Tables.FirstOrDefault();
            if (tbl != null)
            {
                tbl.ShowAutoFilter = true;             // OK for table
            }
            // else: if there is no table for some reason, you could fallback:
            // ws.RangeUsed().SetAutoFilter();

            // Right-align numeric columns
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                var type = dt.Columns[i].DataType;
                if (type == typeof(int) || type == typeof(long) ||
                    type == typeof(decimal) || type == typeof(double) || type == typeof(float))
                {
                    ws.Column(i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
            }

            wb.SaveAs(fullPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path)) return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        int i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name} ({i++}){ext}");
        } while (File.Exists(candidate));
        return candidate;
    }
}
