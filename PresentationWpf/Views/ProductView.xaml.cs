using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Infrastructure.Dtos;
using PresentationWpf.ViewModels;

namespace PresentationWpf.Views
{
    public partial class ProductView : UserControl
    {
        public ProductView()
        {
            InitializeComponent();
        }

        private static void PasteMultipleColumns(ProductViewModel vm, string clipboardText, int startRowIndex, int startColumnIndex)
        {
            if (string.IsNullOrWhiteSpace(clipboardText))
                return;

            var rows = clipboardText
                .Replace("\r\n", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            for (int r = 0; r < rows.Length; r++)
            {
                var values = rows[r].Split('\t');
                int targetRowIndex = startRowIndex + r;

                while (targetRowIndex >= vm.Products.Count)
                    vm.Products.Add(new Product());

                var product = vm.Products[targetRowIndex];

                for (int c = 0; c < values.Length; c++)
                {
                    int targetColumnIndex = startColumnIndex + c;
                    string value = CleanClipboardValue(values[c]);

                    switch (targetColumnIndex)
                    {
                        case 0:
                            if (TryParseInt(value, out var numbering))
                                product.Numbering = numbering;
                            break;

                        case 1:
                            product.ArticleNumber = value;
                            break;

                        case 2:
                            product.ProductName = value;
                            break;

                        case 3:
                            product.GroupName = value;
                            break;

                        case 4:
                            product.BrandName = value;
                            break;

                        case 5:
                            product.Marka = value;
                            break;

                        case 6:
                            product.Model = value;
                            break;

                        case 7:
                            product.Alternative = value;
                            break;

                        case 8:
                            if (TryParseInt(value, out var q))
                                product.Quentity = q;
                            break;

                        case 9:
                            if (TryParseDecimal(value, out var p5))
                                product.PriceLevel5 = p5;
                            break;

                        case 10:
                            if (TryParseDecimal(value, out var p4))
                                product.PriceLevel4 = p4;
                            break;

                        case 11:
                            if (TryParseDecimal(value, out var p3))
                                product.PriceLevel3 = p3;
                            break;

                        case 12:
                            if (TryParseDecimal(value, out var p2))
                                product.PriceLevel2 = p2;
                            break;

                        case 13:
                            if (TryParseDecimal(value, out var p1))
                                product.PriceLevel1 = p1;
                            break;

                        case 14:
                            if (TryParseInt(value, out var min))
                                product.MinRemainingQuantity = min;
                            break;

                        case 15:
                            product.WarehousePlace = value;
                            break;
                    }
                }
            }
        }

        private static string CleanClipboardValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            value = value.Trim();

            // remove outer quotes: "7,03" -> 7,03
            if (value.Length >= 2 && value.StartsWith("\"") && value.EndsWith("\""))
                value = value.Substring(1, value.Length - 2);

            // unescape double quotes
            value = value.Replace("\"\"", "\"");

            return value.Trim();
        }

        private static bool TryParseDecimal(string value, out decimal result)
        {
            value = CleanClipboardValue(value);

            return decimal.TryParse(
                       value,
                       System.Globalization.NumberStyles.Any,
                       System.Globalization.CultureInfo.CurrentCulture,
                       out result)
                   ||
                   decimal.TryParse(
                       value,
                       System.Globalization.NumberStyles.Any,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out result);
        }

        private static bool TryParseInt(string value, out int result)
        {
            value = CleanClipboardValue(value);

            return int.TryParse(
                       value,
                       System.Globalization.NumberStyles.Any,
                       System.Globalization.CultureInfo.CurrentCulture,
                       out result)
                   ||
                   int.TryParse(
                       value,
                       System.Globalization.NumberStyles.Any,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out result);
        }

        private static string GetCellValue(Product? product, int columnIndex)
        {
            if (product == null)
                return "";

            return columnIndex switch
            {
                0 => product.Numbering.ToString(),
                1 => product.ArticleNumber ?? "",
                2 => product.ProductName ?? "",
                3 => product.GroupName ?? "",
                4 => product.BrandName ?? "",
                5 => product.Marka ?? "",
                6 => product.Model ?? "",
                7 => product.Alternative ?? "",
                8 => product.Quentity.ToString(),
                9 => product.PriceLevel5.ToString(),
                10 => product.PriceLevel4.ToString(),
                11 => product.PriceLevel3.ToString(),
                12 => product.PriceLevel2.ToString(),
                13 => product.PriceLevel1.ToString(),
                14 => product.MinRemainingQuantity.ToString(),
                15 => product.WarehousePlace ?? "",
                _ => ""
            };
        }

