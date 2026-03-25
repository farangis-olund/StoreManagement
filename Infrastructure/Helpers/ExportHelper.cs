
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
            var ws = wb.Worksheets.Add(dt, "запрос_склад");   // creates a Table automatically
            var tbl = ws.Tables.FirstOrDefault();
            if (tbl != null)
            {
                tbl.Theme = XLTableTheme.None;       // removes blue style
                tbl.ShowAutoFilter = false;          // hides filter arrows
            }

            ws.Columns().AdjustToContents();
                     
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
            ws.Style.Font.FontColor = XLColor.White;
            ws.Protect("MyStrongPassword123");
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

    // ========================
    // IMPORT
    // ========================

    public DataTable? ImportExcel(string filePath, string sheetName = "Sheet1")
    {
        
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == sheetName)
                         ?? workbook.Worksheets.First();

            var dt = new DataTable();
            bool firstRow = true;

            foreach (var row in worksheet.RowsUsed())
            {
                if (firstRow)
                {
                    foreach (var cell in row.Cells())
                        dt.Columns.Add(cell.Value.ToString());
                    firstRow = false;
                }
                else
                {
                    var dataRow = dt.NewRow();
                    int i = 0;
                    foreach (var cell in row.Cells(1, dt.Columns.Count))
                        dataRow[i++] = cell.Value.ToString();
                    dt.Rows.Add(dataRow);
                }
            }

            return dt;
        }
        catch
        {
            return null;
        }
    }
}
