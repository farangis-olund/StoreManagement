using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PresentationWpf.Dtos;
using Infrastructure.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using PresentationWpf.Services;
using Infrastructure.Entities;
using System.Windows;
using Infrastructure.Dtos;
using Infrastructure.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Repositories;
using System.Windows.Media;


namespace PresentationWpf.ViewModels;

public partial class RetailViewModel : ObservableObject
{
	private readonly ProductService _productService;
	private readonly OrderService _orderService;
	private readonly CustomerService _customerService;
	private readonly UserSessionService _userSessionService;
	private readonly DataTransferService _dataTransferService;
	private readonly IServiceProvider _serviceProvider;
	private readonly CourierRepository _courierRepo;
	private readonly StorekeeperRepository _storekeeperRepo;
	private readonly SalesManagerRepository _salesManagerRepo;
	private readonly DialogService _dialogService;
	private readonly CustomerFinanceService _financeService;
	public RetailViewModel(ProductService productService, 
							UserSessionService userSessionService, 
							OrderService orderService, 
							CustomerService customerService,
							DataTransferService dataTransferService,
							IServiceProvider serviceProvider,
							CourierRepository courierRepo,
							StorekeeperRepository storekeeperRepo,
							SalesManagerRepository salesManagerRepo,
							DialogService dialogService,
							CustomerFinanceService financeService)
	{
		_productService = productService;
		_orderService = orderService;
		_customerService = customerService;
		_userSessionService = userSessionService;
		_dataTransferService = dataTransferService;
		_serviceProvider = serviceProvider;
		_courierRepo = courierRepo;
		_storekeeperRepo = storekeeperRepo;
		_salesManagerRepo = salesManagerRepo;
		_dialogService = dialogService;
		_financeService = financeService;

		_productList = [];
		_selectedProductList = [];
		_alternativeProductList = [];

		SelectedProductList.CollectionChanged += (s, e) =>
		{
			if (e.NewItems != null)
			{
				foreach (ProductModel item in e.NewItems)
				{
					item.PropertyChanged += OnProductModelPropertyChanged;
				}
			}

			if (e.OldItems != null)
			{
				foreach (ProductModel item in e.OldItems)
				{
					item.PropertyChanged -= OnProductModelPropertyChanged;
				}
			}

			UpdateTotalSum();
		};

		CustomersView = CollectionViewSource.GetDefaultView(AllCustomers);
		CustomersView.Filter = CustomerFilter;
	}

	[ObservableProperty]
	private ObservableCollection<ProductModel> _productList = [];

	[ObservableProperty]
	private ObservableCollection<ProductModel> _selectedProductList = [];

	[ObservableProperty]
	private ObservableCollection<InactiveCustomerDto> _inactiveDebtCustomers = [];

	[ObservableProperty]
	private ObservableCollection<InactiveCustomerDto> _inactivePlannedCustomers = [];


	[ObservableProperty]
	private decimal _totalSum = 0;

	[ObservableProperty]
	private string _user = null!;

	[ObservableProperty]
	private Customer? _selectedCustomer = null!;

	[ObservableProperty]
	private bool _directFromStock;

	[ObservableProperty]
	private double _exchangeRate = 1;

	[ObservableProperty]
	private int _customerLevel = 1;

	[ObservableProperty]
	private bool _isSellingWithoutInvoice;

	[ObservableProperty]
	private ObservableCollection<Customer> _allCustomers = [];
	public ICollectionView CustomersView { get; private set; } = null!;

	[ObservableProperty]
	private ObservableCollection<Courier> _courierList = [];
	[ObservableProperty] private Courier _selectedCourier = null!;

	[ObservableProperty]
	private ObservableCollection<SalesManager> _salesManagerList = [];
	private SalesManager? _selectedSalesManager;