        private void CopySelectedColumns_Click(object sender, RoutedEventArgs e)
        {
            var selectedCells = ProductsGrid.SelectedCells;

            if (selectedCells == null || selectedCells.Count == 0)
            {
                MessageBox.Show("Выберите ячейки или столбцы для копирования.");
                return;
            }

            var cells = selectedCells
                .Where(x => x.Item is Product && x.Column != null)
                .OrderBy(x => ProductsGrid.Items.IndexOf(x.Item))
                .ThenBy(x => x.Column.DisplayIndex)
                .ToList();

            if (cells.Count == 0)
                return;

            var groupedRows = cells
                .GroupBy(x => ProductsGrid.Items.IndexOf(x.Item))
                .OrderBy(g => g.Key);

            var sb = new StringBuilder();

            foreach (var rowGroup in groupedRows)
            {
                var rowCells = rowGroup
                    .OrderBy(x => x.Column.DisplayIndex)
                    .ToList();

                for (int i = 0; i < rowCells.Count; i++)
                {
                    var cell = rowCells[i];
                    var product = cell.Item as Product;
                    var value = GetCellValue(product, cell.Column.DisplayIndex);

                    sb.Append(EscapeForSpreadsheet(value));

                    if (i < rowCells.Count - 1)
                        sb.Append('\t');
                }

                sb.Append("\r\n");
            }

            Clipboard.SetText(sb.ToString());
        }

        private static string EscapeForSpreadsheet(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            // escape quotes inside text
            value = value.Replace("\"", "\"\"");

            // always wrap in quotes so decimal commas stay inside one cell
            return $"\"{value}\"";
        }

        private void PasteColumns_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ProductViewModel vm)
                return;

            string clipboardText = Clipboard.GetText(TextDataFormat.UnicodeText);
            if (string.IsNullOrWhiteSpace(clipboardText))
                clipboardText = Clipboard.GetText();

            if (string.IsNullOrWhiteSpace(clipboardText))
            {
                MessageBox.Show("Буфер обмена пуст.");
                return;
            }

            int startRowIndex = ProductsGrid.Items.IndexOf(ProductsGrid.CurrentItem);
            if (startRowIndex < 0)
                startRowIndex = 0;

            int startColumnIndex = ProductsGrid.CurrentCell.Column?.DisplayIndex ?? 0;

            PasteMultipleColumns(vm, clipboardText, startRowIndex, startColumnIndex);

            ProductsGrid.Items.Refresh();
            vm.ProductsView.Refresh();
        }

        private async void ProductsGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ProductViewModel vm)
                return;

            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                string clipboardText = Clipboard.GetText(TextDataFormat.UnicodeText);
                if (string.IsNullOrWhiteSpace(clipboardText))
                    clipboardText = Clipboard.GetText();

                if (string.IsNullOrWhiteSpace(clipboardText))
                    return;

                int startRowIndex = ProductsGrid.Items.IndexOf(ProductsGrid.CurrentItem);
                if (startRowIndex < 0)
                    startRowIndex = 0;

                int startColumnIndex = ProductsGrid.CurrentCell.Column?.DisplayIndex ?? 0;

                PasteMultipleColumns(vm, clipboardText, startRowIndex, startColumnIndex);

                ProductsGrid.Items.Refresh();
                vm.ProductsView.Refresh();
                RefreshRowNumbers();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete)
            {
                var items = ProductsGrid.SelectedItems.Cast<Product>().ToList();
                if (items.Any())
                {
                    await vm.DeleteAsync(items);
                    RefreshRowNumbers();
                    e.Handled = true;
                }
            }
        }

        private void ColumnHeader_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridColumnHeader header || header.Column == null)
                return;

            var grid = ProductsGrid;
            int columnIndex = header.Column.DisplayIndex;

            bool isCtrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (!isCtrl)
                grid.SelectedCells.Clear();

            foreach (var item in grid.Items)
            {
                if (item == CollectionView.NewItemPlaceholder)
                    continue;

                var cellInfo = new DataGridCellInfo(item, grid.Columns[columnIndex]);

                if (!grid.SelectedCells.Contains(cellInfo))
                    grid.SelectedCells.Add(cellInfo);
            }

            var firstItem = grid.Items
                .Cast<object>()
                .FirstOrDefault(x => x != CollectionView.NewItemPlaceholder);

            if (firstItem != null)
                grid.CurrentCell = new DataGridCellInfo(firstItem, grid.Columns[columnIndex]);

            e.Handled = true;
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void RefreshRowNumbers()
        {
            ProductsGrid.Dispatcher.InvokeAsync(() =>
            {
                for (int i = 0; i < ProductsGrid.Items.Count; i++)
                {
                    var row = ProductsGrid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow;

                    if (row != null)
                        row.Header = (i + 1).ToString();
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}