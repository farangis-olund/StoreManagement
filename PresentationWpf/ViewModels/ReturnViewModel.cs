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
using PresentationWpf.Views;
using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using System.IO;
using Infrastructure.Constants;
using PresentationWpf.Services;

namespace PresentationWpf.ViewModels
{
	public sealed partial class ProductPickItem : ObservableObject
	{
		public string Article { get; init; } = "";
		public string Display { get; init; } = "";

        public string BrandName { get; init; } = "";
        public string ProductName { get; init; } = "";
        public string ProductMarka { get; init; } = "";

        public string ProductModel { get; init; } = "";



        // All price levels from ProductEntity
        public decimal PriceLevel1 { get; init; }
		public decimal PriceLevel2 { get; init; }
		public decimal PriceLevel3 { get; init; }
		public decimal PriceLevel4 { get; init; }
		public decimal PriceLevel5 { get; init; }

		public int Quantity { get; init; }

		// Final price depending on customer's PriceLevelCode
		[ObservableProperty] private decimal levelPrice;

		public void ApplyCustomerLevel(int? levelCode)
		{
			LevelPrice = levelCode switch
			{
				1 => PriceLevel1,
				2 => PriceLevel2,
				3 => PriceLevel3,
				4 => PriceLevel4,
				5 => PriceLevel5,
				_ => PriceLevel1 // fallback
            };
		}

		public override string ToString() => Display;
	}


	public partial class ReturnViewModel : ObservableObject
	{
		private readonly ProductService _productService;
		private readonly CustomerService _customerService;
		private readonly OrderService _orderService;
		private readonly ReturnService _returnService;
		private readonly ReturnReasonService _returnReasonService;
        private readonly CustomerFinanceService _customerFinanceService;
        private readonly OrganizationInfoService _orgService;
        private readonly UserSessionService _userSessionService;
        private readonly PermissionService _permissionService;
        public ReturnViewModel(ProductService productService, 
			CustomerService customerService, 
			OrderService orderService, 
			ReturnService returnService, 
			ReturnReasonService returnReasonService, 
			CustomerFinanceService customerFinanceService,
            OrganizationInfoService orgService,
            UserSessionService userSessionService,
            PermissionService permissionService)
		{
			_productService = productService;
			_customerService = customerService;
			_orderService = orderService;
			_returnService = returnService;
			_returnReasonService = returnReasonService;
			_customerFinanceService = customerFinanceService;
			_orgService = orgService;
			_userSessionService = userSessionService;
            _permissionService = permissionService;
			
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
        [ObservableProperty] private double _exchangeRate = 1;
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
				(o.CustomerId?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false) ||
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
            var rate = _userSessionService.ExchangeRate;
            ExchangeRate = rate > 0 ? rate : 1;
            // Customers
            // Clear both
            CustomersFiltered.Clear();

            var customers = await _customerService.GetAllCustomersAsync();
            foreach (var c in customers.Where(c => !IsOnlyOfficialCustomer || c.OfficialCustomer))
            {
                var customerLite = new CustomerLite
                {
                    Id = c.Id,
                    FullName = c.FullName,
                    PriceLevelCode = (int)c.PriceLevel.Code
                };
				              
                CustomersFiltered.Add(customerLite); // ✅ initial population
            }

            ProductsLookup.Clear();
			foreach (var p in await _productService.GetAllProductAsync())
			{
				var item = new ProductPickItem
				{
					Article = p.ArticleNumber,
                    Display = p.ArticleNumber,
                    ProductName = p.ProductName,   
                    BrandName = p.BrandName, 
					ProductMarka = p.Marka,
					ProductModel =p.Model,
                    PriceLevel1 = p.PriceLevel1,
					PriceLevel2 = p.PriceLevel2,
					PriceLevel3 = p.PriceLevel3,
					PriceLevel4 = p.PriceLevel4,
                    PriceLevel5 = p.PriceLevel5, 
					Quantity = p.Quentity
				};

				// initially apply level 1 (Retail) until a customer is selected
				item.ApplyCustomerLevel(1);

				ProductsLookup.Add(item);
			}



			// Orders
			await LoadOrdersAsync();
		}