	public SalesManager? SelectedSalesManager
	{
		get => _selectedSalesManager;
		set
		{
			if (_selectedSalesManager == value) return;
			_selectedSalesManager = value;
			OnPropertyChanged();
			RebuildTerritories();
			_ = LoadInactiveCustomersAsync();
			CustomersView.Refresh();    
		}
	}

	private string? _selectedTerritory;
	public string? SelectedTerritory
	{
		get => _selectedTerritory;
		set { if (_selectedTerritory == value) return; 
			_selectedTerritory = value; 
			OnPropertyChanged(); 
			CustomersView.Refresh();
			_ = LoadInactiveCustomersAsync();
		}

	}

	public ListCollectionView CustomersForManager { get; private set; } = null!;

	[ObservableProperty]
	private ObservableCollection<Storekeeper> _storekeeperList = [];
	[ObservableProperty] private Storekeeper _selectedStorekeeper = null!;

	private ObservableCollection<TerritoryOption> _territoryOptions = [];
	public ObservableCollection<TerritoryOption> TerritoryOptions
	{
		get => _territoryOptions;
		set { _territoryOptions = value; OnPropertyChanged(); }
	}


	private string _filterText = null!;

	public string FilterText
	{
		get => _filterText;
		set
		{
			SetProperty(ref _filterText, value);
			ApplyFilter();
		}
	}

	private bool _searchAllWords;
	public bool SearchAllWords
	{
		get => _searchAllWords;
		set
		{
			_searchAllWords = value;
			OnPropertyChanged(nameof(SearchAllWords));
			CollectionViewSource.GetDefaultView(ProductListView).Refresh();
		}
	}

	public ICollectionView ProductListView { get; private set; } = null!;

	public async Task InitializeAsync()
	{
		User = _userSessionService.FirstName + " " + _userSessionService.LastName;

		// 1) Make sure rate is valid (avoid multiplying by 0)
		var rate = _userSessionService.ExchangeRate;
		ExchangeRate = rate > 0 ? rate : 1;
		

		// 2) Get products (DTOs) and convert to ProductModel using your implicit operator
		var productDtos = await _productService.GetAllProductAsync(); // IEnumerable<Product> (DTO)
		foreach (var dto in productDtos.Where(p => p.Quentity > 0))
		{
			var pm = (ProductModel)dto;     // uses implicit operator to fill euro prices
			pm.ExchangeRate = ExchangeRate; // triggers Retail/Customer price recompute
			pm.RefreshPrices();             // ensure initial bindings show values
			ProductList.Add(pm);
		}

		ProductListView = CollectionViewSource.GetDefaultView(ProductList);
		ProductListView.Filter = FilterProducts;
		ProductListView.Refresh();
		// 3) Customers (source collection)
		var customersEntity = await _customerService.GetAllCustomersAsync();
		AllCustomers = new ObservableCollection<Customer>(
			customersEntity.Select(e => (Customer)e));

		// 3a) View over customers (bind UI to this!)
		CustomersView = CollectionViewSource.GetDefaultView(AllCustomers);
		CustomersView.Filter = CustomerFilter;    // <- uses SelectedSalesManager / SelectedTerritory

		// Optional: start with “all territories” option preloaded
		TerritoryOptions = new ObservableCollection<TerritoryOption>
	{
		new TerritoryOption { Value = null, Display = "Все территории" }
	};
		SelectedTerritory = null;                 // show all by default
		SelectedSalesManager = null;              // no manager selected -> view shows all customers

		// 4) Other lookups
		var couriers = await _courierRepo.GetAllAsync();
		CourierList = new ObservableCollection<Courier>(couriers.Select(e => (Courier)e));

		var storekeepers = await _storekeeperRepo.GetAllAsync();
		StorekeeperList = new ObservableCollection<Storekeeper>(storekeepers.Select(e => (Storekeeper)e));

		var salesManagers = await _salesManagerRepo.GetAllAsync();
		SalesManagerList = new ObservableCollection<SalesManager>(salesManagers.Select(e => (SalesManager)e));


	}


