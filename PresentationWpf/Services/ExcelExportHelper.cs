using ClosedXML.Excel;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace PresentationWpf.Services;

public static class ExcelExportHelper
{
    //public static void ExportFromDataGrid(DataGrid grid, string defaultFileName)
    //{
    //    if (grid.Items.Count == 0)
    //    {
    //        MessageBox.Show("Нет данных для экспорта.", "Excel",
    //            MessageBoxButton.OK, MessageBoxImage.Information);
    //        return;
    //    }

    //    var dialog = new SaveFileDialog
    //    {
    //        FileName = defaultFileName,
    //        Filter = "Excel file (*.xlsx)|*.xlsx"
    //    };

    //    if (dialog.ShowDialog() != true)
    //        return;

    //    using var workbook = new XLWorkbook();
    //    var sheet = workbook.Worksheets.Add("Report");

    //    // ===== HEADERS =====
    //    for (int i = 0; i < grid.Columns.Count; i++)
    //    {
    //        sheet.Cell(1, i + 1).Value = grid.Columns[i].Header?.ToString();
    //        sheet.Cell(1, i + 1).Style.Font.Bold = true;
    //    }

    //    // ===== ROWS =====
    //    int row = 2;

    //    foreach (var item in grid.Items)
    //    {
    //        for (int col = 0; col < grid.Columns.Count; col++)
    //        {
    //            var column = grid.Columns[col] as DataGridBoundColumn;

    //            if (column?.Binding is System.Windows.Data.Binding binding)
    //            {
    //                var prop = item.GetType().GetProperty(binding.Path.Path);

    //                var value = prop?.GetValue(item);

    //                sheet.Cell(row, col + 1).Value = value?.ToString();
    //            }
    //        }

    //        row++;
    //    }

    //    sheet.Columns().AdjustToContents();

    //    workbook.SaveAs(dialog.FileName);

    //    MessageBox.Show("Экспорт завершён.", "Excel",
    //        MessageBoxButton.OK, MessageBoxImage.Information);
    //}

    public static void ExportFromDataGrid(DataGrid grid, string defaultFileName)
    {
        if (grid == null || grid.Items.Count == 0)
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

        var exportColumns = grid.Columns
            .Where(c => c.Visibility == Visibility.Visible)
            .OrderBy(c => c.DisplayIndex)
            .ToList();

        // ===== HEADERS =====
        for (int i = 0; i < exportColumns.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = exportColumns[i].Header?.ToString() ?? "";
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        // ===== ROWS =====
        int row = 2;

        foreach (var item in grid.Items)
        {
            if (item == CollectionView.NewItemPlaceholder)
                continue;

            for (int col = 0; col < exportColumns.Count; col++)
            {
                var column = exportColumns[col];
                var value = GetCellExportValue(column, item);

                if (value is decimal dec)
                    sheet.Cell(row, col + 1).Value = dec;
                else if (value is double dbl)
                    sheet.Cell(row, col + 1).Value = dbl;
                else if (value is float fl)
                    sheet.Cell(row, col + 1).Value = fl;
                else if (value is int i)
                    sheet.Cell(row, col + 1).Value = i;
                else if (value is long l)
                    sheet.Cell(row, col + 1).Value = l;
                else if (value is DateTime dt)
                    sheet.Cell(row, col + 1).Value = dt;
                else
                    sheet.Cell(row, col + 1).Value = value?.ToString() ?? "";
            }

            row++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(dialog.FileName);

        MessageBox.Show("Экспорт завершён.", "Excel",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static object? GetCellExportValue(DataGridColumn column, object item)
    {
        // 1) Standard bound columns
        if (column is DataGridBoundColumn boundColumn &&
            boundColumn.Binding is Binding binding)
        {
            return EvaluateBinding(item, binding);
        }

        // 2) Template columns (best effort)
        if (column is DataGridTemplateColumn templateColumn)
        {
            var content = templateColumn.CellTemplate?.LoadContent();

            if (content is FrameworkElement fe)
            {
                fe.DataContext = item;

                // Try common controls
                if (fe is TextBlock tb)
                    return tb.Text;

                if (fe is ContentPresenter cp)
                {
                    cp.Content = item;
                    cp.ApplyTemplate();
                }

                // Search child TextBlock after bindings apply
                fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                fe.Arrange(new Rect(0, 0, fe.DesiredSize.Width, fe.DesiredSize.Height));
                fe.UpdateLayout();

                var textBlock = FindVisualChild<TextBlock>(fe);
                if (textBlock != null)
                    return textBlock.Text;

                var checkBox = FindVisualChild<CheckBox>(fe);
                if (checkBox != null)
                    return checkBox.IsChecked;

                var textBox = FindVisualChild<TextBox>(fe);
                if (textBox != null)
                    return textBox.Text;
            }
        }

        return null;
    }

    private static object? EvaluateBinding(object dataItem, Binding sourceBinding)
    {
        var target = new TextBlock
        {
            DataContext = dataItem
        };

        var binding = CloneBinding(sourceBinding);

        BindingOperations.SetBinding(target, TextBlock.TextProperty, binding);

        // If binding produces text, return text
        var text = target.Text;
        if (!string.IsNullOrWhiteSpace(text))
            return text;

        return text;
    }

    private static Binding CloneBinding(Binding original)
    {
        return new Binding
        {
            Path = original.Path,
            XPath = original.XPath,
            Mode = original.Mode,
            UpdateSourceTrigger = original.UpdateSourceTrigger,
            Converter = original.Converter,
            ConverterCulture = original.ConverterCulture,
            ConverterParameter = original.ConverterParameter,
            StringFormat = original.StringFormat,
            TargetNullValue = original.TargetNullValue,
            FallbackValue = original.FallbackValue,
            BindsDirectlyToSource = original.BindsDirectlyToSource,
            ValidatesOnDataErrors = original.ValidatesOnDataErrors,
            ValidatesOnExceptions = original.ValidatesOnExceptions,
            NotifyOnValidationError = original.NotifyOnValidationError,
            NotifyOnSourceUpdated = original.NotifyOnSourceUpdated,
            NotifyOnTargetUpdated = original.NotifyOnTargetUpdated
        };
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null)
            return null;

        int count = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T match)
                return match;

            var nested = FindVisualChild<T>(child);
            if (nested != null)
                return nested;
        }

        return null;
    }
}