		[RelayCommand]
		private async Task Loaded()
		{
			await InitializeAsync();
			LoadReasons();
		}

		partial void OnSearchFromChanged(DateTime? value) => _ = LoadOrdersAsync();
		partial void OnSearchToChanged(DateTime? value) => _ = LoadOrdersAsync();

		// ===== MANUAL MODE ===================================================
		[ObservableProperty] private bool manualMode;
       
		[ObservableProperty]
        private ObservableCollection<CustomerLite> customersFiltered = new();

        public bool IsOnlyOfficialCustomer =>
            _permissionService.Has("OnlyOfficialCustomer") ||
            _permissionService.Has("onlyOfficialCustomer");

        public string CustomerDisplayMemberPath => IsOnlyOfficialCustomer ? "FullName" : "Display";
       
		 public ObservableCollection<ProductPickItem> ProductsLookup { get; } = new();

		[ObservableProperty] private string? manualCustomerId;
		[ObservableProperty] private ProductPickItem? selectedManualProduct;
		[ObservableProperty] private decimal manualQty = 1;
		private bool _manualPriceTouched;
		[ObservableProperty] private decimal manualPrice;
		partial void OnManualPriceChanged(decimal value) => _manualPriceTouched = true;

	
		//private void PrefillManualPriceAsync(ProductPickItem? item)
		//{
		//	if (item is null) return;

		//	_manualPriceTouched = false;

		//	if (!_manualPriceTouched || ManualPrice <= 0)
		//		ManualPrice = item.LevelPrice;

		//	if (ManualQty <= 0) ManualQty = 1;
		//}


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
                var remainingQty = o.Quantity - (o.ReturnedQty ?? 0);

                if (remainingQty <= 0)
                    continue; // skip fully returned

                SearchResults.Add(new SearchHitRow
                {
                    Date = o.Date,
                    OrderId = o.OrderId,
                    CustomerId = o.CustomerId,
                    CustomerName = o.CustomerName,
                    Article = o.Article,
                    ProductName = o.ProductName,
                    Brand = o.Brand,
                    Marka = o.Marka,
                    Model = o.Model,
                    Quantity =(int)remainingQty,  // ✅ only show what's left
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
				Marka = hit.Marka,
				Model = hit.Model,
				PurchasedQty = hit.Quantity,
				ReturnQty = 1,
				Price = hit.Price,
				CustomerId = hit.CustomerId,
				OrderId = hit.OrderId
			};
			TrackLine(line);
			Lines.Add(line);
			RecalcTotals();
		}

		[RelayCommand]
		private void AddManualLine()
		{
			if (!ManualMode)
			{
				MessageBox.Show("Включите ручной режим.", "Ошибка");
				return;
			}

			if (SelectedCustomer is null)
			{
				MessageBox.Show("Выберите клиента.", "Ошибка");
				return;
			}

			if (SelectedManualProduct is null)
			{
				MessageBox.Show("Выберите товар.", "Ошибка");
				return;
			}

			if (ManualQty <= 0)
			{
				MessageBox.Show("Количество должно быть больше 0.", "Ошибка");
				return;
			}

			// If product entity has stock/quantity check
			if (SelectedManualProduct is { } prod && prod is ProductPickItem ppi && ppi is not null)
			{
				// example: if your ProductPickItem has a "Stock" or "Quantity" field
				if (ppi.Quantity < ManualQty)
				{
					MessageBox.Show($"Недостаточно на складе. Доступно: {ppi.Quantity}", "Ошибка");
					return;
				}
			}

			if (ManualPrice <= 0)
			{
				MessageBox.Show("Цена должна быть больше 0.", "Ошибка");
				return;
			}

			// Prevent duplicate products (same Article)
			if (Lines.Any(l => l.Article == SelectedManualProduct.Article))
			{
				MessageBox.Show("Этот товар уже добавлен в список возврата.", "Ошибка");
				return;
			}

			var p = SelectedManualProduct;

			var line = new ReturnLineRow
			{
				Article = p.Article,
                Name = p.ProductName,
                Brand = p.BrandName,
				Marka= p.ProductMarka,
				Model =p.ProductModel,
                PurchasedQty = p.Quantity,
				ReturnQty = ManualQty,
				Price = ManualPrice
			};

			TrackLine(line);
			Lines.Add(line);
			RecalcTotals();
		}


