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

			// === Paste from Excel (Ctrl+V) ===
			if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
			{
				string clipboardText = Clipboard.GetText();
				if (string.IsNullOrWhiteSpace(clipboardText)) return;

				var rows = clipboardText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
				if (rows.Length == 0) return;

				foreach (var row in rows)
				{
					var values = row.Split('\t'); // Excel uses tabs
					var article = values.ElementAtOrDefault(1)?.Trim(); // Артикул (2nd column)
					if (string.IsNullOrWhiteSpace(article)) continue;

					var existing = vm.Products.FirstOrDefault(p => p.ArticleNumber == article);

					// --- Update existing product ---
					if (existing != null)
					{
						existing.Numbering = int.TryParse(values.ElementAtOrDefault(0), out var num) ? num : existing.Numbering; // Нумерация
						existing.ProductName = values.ElementAtOrDefault(2) ?? existing.ProductName;              // Наименование
						existing.GroupName = values.ElementAtOrDefault(3) ?? existing.GroupName;                  // Группа
						existing.BrandName = values.ElementAtOrDefault(4) ?? existing.BrandName;                  // Бренд
						existing.Marka = values.ElementAtOrDefault(5) ?? existing.Marka;                          // Марка
						existing.Model = values.ElementAtOrDefault(6) ?? existing.Model;                          // Модель
						existing.Alternative = values.ElementAtOrDefault(7) ?? existing.Alternative;              // Альтернатива
						existing.Quentity = int.TryParse(values.ElementAtOrDefault(8), out var q) ? q : existing.Quentity;  // Количество
						existing.PriceLevel5 = decimal.TryParse(values.ElementAtOrDefault(9), out var r) ? r : existing.PriceLevel5;  // Розн
						existing.PriceLevel4 = decimal.TryParse(values.ElementAtOrDefault(10), out var w2) ? w2 : existing.PriceLevel4;  // Опт2
						existing.PriceLevel3 = decimal.TryParse(values.ElementAtOrDefault(11), out var s) ? s : existing.PriceLevel3;        // Серв
						existing.PriceLevel2 = decimal.TryParse(values.ElementAtOrDefault(12), out var w1) ? w1 : existing.PriceLevel2; // Опт1
						existing.PriceLevel1 = decimal.TryParse(values.ElementAtOrDefault(13), out var n) ? n : existing.PriceLevel1;                         // Цена Нето
						existing.MinRemainingQuantity = int.TryParse(values.ElementAtOrDefault(14), out var m) ? m : existing.MinRemainingQuantity;     // Мин_допустимое кол-во
						existing.WarehousePlace = values.ElementAtOrDefault(15) ?? existing.WarehousePlace;                                            // Место на складе
					}
					else
					{
						// --- Add new product ---
						var product = new Product
						{
							Numbering = int.TryParse(values.ElementAtOrDefault(0), out var num) ? num : 0,          // Нумерация
							ArticleNumber = article,                                                               // Артикул
							ProductName = values.ElementAtOrDefault(2) ?? "",                                      // Наименование
							GroupName = values.ElementAtOrDefault(3) ?? "",                                        // Группа
							BrandName = values.ElementAtOrDefault(4) ?? "",                                        // Бренд
							Marka = values.ElementAtOrDefault(5) ?? "",                                            // Марка
							Model = values.ElementAtOrDefault(6) ?? "",                                            // Модель
							Alternative = values.ElementAtOrDefault(7) ?? "",                                      // Альтернатива
							Quentity = int.TryParse(values.ElementAtOrDefault(8), out var q) ? q : 0,              // Количество
							PriceLevel5 = decimal.TryParse(values.ElementAtOrDefault(9), out var r) ? r : 0,   // Розн
							PriceLevel4 = decimal.TryParse(values.ElementAtOrDefault(10), out var w2) ? w2 : 0, // Опт2
							PriceLevel3 = decimal.TryParse(values.ElementAtOrDefault(11), out var s) ? s : 0, // Серв
							PriceLevel2 = decimal.TryParse(values.ElementAtOrDefault(12), out var w1) ? w1 : 0, // Опт1
							PriceLevel1 = decimal.TryParse(values.ElementAtOrDefault(13), out var n) ? n : 0,         // Цена Нето
							
							MinRemainingQuantity = int.TryParse(values.ElementAtOrDefault(14), out var m) ? m : 0, // Мин_допустимое кол-во
							WarehousePlace = values.ElementAtOrDefault(15) ?? ""                                   // Место на складе
						};

						vm.Products.Add(product);
					}
				}

				e.Handled = true;
			}

			// === Delete selected rows (Delete key) ===
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
