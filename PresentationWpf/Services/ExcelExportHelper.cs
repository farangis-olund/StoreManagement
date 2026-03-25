using ClosedXML.Excel;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
namespace PresentationWpf.Services;

public static class ExcelExportHelper
{
    public static void ExportFromDataGrid(DataGrid grid, string defaultFileName)
    {
        if (grid.Items.Count == 0)
        {
            MessageBox.Show("Нет данных для экспорта.", "Excel",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = defaultFileName,
            Filter = "Excel file (*.xlsx)|*.xlsx"
        };

        if (dialog.ShowDialog() != true)
            return;

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");

        // ===== HEADERS =====
        for (int i = 0; i < grid.Columns.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = grid.Columns[i].Header?.ToString();
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        // ===== ROWS =====
        int row = 2;

        foreach (var item in grid.Items)
        {
            for (int col = 0; col < grid.Columns.Count; col++)
            {
                var column = grid.Columns[col] as DataGridBoundColumn;

                if (column?.Binding is System.Windows.Data.Binding binding)
                {
                    var prop = item.GetType().GetProperty(binding.Path.Path);

                    var value = prop?.GetValue(item);

                    sheet.Cell(row, col + 1).Value = value?.ToString();
                }
            }

            row++;
        }

        sheet.Columns().AdjustToContents();

        workbook.SaveAs(dialog.FileName);

        MessageBox.Show("Экспорт завершён.", "Excel",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}