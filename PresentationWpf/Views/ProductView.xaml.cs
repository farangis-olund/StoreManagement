using Infrastructure.Dtos;
using PresentationWpf.Dtos;
using PresentationWpf.ViewModels;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Input;


namespace PresentationWpf.Views
{
    /// <summary>
    /// Interaction logic for ProductView.xaml
    /// </summary>
    public partial class ProductView : UserControl
    {
        public ProductView()
        {
            InitializeComponent();


        }
        private async void ProductsGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ProductViewModel vm)
                return;

            // --- Вставка из Excel (Ctrl+V) ---
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                string clipboardText = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(clipboardText)) return;

                var rows = clipboardText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (rows.Length == 0) return;

                foreach (var row in rows)
                {
                    var values = row.Split('\t'); // Excel использует табы
                    var article = values.ElementAtOrDefault(0)?.Trim();
                    if (string.IsNullOrWhiteSpace(article)) continue;

                    // ищем существующий продукт
                    var existing = vm.Products.FirstOrDefault(p => p.ArticleNumber == article);

                    if (existing != null)
                    {
                        // 🔄 обновляем существующую запись
                        existing.ProductName = values.ElementAtOrDefault(1) ?? existing.ProductName;
                        existing.Model = values.ElementAtOrDefault(2) ?? existing.Model;
                        existing.Marka = values.ElementAtOrDefault(3) ?? existing.Marka;
                        existing.Alternative = values.ElementAtOrDefault(4) ?? existing.Alternative;
                        existing.GroupName = values.ElementAtOrDefault(5) ?? existing.GroupName;   // ✅ name
                        existing.BrandName = values.ElementAtOrDefault(6) ?? existing.BrandName;   // ✅ name
                        existing.Quentity = int.TryParse(values.ElementAtOrDefault(7), out var q) ? q : existing.Quentity;
                        existing.WarehousePlace = values.ElementAtOrDefault(8) ?? existing.WarehousePlace;
                        existing.MinRemainingQuantity = int.TryParse(values.ElementAtOrDefault(9), out var m) ? m : existing.MinRemainingQuantity;
                        existing.RetailPriceEuro = decimal.TryParse(values.ElementAtOrDefault(10), out var r) ? r : existing.RetailPriceEuro;
                        existing.WholesalePriceEuro = decimal.TryParse(values.ElementAtOrDefault(11), out var w) ? w : existing.WholesalePriceEuro;
                        existing.ServicePriceEuro = decimal.TryParse(values.ElementAtOrDefault(12), out var s) ? s : existing.ServicePriceEuro;
                        existing.WholesalePrice1Euro = decimal.TryParse(values.ElementAtOrDefault(13), out var w1) ? w1 : existing.WholesalePrice1Euro;
                        existing.NetPrice = decimal.TryParse(values.ElementAtOrDefault(14), out var n) ? n : existing.NetPrice;
                        existing.SmallWholesalePrice = decimal.TryParse(values.ElementAtOrDefault(15), out var sw) ? sw : existing.SmallWholesalePrice;
                    }
                    else
                    {
                        // ➕ добавляем новый
                        var product = new Product
                        {
                            ArticleNumber = article,
                            ProductName = values.ElementAtOrDefault(1) ?? "",
                            Model = values.ElementAtOrDefault(2) ?? "",
                            Marka = values.ElementAtOrDefault(3) ?? "",
                            Alternative = values.ElementAtOrDefault(4) ?? "",
                            GroupName = values.ElementAtOrDefault(5) ?? "",   // ✅ name
                            BrandName = values.ElementAtOrDefault(6) ?? "",   // ✅ name
                            Quentity = int.TryParse(values.ElementAtOrDefault(7), out var q) ? q : 0,
                            WarehousePlace = values.ElementAtOrDefault(8) ?? "",
                            MinRemainingQuantity = int.TryParse(values.ElementAtOrDefault(9), out var m) ? m : 0,
                            RetailPriceEuro = decimal.TryParse(values.ElementAtOrDefault(10), out var r) ? r : 0,
                            WholesalePriceEuro = decimal.TryParse(values.ElementAtOrDefault(11), out var w) ? w : 0,
                            ServicePriceEuro = decimal.TryParse(values.ElementAtOrDefault(12), out var s) ? s : 0,
                            WholesalePrice1Euro = decimal.TryParse(values.ElementAtOrDefault(13), out var w1) ? w1 : 0,
                            NetPrice = decimal.TryParse(values.ElementAtOrDefault(14), out var n) ? n : 0,
                            SmallWholesalePrice = decimal.TryParse(values.ElementAtOrDefault(15), out var sw) ? sw : 0
                        };

                        vm.Products.Add(product);
                    }

                }

                e.Handled = true;
            }

            // --- Удаление выделенных строк (Delete) ---
            if (e.Key == Key.Delete)
            {
                var items = ProductsGrid.SelectedItems.Cast<Product>().ToList();
                if (items.Any())
                {
                    await vm.DeleteAsync(items);
                    e.Handled = true;
                }
            }
        }


    }
}
