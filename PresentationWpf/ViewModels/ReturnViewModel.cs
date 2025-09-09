using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Utilities;

namespace PresentationWpf.ViewModels
{
	public sealed class ProductPickItem
	{
		public string Article { get; init; } = "";
		public string Display { get; init; } = "";
		public decimal? ListPrice { get; init; }
		public override string ToString() => Display;
	}

	public partial class ReturnViewModel : ObservableObject
	{
		private readonly ProductService _productService;
		private readonly CustomerService _customerService;
		private readonly OrderService _orderService;
		private readonly ReturnService _returnService;


		public ReturnViewModel(ProductService productService, CustomerService customerService, OrderService orderService, ReturnService returnService)
		{
			_productService = productService;
			_customerService = customerService;
			_orderService = orderService;
			_returnService = returnService;
			
			Lines.CollectionChanged += (_, __) => RecalcTotals();

			SearchResultsView = CollectionViewSource.GetDefaultView(SearchResults);
			SearchResultsView.Filter = FilterOrders;
		}

		// ===== SEARCH (ORDERS) ===============================================

		

		[ObservableProperty] private DateTime? _searchFrom;
		[ObservableProperty] private DateTime? _searchTo;

		// ===== SEARCH (ORDERS) ===============================================

		[ObservableProperty] private string _searchText = string.Empty; // <--- добавил
		[ObservableProperty] private bool _searchExpanded;              // <--- добавил

		public ObservableCollection<SearchHitRow> SearchResults { get; } = [];
		public ICollectionView SearchResultsView { get; private set; } = null!;

		private bool FilterOrders(object item)
		{
			if (item is not SearchHitRow o) return false;

			// 🚩 Если строка поиска пустая → не показывать ничего
			if (string.IsNullOrWhiteSpace(SearchText))
				return false;

			var words = SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

			bool ContainsWord(string word) =>
				(o.OrderId?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false) ||
				(o.CustomerName?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false) ||
				(o.Article?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false) ||
				(o.ProductName?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false) ||
				(o.Brand?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false) ||
				(o.Model?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false);

			// 🚩 "И" — все слова должны совпасть
			return words.All(ContainsWord);
		}

		partial void OnSearchTextChanged(string value) => ApplyFilter();

		private void ApplyFilter() => SearchResultsView?.Refresh();

		// ===== INIT =============================================================
		public async Task InitializeAsync()
		{
			// Customers
			CustomersFiltered.Clear();
			foreach (var c in await _customerService.GetAllCustomersAsync())
				CustomersFiltered.Add(new CustomerLite { Id = c.Id, FullName = c.FullName });

			// Products
			ProductsLookup.Clear();
			foreach (var p in await _productService.GetAllProductAsync())
				ProductsLookup.Add(new ProductPickItem { Article = p.ArticleNumber, Display = p.Display, ListPrice = p.RetailPriceEuro });

			// Orders
			await LoadOrdersAsync();
		}

		[RelayCommand]
		private async Task Loaded()
		{
			await InitializeAsync();
		}

		partial void OnSearchFromChanged(DateTime? value) => _ = LoadOrdersAsync();
		partial void OnSearchToChanged(DateTime? value) => _ = LoadOrdersAsync();

		// ===== MANUAL MODE ===================================================
		[ObservableProperty] private bool manualMode;
		public ObservableCollection<CustomerLite> CustomersFiltered { get; } = new();
		public ObservableCollection<ProductPickItem> ProductsLookup { get; } = new();

		[ObservableProperty] private string? manualCustomerId;
		[ObservableProperty] private ProductPickItem? selectedManualProduct;
		[ObservableProperty] private decimal manualQty = 1;
		private bool _manualPriceTouched;
		[ObservableProperty] private decimal manualPrice;
		partial void OnManualPriceChanged(decimal value) => _manualPriceTouched = true;

		partial void OnSelectedManualProductChanged(ProductPickItem? value)
			=> _ = PrefillManualPriceAsync(value);

		private async Task PrefillManualPriceAsync(ProductPickItem? item)
		{
			if (item is null) return;

			_manualPriceTouched = false;

			decimal price = item.ListPrice ?? await _productService.GetDefaultPriceAsync(item.Article);
			if (!_manualPriceTouched || ManualPrice <= 0) ManualPrice = price;
			if (ManualQty <= 0) ManualQty = 1;
		}

		// ===== LINES GRID ====================================================
		public ObservableCollection<ReturnLineRow> Lines { get; } = new();
		[ObservableProperty] private decimal subtotal;
		partial void OnSubtotalChanged(decimal value) => RefundAmount = value;
		[ObservableProperty] private decimal refundAmount;

		// ===== META ==========================================================
		[ObservableProperty] private string? reason;
		[ObservableProperty] private string refundMethod = "cash";
		[ObservableProperty] private string? comment;