	private ObservableCollection<ProductModel> _alternativeProductList;
	public ObservableCollection<ProductModel> AlternativeProductList
	{
		get { return _alternativeProductList; }
		set { _alternativeProductList = value; OnPropertyChanged(nameof(AlternativeProductList)); }
	}

	private ProductModel _selectedProduct = null!;
	public ProductModel SelectedProduct
	{
		get { return _selectedProduct; }
		set
		{
			if (_selectedProduct == value) return;
			_selectedProduct = value;
			OnPropertyChanged(nameof(SelectedProduct));
			
			AlternativeProductList.Clear(); 
			if (_selectedProduct != null && !string.IsNullOrEmpty(_selectedProduct.Alternative))
			{
				LoadAlternativeProducts(_selectedProduct.Alternative);
			}
		}

	}

	partial void OnExchangeRateChanged(double value)
	{
		if (ProductList is null) return;
		var rate = value > 0 ? value : 1.0;

		foreach (var p in ProductList)
		{
			p.ExchangeRate = rate;   // triggers price recalculation and Total update
		}
	}

	partial void OnSelectedCustomerChanged(Customer? value)
	{
		ApplyCustomerLevelPrice();

		if (value == null)
		{
			RequiredPurchase = 0;
			PlannedPurchase = 0;
			return;
		}

		// ✅ Check if this customer exists in inactive debt list
		var debtInfo = InactiveDebtCustomers.FirstOrDefault(c => c.CustomerId == value.Id);
		if (debtInfo != null)
		{
			RequiredPurchase = debtInfo.Shortfall;
		}
		else
		{
			RequiredPurchase = 0;
		}

		// ✅ Check if this customer exists in inactive planned list
		var plannedInfo = InactivePlannedCustomers.FirstOrDefault(c => c.CustomerId == value.Id);
		if (plannedInfo != null)
		{
			PlannedPurchase = plannedInfo.Shortfall;
		}
		else
		{
			PlannedPurchase = 0;
		}

	}


	private void ApplyCustomerLevelPrice()
	{
		if (ProductList is null) return;

		int? level = SelectedCustomer?.PriceLevelCode; // null if none selected
		if (level is < 1 or > 6) level = 1;           // validate if present

		foreach (var p in ProductList)
		{
			p.CustomerPriceLevel = level; // null ⇒ CustomerPrice becomes null
			p.RefreshPrices();
		}
	}

	private void LoadAlternativeProducts(string alternative)
	{
		if (string.IsNullOrEmpty(alternative)) return;
		AlternativeProductList.Clear();
	
		foreach (var product in ProductList.Where(p => p.Alternative == alternative))
		{
			AlternativeProductList.Add(product);
		}
	}
	
	[RelayCommand]
	private void AddToChart(ProductModel product)
	{
		if (product != null && !SelectedProductList.Contains(product))
		{
			product.OrderQuentity = 0;

			SelectedProductList.Add(product);
		}

		
	}

	[RelayCommand]
	private void RemoveProduct(ProductModel product)
	{
		if (product != null && SelectedProductList.Contains(product))
		{
			SelectedProductList.Remove(product);
		}
	}

	[RelayCommand]
	