		[ObservableProperty]
		private ObservableCollection<ReturnReasonEntity> returnReasons = [];

		[ObservableProperty]
		private ReturnReasonEntity? selectedReturnReason;

		private async void LoadReasons()
		{
			var list = await _returnReasonService.GetAllAsync();
			ReturnReasons = new ObservableCollection<ReturnReasonEntity>(list);
		}

        [ObservableProperty]
        private SearchHitRow? selectedSearchHit;


        [RelayCommand]
        private async Task SubmitAsync()
        {
            if (Lines.Count == 0)
            {
                MessageBox.Show("Добавьте позиции возврата.");
                return;
            }

            if (ManualMode == true && SelectedCustomer == null)
            {
                MessageBox.Show("Выберите клиента.");
                return;
            }

            if (SelectedReturnReason == null)
            {
                MessageBox.Show("Выберите причину возврата.");
                return;
            }

            var total = Lines.Sum(l => l.Total);
            
			var firstLine = Lines.FirstOrDefault();

			var entity = new ReturnEntity
			{
				Date = DateTime.Now,
				CustomerId = SelectedCustomer?.Id
				 ?? firstLine?.CustomerId
				 ?? string.Empty,

				InvoiceNumber = ManualMode
					? null
					: firstLine?.OrderId,
				Rate = (decimal)ExchangeRate,
               
                IsManual = ManualMode,
                TotalAmount = total,
                AmountInWords = NumberToWordsConverter.ConvertToRussianWords(total),
                RefundMethod = RefundMethod,
                ReturnReasonId = SelectedReturnReason.Id,
                Comment = string.IsNullOrWhiteSpace(Comment) ? string.Empty : Comment,
                ReturnDetails = Lines.Select(l => new ReturnDetailEntity
                {
                    ArticleNumber = l.Article,
                    Quantity = (int)l.ReturnQty,
                    Price = l.Price,
                    Total = l.Total
                }).ToList()
            };

            try
            {
                var saved = await _returnService.AddReturnAsync(entity);

                if (saved != null)
                {
                    await LoadOrdersAsync();
                    MessageBox.Show("Возврат успешно оформлен!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                                     
                    var financeInfo = await _customerFinanceService.GetFinanceInfoAsync(saved.CustomerId);
                    var shopDisplay = await _orgService.GetShopDisplayAsync();
					decimal currentOldDebt = 0;

                    if (saved.RefundMethod == RefundMethodConstants.Balance)
                    {
                        currentOldDebt = financeInfo.OldDebt + saved.TotalAmount;
                    }
                    else
                    {
                        currentOldDebt = financeInfo.OldDebt;
                    }

                    // 🔹 Build the view model for the return invoice
                    var invoiceVm = new ReturnInvoiceViewModel
                    {
                        CustomerName = saved.Customer.FullName,
                        CustomerId = saved.CustomerId,
                        TotalAmount = saved.TotalAmount,
                        TotalAmountWords = saved.AmountInWords,
                        Date = saved.Date,
                        OldDebt = currentOldDebt,
                        ReturnedAmount = saved.TotalAmount,
                        RemainingDebt = financeInfo.Balance,
                        InvoiceNumber = saved.Id,
                        OrderNumber = saved.InvoiceNumber,
                        RefundMethod = saved.RefundMethod,
                        ShopName = shopDisplay ?? ""
                    };

                    foreach (var d in saved.ReturnDetails)
                    {
                        invoiceVm.Lines.Add(new ReturnInvoiceLine
                        {
                            Article = d.ArticleNumber,
                            Name = d.Product?.ProductName ?? "",
                            Brand = d.Product?.Brand.BrandName ?? "",
                            Marka = d.Product?.Marka ?? "",
                            Model = d.Product?.Model ?? "",
                            Quantity = d.Quantity,
                            Price = d.Price,
                            Total = d.Total
                        });
                    }

                    // 🔹 Generate Return Invoice PDF
                    var document = new Documents.ReturnInvoiceDocument(invoiceVm);
                    string folder = Path.Combine(Path.GetTempPath(), "ReturnInvoices");
                    Directory.CreateDirectory(folder);
                    string file = Path.Combine(folder, $"ReturnInvoice_{invoiceVm.InvoiceNumber}.pdf");
                    document.GeneratePdf(file);

                    // 🔹 Show PDF in preview window
                    var preview = new DocumentPreviewView(file);
                    var window = new Window
                    {
                        Title = "Возвратный чек",
                        Content = preview,
                        Width = 900,
                        Height = 1000,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };

                    window.ShowDialog();

                    // ✅ 1. Clear and reinitialize form
                    Lines.Clear();
					SearchText = null;
                    SelectedCustomer = null;
                    SelectedSearchHit = null;
                    SelectedReturnReason = null;
                    RefundMethod = null!;
                    Comment = null!;
                    RecalcTotals();

                    // reinitialize new empty view model
                    await InitializeAsync();
                }
                else
                {
                    MessageBox.Show("Ошибка: возврат не был сохранён ❌",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
			public string CustomerId { get; set; } = "";
			public string CustomerName { get; set; } = "";
			public string Article { get; set; } = "";
			public string ProductName { get; set; } = "";
			public string Brand { get; set; } = "";
			public string Model { get; set; } = "";
            public string Marka { get; set; } = "";
            public int Quantity { get; set; }
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
            [ObservableProperty] private string customerId = "";
            [ObservableProperty] private string orderId = "";
            [ObservableProperty] private decimal purchasedQty;
        


            private decimal _returnQty;
            public decimal ReturnQty
            {
                get => _returnQty;
                set
                {
                    var newValue = value;

                    // ⛔ clamp: not less than 0, not greater than PurchasedQty
                    if (newValue > PurchasedQty)
                        newValue = PurchasedQty;
                    if (newValue < 0)
                        newValue = 0;

                    if (_returnQty != newValue)
                    {
                        _returnQty = newValue;
                        OnPropertyChanged(nameof(ReturnQty));
                        OnPropertyChanged(nameof(Total));
                    }
                }
            }

            private decimal _price;
            public decimal Price
            {
                get => _price;
                set
                {
                    var newValue = value < 0 ? 0 : value;
                    if (_price != newValue)
                    {
                        _price = newValue;
                        OnPropertyChanged(nameof(Price));
                        OnPropertyChanged(nameof(Total));
                    }
                }
            }

            public decimal Total => Math.Round(ReturnQty * Price, 2);
        }

		public sealed class CustomerLite
		{
			public string Id { get; set; } = "";
			public string FullName { get; set; } = "";
			public int PriceLevelCode { get; set; } = 1;
            public string Display => $"{Id} – {FullName}";

            public override string ToString() => FullName;
		}

        

        [ObservableProperty]
		private CustomerLite? selectedCustomer;

		partial void OnSelectedCustomerChanged(CustomerLite? value)
		{
			if (value is null) return;

			// Recalculate prices for all products
			foreach (var product in ProductsLookup)
				product.ApplyCustomerLevel(value.PriceLevelCode);

			// Update ManualPrice to reflect the customer’s level
			if (SelectedManualProduct != null)
			{
				SelectedManualProduct.ApplyCustomerLevel(value.PriceLevelCode);

				// ✅ always update ManualPrice (not only if <= 0)
				ManualPrice = SelectedManualProduct.LevelPrice;
			}
		}

		partial void OnSelectedManualProductChanged(ProductPickItem? value)
		{
			if (value is null) return;

			// Get currently selected customer's level
			var level = SelectedCustomer?.PriceLevelCode ?? 1;

			value.ApplyCustomerLevel(level);

			// ✅ Always update ManualPrice
			ManualPrice = value.LevelPrice;

			if (ManualQty <= 0) ManualQty = 1;
		}

        public ObservableCollection<string> RefundMethods { get; } = new()
			{
				RefundMethodConstants.Cash,
				RefundMethodConstants.Card,
				RefundMethodConstants.Balance
			};

    }
}