		[RelayCommand]
		private async Task LoadOrdersAsync()
		{
			SearchResults.Clear();

			IEnumerable<OrderRowDto> orders;
			if (SearchFrom is null && SearchTo is null)
				orders = await _orderService.GetOrdersAsync();
			else
				orders = await _orderService.GetOrdersInRangeAsync(SearchFrom, SearchTo);

			foreach (var o in orders)
			{
				SearchResults.Add(new SearchHitRow
				{
					Date = o.Date,
					OrderId = o.OrderId,
					CustomerName = o.CustomerName,
					Article = o.Article,
					ProductName = o.ProductName,
					Brand = o.Brand,
					Model = o.Model,
					Quantity = o.Quantity,
					Price = o.Price
				});
			}

			if (SearchResultsView == null)
			{
				SearchResultsView = CollectionViewSource.GetDefaultView(SearchResults);
				SearchResultsView.Filter = FilterOrders;
			}
			else
			{
				SearchResultsView.Refresh();
			}

			SearchExpanded = SearchResults.Any();
		}



		// ===== COMMANDS ======================================================
		[RelayCommand]
		private void UseSearchHit(SearchHitRow? hit)
		{
			if (hit is null) return;

			var line = new ReturnLineRow
			{
				Article = hit.Article,
				Name = hit.ProductName,
				Brand = hit.Brand,
				Model = hit.Model,
				PurchasedQty = hit.Quantity,
				ReturnQty = 1,
				Price = hit.Price
			};
			TrackLine(line);
			Lines.Add(line);
			RecalcTotals();
		}

		[RelayCommand]
		private void AddManualLine()
		{
			if (!ManualMode) { MessageBox.Show("Включите ручной режим."); return; }
			if (string.IsNullOrWhiteSpace(ManualCustomerId)) { MessageBox.Show("Выберите клиента."); return; }
			if (SelectedManualProduct is null) { MessageBox.Show("Выберите товар."); return; }
			if (ManualQty <= 0) { MessageBox.Show("Количество должно быть больше 0."); return; }
			if (ManualPrice < 0) { MessageBox.Show("Цена не может быть отрицательной."); return; }

			var p = SelectedManualProduct;

			var line = new ReturnLineRow
			{
				Article = p.Article,
				Name = p.Display,
				Brand = "",
				PurchasedQty = ManualQty,
				ReturnQty = ManualQty,
				Price = ManualPrice
			};

			TrackLine(line);
			Lines.Add(line);
			RecalcTotals();
		}

		[RelayCommand]
		private async Task SubmitAsync()
		{
			if (Lines.Count == 0)
			{
				MessageBox.Show("Добавьте позиции возврата.");
				return;
			}

			var total = Lines.Sum(l => l.Total);

			var entity = new ReturnEntity
			{
				Date = DateTime.Now,
				CustomerId = ManualCustomerId!,
				InvoiceNumber = ManualMode ? null : "№НАКЛАДНОЙ", // если вручную, то null
				IsManual = ManualMode,
				TotalAmount = total,
				AmountInWords = NumberToWordsConverter.ConvertToRussianWords(total),
				RefundMethod = RefundMethod,
				Reason = Reason,
				Comment = Comment,
				ReturnDetails = Lines.Select(l => new ReturnDetailEntity
				{
					ArticleNumber = l.Article,
					Quantity = (int)l.ReturnQty,
					Price = l.Price,
					Total = l.Total
				}).ToList()
			};

			var saved = await _returnService.AddReturnAsync(entity);

			if (saved != null)
			{
				MessageBox.Show($"Возврат {saved.Id} успешно оформлен!");
				Lines.Clear();
				RecalcTotals();
			}
			else
			{
				MessageBox.Show("Ошибка при оформлении возврата.");
			}
		}

		[RelayCommand]
		private void RemoveLine(ReturnLineRow? line)
		{
			if (line is null) return;
			Lines.Remove(line);
			RecalcTotals();
		}

		// ===== HELPERS =======================================================
		private void TrackLine(ReturnLineRow line)
		{
			line.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName is nameof(ReturnLineRow.ReturnQty) or nameof(ReturnLineRow.Price))
					RecalcTotals();
			};
		}

		private void RecalcTotals() => Subtotal = Math.Round(Lines.Sum(l => l.Total), 2);

		// ===== MODELS ========================================================
		public sealed class SearchHitRow
		{
			public DateTime Date { get; set; }
			public string OrderId { get; set; } = "";
			public string CustomerName { get; set; } = "";
			public string Article { get; set; } = "";
			public string ProductName { get; set; } = "";
			public string Brand { get; set; } = "";
			public string Model { get; set; } = "";
			public decimal Quantity { get; set; }
			public decimal Price { get; set; }
		}

		public sealed partial class ReturnLineRow : ObservableObject
		{
			[ObservableProperty] private string article = "";
			[ObservableProperty] private string name = "";
			[ObservableProperty] private string brand = "";
			[ObservableProperty] private string marka = "";
			[ObservableProperty] private string model = "";
			[ObservableProperty] private string place = "";
			[ObservableProperty] private decimal purchasedQty;
			[ObservableProperty] private decimal returnQty;
			[ObservableProperty] private decimal price;

			public decimal Total => Math.Round(returnQty * price, 2);
			partial void OnReturnQtyChanged(decimal v) => OnPropertyChanged(nameof(Total));
			partial void OnPriceChanged(decimal v) => OnPropertyChanged(nameof(Total));
		}

		public sealed class CustomerLite
		{
			public string Id { get; set; } = "";
			public string FullName { get; set; } = "";
			public override string ToString() => FullName;
		}
	}
}