	private async Task SaveOrder()
	{
		if (SelectedProductList == null || !SelectedProductList.Any())
		{
			MessageBox.Show("Список продуктов пуст. Пожалуйста, добавьте хотя бы один товар.", "Ошибка");
			return;
		}

		// if barter -> require a manual price on each line
		if (IsBarter)
		{
			var noPrice = SelectedProductList
				.FirstOrDefault(p => p.OrderQuentity <= 0 || !p.BarterPriceSom.HasValue);
			if (noPrice != null)
			{
				MessageBox.Show(
					$"В режиме бартера у каждой позиции должно быть указано значение цены и количество > 0.\n" +
					$"Проверьте товар: {noPrice.ArticleNumber} — {noPrice.ProductName}.",
					"Ошибка");
				return;
			}
		}

		// compute totals with effective price (barter price if set, otherwise normal)
		var totalSum = SelectedProductList.Sum(p => (p.EffectivePriceSom ?? 0m) * p.OrderQuentity);
		if (totalSum <= 0)
		{
			MessageBox.Show("Сумма заказа не может быть 0. Пожалуйста, проверьте количество заказа.", "Ошибка");
			return;
		}

		if (SelectedCustomer == null)
		{
			MessageBox.Show("Для оформление заказа, сперва выберите клиента.", "Ошибка");
			return;
		}
		// Only validate courier / storekeeper if NOT barter
		if (!IsBarter)
		{
			if (SelectedCourier == null)
			{
				MessageBox.Show("Для оформление заказа, сперва укажите доставщика.", "Ошибка");
				return;
			}

			if (SelectedStorekeeper == null)
			{
				MessageBox.Show("Для оформление заказа, сперва укажите складчика.", "Ошибка");
				return;
			}
		}


		var totalSumInWords = NumberToWordsConverter.ConvertToRussianWords(totalSum);

		// Build persistence entity
		var orderEntity = new OrderEntity
		{
			Date = DateTime.Now,
			Rate = ExchangeRate,
			UserId = _userSessionService.UserId,
			WithoutInvoice = IsSellingWithoutInvoice,
			DirectFromStock = DirectFromStock,
			Stock = false,
			IsPaid = false,
			SuminWords = totalSumInWords,
			CustomerId = SelectedCustomer.Id,
			IsBarter = IsBarter,

			OrderDetails = SelectedProductList.Select(p =>
			{
				// pick the unit price according to mode
				var unit = IsBarter
					? (p.BarterPriceSom ?? 0m)
					: (p.CustomerPriceSom ?? 0m);

				var qty = Math.Max(0, p.OrderQuentity);
				return new OrderDetailEntity
				{
					ArticleNumber = p.ArticleNumber,
					Quentity = qty,
					Price = qty > 0 ? unit : 0m
				};
			}).ToList()
		};
		if (!IsBarter)
		{
			orderEntity.CourierId = SelectedCourier!.Id;
			orderEntity.StorekeeperId = SelectedStorekeeper!.Id;
		}


		var result = await _orderService.AddOrderAsync(orderEntity);

		if (result != null)
		{
			MessageBox.Show("Заказ успешно оформлен!", "Оформление заказа");

			//_dataTransferService.SelectedOrder = newOrder;
			SelectedProductList.Clear();
			await RefreshProductsAsync();

			// Print: barter report vs invoice
			if (IsBarter)
				DisplayBarterReportView();  // TODO: your method that opens the simple barter report
			//else
			//	DisplayInvoiceView();       // existing invoice printer
		}
		else
		{
			MessageBox.Show("По некоторым причинам заказ неоформлен, попробуйте еще раз!", "Оформление заказа");
		}
	}


	private void OnProductModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ProductModel.Total))
		{
			UpdateTotalSum();
		}
	}

	public void UpdateTotalSum()
	{
		TotalSum = SelectedProductList.Sum(p => p.Total);
	}

	private bool FilterProducts(object item)
	{
		if (item is ProductModel product)
		{
			if (string.IsNullOrEmpty(FilterText))
			{
				return true;
			}

			// Split FilterText into words
			var filterWords = FilterText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			// Define a function to check if a product contains a word
			bool ContainsWord(string word) =>
				(product.ArticleNumber != null && product.ArticleNumber.Contains(word, StringComparison.OrdinalIgnoreCase)) ||
				(product.ProductName != null && product.ProductName.Contains(word, StringComparison.OrdinalIgnoreCase)) ||
				(product.Marka != null && product.Marka.Contains(word, StringComparison.OrdinalIgnoreCase)) ||
				(product.Model != null && product.Model.Contains(word, StringComparison.OrdinalIgnoreCase)) ||
				(product.BrandName != null && product.BrandName.Contains(word, StringComparison.OrdinalIgnoreCase)) ||
				(product.GroupName != null && product.GroupName.Contains(word, StringComparison.OrdinalIgnoreCase));

			// Check all words or any word based on the checkbox state
			if (SearchAllWords)
			{
				// Check if the product contains all the words
				return filterWords.All(word => ContainsWord(word));
			}
			else
			{
				// Check if the product contains any of the words
				return filterWords.Any(word => ContainsWord(word));
			}
		}

		return false;
	}

	private void ApplyFilter()
	{
		ProductListView?.Refresh();
	}
		
	//private void DisplayInvoiceView()
	//{
	//	var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
	//	var orderInvoiceViewModel = _serviceProvider.GetRequiredService<OrderInvoiceViewModel>();
	//	orderInvoiceViewModel.LoadInvoiceData();
	//	mainViewModel.CurrentViewModel = orderInvoiceViewModel;
	//}

	private void DisplayBarterReportView() { }
	
	private bool CustomerFilter(object obj)
	{
		if (obj is not Customer c) return false;

		// If no manager selected -> show everyone
		if (SelectedSalesManager == null) return true;

		// Manager selected -> filter by manager (and optionally by territory)
		bool byManager = c.SalesManagerId == SelectedSalesManager.Id;
		bool byTerritory = string.IsNullOrWhiteSpace(SelectedTerritory)
						   || string.Equals(c.Territory, SelectedTerritory, StringComparison.OrdinalIgnoreCase);

		return byManager && byTerritory;
	}

	private void RebuildTerritories()
	{
		// keep the same collection instance so binding updates automatically
		TerritoryOptions.Clear();
		TerritoryOptions.Add(new TerritoryOption { Value = null, Display = "Все территории" });

		if (SelectedSalesManager != null)
		{
			var terrs = AllCustomers
				.Where(c => c.SalesManagerId == SelectedSalesManager.Id)
				.Select(c => c.Territory)
				.Where(t => !string.IsNullOrWhiteSpace(t))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(t => t);

			foreach (var t in terrs)
				TerritoryOptions.Add(new TerritoryOption { Value = t, Display = t! });
		}

		// reset selection so "Все территории" is active after manager change
		SelectedTerritory = null;
	}

	private bool _isBarter;
	public bool IsBarter
	{
		get => _isBarter;
		set
		{
			if (_isBarter == value) return;
			_isBarter = value;
			OnPropertyChanged(nameof(IsBarter));

			foreach (var item in SelectedProductList)
				item.ClearBarterPrice(); // start with blank barter price
		}
	}

	[RelayCommand]
	private async Task ShowSummaryAsync()
	{
		if (SelectedCustomer == null)
		{
			MessageBox.Show("Сначала выберите клиента.", "Итоги");
			return;
		}
		_dataTransferService.CustomerId = SelectedCustomer.Id;

		var vm = _serviceProvider.GetRequiredService<SummaryViewModel>();

		await vm.LoadAsync();

		// Show it
		_dialogService.Show(vm);

		//var vm = new SummaryViewModel(_orderService, _dataTransferService, _serviceProvider);

		//await vm.LoadAsync();

		//// MODEL-LESS:
		//_dialogService.Show(vm);  // ⬅ instead of ShowDialogAsync
	}

	public async Task SelectCustomerByIdAsync(string customerId)
	{
		if (string.IsNullOrWhiteSpace(customerId)) return;

		// ensure list is ready
		if (AllCustomers == null || AllCustomers.Count == 0)
			await InitializeAsync();

		var existing = AllCustomers.FirstOrDefault(c => c.Id == customerId);
		if (existing != null)
		{
			SelectedCustomer = existing;   // ⬅ вот тут реально выбирается клиент
			return;
		}

		// fallback: fetch single by id (если клиента ещё нет в списке)
		var one = await _customerService.GetCustomerAsync(customerId);
		if (one != null)
		{
			AllCustomers.Add(one);
			SelectedCustomer = one;
		}
	}

	[ObservableProperty]
	private int _inactiveDebtCount;

	[ObservableProperty]
	private int _inactivePlannedCount;
	private async Task LoadInactiveCustomersAsync()
	{
		InactiveDebtCustomers.Clear();
		InactivePlannedCustomers.Clear();

		InactiveDebtCount = 0;
		InactivePlannedCount = 0;

		if (SelectedSalesManager == null)
			return;

		var managerId = SelectedSalesManager.Id;
		var territory = SelectedTerritory;

		// 1) Customers with unpaid debts
		var debtList = await _financeService.NeaktivOtEzhigodPogashenieAsync(managerId, territory);
		foreach (var c in debtList)
			InactiveDebtCustomers.Add(c);

		InactiveDebtCount = InactiveDebtCustomers.Count;

		// 2) Customers not buying as planned
		var plannedList = await _financeService.NeaktivNeZakupAsync(managerId, territory);
		foreach (var c in plannedList)
			InactivePlannedCustomers.Add(c);

		InactivePlannedCount = InactivePlannedCustomers.Count;
	}

	private async Task RefreshProductsAsync()
	{
		ProductList.Clear();
		var productDtos = await _productService.GetAllProductAsync();

		foreach (var dto in productDtos.Where(p => p.Quentity > 0))
		{
			var pm = (ProductModel)dto;
			pm.ExchangeRate = ExchangeRate;
			pm.RefreshPrices();
			ProductList.Add(pm);
		}
		ApplyCustomerLevelPrice();
	}

	private InactiveCustomerDto? _selectedRequiredPurchaseCombo;
	public InactiveCustomerDto? SelectedRequiredPurchaseCombo
	{
		get => _selectedRequiredPurchaseCombo;
		set
		{
			if (_selectedRequiredPurchaseCombo == value) return;
			_selectedRequiredPurchaseCombo = value;
			OnPropertyChanged();

			if (value != null)
			{
				// update fields
				RequiredPurchase = value.Shortfall;
				_ = SelectCustomerByIdAsync(value.CustomerId);

				// ✅ clear the other combo
				SelectedPlannedPurchaseCombo = null;
			}
			
		}
	}

	private InactiveCustomerDto? _selectedPlannedPurchaseCombo;
	public InactiveCustomerDto? SelectedPlannedPurchaseCombo
	{
		get => _selectedPlannedPurchaseCombo;
		set
		{
			if (_selectedPlannedPurchaseCombo == value) return;
			_selectedPlannedPurchaseCombo = value;
			OnPropertyChanged();

			if (value != null)
			{
				PlannedPurchase = value.Shortfall;
				_ = SelectCustomerByIdAsync(value.CustomerId);

				// ✅ clear the other combo
				SelectedRequiredPurchaseCombo = null;
			}
			
		}
	}

	private decimal _requiredPurchase;
	public decimal RequiredPurchase
	{
		get => _requiredPurchase;
		set
		{
			if (_requiredPurchase == value) return;
			_requiredPurchase = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(RequiredPurchaseColor));
		}
	}

	private decimal _plannedPurchase;
	public decimal PlannedPurchase
	{
		get => _plannedPurchase;
		set
		{
			if (_plannedPurchase == value) return;
			_plannedPurchase = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(PlannedPurchaseColor));
		}
	}

	// Expose brush colors for binding
	public Brush RequiredPurchaseColor => RequiredPurchase > 0 ? Brushes.Red : Brushes.Green;
	public Brush PlannedPurchaseColor => PlannedPurchase > 0 ? Brushes.Red : Brushes.Green;


}

