using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Constants;
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
using PresentationWpf.Views;
using System.Globalization;


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
    private readonly PermissionService _permissionService;
    private readonly Infrastructure.Security.OrderOperationsAccessPolicy _orderOperationsAccess;
    private bool _isAdjustingProductLimit;
    private bool _isSavingOrder;
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
							CustomerFinanceService financeService,
                            PermissionService permissionService,
                            ProductRequestService productRequestService, OfficialMonthlyPlanService? monthlyPlanService = null,
                            RetailManagerService? retailManagerService = null)
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
        _permissionService = permissionService;
        _orderOperationsAccess = new(permissionService);
        _productRequestService = productRequestService;
        _retailManagerService = retailManagerService;
        if (userSessionService != null && retailManagerService != null)
            WeakEventManager<UserSessionService, EventArgs>.AddHandler(userSessionService, nameof(UserSessionService.UserChanged), OnRetailUserChanged);
        InitializeMonthlyPlans(monthlyPlanService);
        PropertyChangedEventManager.AddHandler(_permissionService, OnOrderPermissionsChanged,
            nameof(PermissionService.CurrentPermissions));

		_productList = [];
		_selectedProductList = [];
		_alternativeProductList = [];

		SelectedProductList.CollectionChanged += (s, e) =>
		{
            // Clear() reports Reset without OldItems; detach those rows as well.
            foreach (var item in _subscribedCartProducts.Except(SelectedProductList).ToArray())
            {
                item.PropertyChanged -= OnProductModelPropertyChanged;
                item.OrderQuantityExceeded -= OnOrderQuantityExceeded;
                _subscribedCartProducts.Remove(item);
                foreach (var key in _cartRequestIds.Keys.Where(k => k.Article == item.ArticleNumber).ToArray())
                    _cartRequestIds.Remove(key);
            }
            foreach (var item in SelectedProductList.Except(_subscribedCartProducts))
            {
                item.IsBarterPriceMode = IsBarter;
                item.PropertyChanged += OnProductModelPropertyChanged;
                item.OrderQuantityExceeded += OnOrderQuantityExceeded;
                _subscribedCartProducts.Add(item);
            }

			UpdateTotalSum();
            EnforceBuyLimit();
            ClearCartCommand.NotifyCanExecuteChanged();
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

	private Customer? _selectedCustomer = null!;

    [ObservableProperty]
    private bool _isOrderBlockedByUnpaidPaymentLimit;

    [ObservableProperty]
    private decimal? _availableRestriction;
    private int _restrictionRequestVersion;
    private decimal? _customerBalance;

    private bool IsOrderCustomerReady => IsRetailManagerContextReady && SelectedCustomer != null;
    private bool IsCustomerFinanceReady => IsOrderCustomerReady && _customerBalance.HasValue && AvailableRestriction.HasValue;
    private CustomerOrderAllowance? OrderAllowance => !IsBarter && IsCustomerFinanceReady
        ? new CustomerOrderAllowance(SelectedCustomer!.BuyLimit, _customerBalance!.Value,
            _permissionService.Has(PermissionCatalog.OrdersExceedBuyLimit), AvailableRestriction)
        : null;

    partial void OnAvailableRestrictionChanged(decimal? value)
    {
        EnforceBuyLimit();
        AddToChartCommand.NotifyCanExecuteChanged();
        SaveOrderCommand.NotifyCanExecuteChanged();
    }

    public bool IsOnlyOfficialCustomer =>
        _permissionService.Has(PermissionCatalog.OnlyOfficialCustomer) ||
        _permissionService.Has("onlyOfficialCustomer");

    public bool CanViewCustomerCode => !IsOnlyOfficialCustomer;

    public string CustomerDisplayMemberPath => IsOnlyOfficialCustomer ? "FullName" : "Id";

    public bool CanUseBarter => _permissionService.Has(PermissionCatalog.OrdersBarter);
    public bool CanChangeBarterMode => CanUseBarter && IsOrderCustomerReady && !_isSavingOrder;
    public bool CanUseDirectFromStock => _permissionService.Has(PermissionCatalog.OrdersDirectFromStock);
    public bool CanOpenDelivery => _orderOperationsAccess.Can(PermissionCatalog.OrdersDelivery);
    public bool CanAssignPickers => _orderOperationsAccess.Can(PermissionCatalog.OrdersAssignPickers);

    private void OnOrderPermissionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshRetailManagerPermissions();
        RefreshMonthlyPlanPermissions();
        OnPropertyChanged(nameof(CanUseBarter));
        OnPropertyChanged(nameof(CanChangeBarterMode));
        OnPropertyChanged(nameof(CanUseDirectFromStock));
        OnPropertyChanged(nameof(CanOpenDelivery));
        OnPropertyChanged(nameof(CanAssignPickers));
        ShowDeliveryOrdersCommand.NotifyCanExecuteChanged();
        ShowAssignPickersCommand.NotifyCanExecuteChanged();
        if (!CanUseBarter) SetBarterMode(false);
        if (!CanUseDirectFromStock) DirectFromStock = false;
        EnforceBuyLimit();
        SaveOrderCommand.NotifyCanExecuteChanged();
    }

    private bool HasValidCourier => SelectedCourier != null && CourierList.Contains(SelectedCourier);
    private bool HasValidStorekeeper => SelectedStorekeeper != null && StorekeeperList.Contains(SelectedStorekeeper);

    private bool CanSaveOrder() => IsOrderCustomerReady && !HasPendingProductRequests &&
        (IsBarter || (IsCustomerFinanceReady && IsOfficialPlanReady && !IsOrderBlockedByUnpaidPaymentLimit &&
            !IsBuyLimitExceeded(TotalSum) && !IsMonthlyPlanLimitExceeded(TotalSum) && HasValidCourier && HasValidStorekeeper)) &&
        (!IsBarter || SelectedProductList.All(p => p.BarterPriceSom is >= 0)) &&
        (!IsBarter || CanUseBarter) && (!DirectFromStock || CanUseDirectFromStock);

    partial void OnIsOrderBlockedByUnpaidPaymentLimitChanged(bool value)
    {
        SaveOrderCommand.NotifyCanExecuteChanged();
    }

	[ObservableProperty]
	private bool _directFromStock;

    partial void OnDirectFromStockChanged(bool value)
    {
        if (value && !CanUseDirectFromStock) DirectFromStock = false;
        SaveOrderCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private bool _officialOrder = true;

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
	[NotifyCanExecuteChangedFor(nameof(SaveOrderCommand))]
	private ObservableCollection<Courier> _courierList = [];
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SaveOrderCommand))]
	private Courier? _selectedCourier;

	[ObservableProperty]
	private ObservableCollection<SalesManager> _salesManagerList = [];
	private SalesManager? _selectedSalesManager;

	public SalesManager? SelectedSalesManager
	{
		get => _selectedSalesManager;
		set
		{
            if (!CanChooseSalesManager || (value != null && !SalesManagerList.Contains(value)))
            {
                OnPropertyChanged(nameof(SelectedSalesManager));
                return;
            }
            if (_selectedSalesManager != value && SelectedCustomer != null &&
                value != null && SelectedCustomer.SalesManagerId != value.Id &&
                !CanChangeCustomer(null, nameof(SelectedSalesManager))) return;
            SetSalesManagerCore(value);
		}
	}

	private string? _selectedTerritory;
	public string? SelectedTerritory
	{
		get => _selectedTerritory;
		set { if (_selectedTerritory == value) return;
            if (!_isChangingManager && SelectedCustomer != null && !string.IsNullOrWhiteSpace(value) &&
                !string.Equals(SelectedCustomer.Territory, value, StringComparison.OrdinalIgnoreCase) &&
                !CanChangeCustomer(null, nameof(SelectedTerritory))) return;
			_selectedTerritory = value; 
			OnPropertyChanged(); 
            if (!_isChangingManager) RetailFiltersChanged();
		}

	}

	public ListCollectionView CustomersForManager { get; private set; } = null!;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SaveOrderCommand))]
	private ObservableCollection<Storekeeper> _storekeeperList = [];
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SaveOrderCommand))]
	private Storekeeper? _selectedStorekeeper;

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
			if (!SetProperty(ref _filterText, value)) return;
            ResetProductRequestEntry();
            SelectedProduct = null!;
            UpdateInlineSuggestion();
            ApplyFilter();
		}
	}

    private string? _exactArticleFilter;
	private bool _searchAllWords = true;
	public bool SearchAllWords
	{
		get => _searchAllWords;
		set
		{
            if (SetProperty(ref _searchAllWords, value))
                ApplyFilter();
		}
	}

    [ObservableProperty]
    private string inlineSuggestion;

    [ObservableProperty]
    private double inlineSuggestionOffset;

    [ObservableProperty]
    private string fullSuggestion;

    private void UpdateInlineSuggestion()
    {
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            InlineSuggestion = "";
            FullSuggestion = "";
            InlineSuggestionOffset = 0;
            return;
        }

        var match = ProductList
            .Select(p => p.ArticleNumber)
            .FirstOrDefault(a =>
                a.StartsWith(FilterText, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(match))
        {
            InlineSuggestion = "";
            FullSuggestion = "";
            InlineSuggestionOffset = 0;
            return;
        }

        // 👇 store full suggestion
        FullSuggestion = match;

        // 👇 remaining suffix
        var remaining = match.Substring(FilterText.Length);

        InlineSuggestion = remaining;

        if (string.IsNullOrEmpty(remaining))
        {
            InlineSuggestionOffset = 0;
            return;
        }

        // measure width
        var formatted = new FormattedText(
            FilterText,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            18,
            Brushes.Black,
            1.0
        );

        InlineSuggestionOffset = formatted.Width;
    }

    [ObservableProperty]
    private string? customerLevelArrow;

    [ObservableProperty]
    private Brush customerLevelArrowColor = Brushes.Transparent;

    [ObservableProperty]
    private bool showCustomerLevelArrow;

    [ObservableProperty]
    private decimal customerLast30DaysTotal;

    [ObservableProperty]
    private string? customerCalculatedPriceType;

    public ICollectionView ProductListView { get; private set; } = null!;

	public async Task InitializeAsync()
	{
        var initialization = ++_retailInitializationVersion;
        var userId = _userSessionService.UserId;
        if (_retailManagerService != null) ClearRetailManagerContext();
		User = _userSessionService.FirstName + " " + _userSessionService.LastName;

        // 1) Make sure rate is valid (avoid multiplying by 0)
        var rate = _userSessionService.ExchangeRate;
		ExchangeRate = rate > 0 ? rate : 1;

        ProductList.Clear();

        // 2) Get products (DTOs) and convert to ProductModel using your implicit operator
        var productDtos = await _productService.GetAllProductAsync(); 
        if (initialization != _retailInitializationVersion || userId != _userSessionService.UserId) return;
		foreach (var dto in productDtos)
		{
			var pm = (ProductModel)dto;     
			pm.ExchangeRate = ExchangeRate; 
			pm.RefreshPrices();             
			ProductList.Add(pm);
		}

		ProductListView = CollectionViewSource.GetDefaultView(ProductList);
		ProductListView.Filter = FilterProducts;
        ApplyFilter();
		// 3) Customers (source collection)
        if (_retailManagerService != null) await RefreshManagerContextAsync();
        else
        {
            var customersEntity = await _customerService.GetAllCustomersAsync();
            AllCustomers = new ObservableCollection<Customer>(customersEntity.Select(e => (Customer)e));
            CustomersView = CollectionViewSource.GetDefaultView(AllCustomers);
            CustomersView.Filter = CustomerFilter;
            SetSalesManagerCore(null);
        }
        if (initialization != _retailInitializationVersion || userId != _userSessionService.UserId) return;

        // 4) Other lookups — only Active = true

        var couriers = await _courierRepo.GetAllAsync();
        if (initialization != _retailInitializationVersion || userId != _userSessionService.UserId) return;
        CourierList = new ObservableCollection<Courier>(
            couriers
                .Where(e => e.Active)
                .Select(e => (Courier)e)
        );

        var storekeepers = await _storekeeperRepo.GetAllAsync();
        if (initialization != _retailInitializationVersion || userId != _userSessionService.UserId) return;
        StorekeeperList = new ObservableCollection<Storekeeper>(
            storekeepers
                .Where(e => e.Active)
                .Select(e => (Storekeeper)e)
        );

        if (_retailManagerService == null)
        {
            var salesManagers = await _salesManagerRepo.GetAllAsync();
            SalesManagerList = new ObservableCollection<SalesManager>(salesManagers.Where(e => e.Active).Select(e => (SalesManager)e));
        }
        await RefreshMonthlyPlanStateAsync();
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
            UpdateProductRequestPrompt();
			
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

	private void OnSelectedCustomerChanged(Customer? value)
	{
        SetBarterMode(false, refreshOrdinaryState: false);
        OnPropertyChanged(nameof(CanChangeBarterMode));
        SelectedCourier = null;
        SelectedStorekeeper = null;
        _manualRequestId = Guid.NewGuid();
        _customerBalance = null;
        AddToChartCommand.NotifyCanExecuteChanged();
        SaveOrderCommand.NotifyCanExecuteChanged();
        OfficialOrder = !IsBarter && value?.OfficialCustomer == true;
        _ = RefreshMonthlyPlanStateAsync();

		ApplyCustomerLevelPrice();

        _ = UpdateCustomerLevelIndicatorAsync();

        _ = WarnIfUnpaidPaymentLimitReachedAsync(value);
        _ = RefreshAvailableRestrictionAsync(value);

        if (SelectedRequiredPurchaseCombo?.CustomerId != value?.Id)
            SelectedRequiredPurchaseCombo = null;
        if (SelectedPlannedPurchaseCombo?.CustomerId != value?.Id)
            SelectedPlannedPurchaseCombo = null;
        UpdateCustomerPurchaseIndicators();
	}

    private async Task<bool> RefreshAvailableRestrictionAsync(Customer? customer)
    {
        var requestVersion = ++_restrictionRequestVersion;
        AvailableRestriction = null;
        _customerBalance = null;
        AddToChartCommand.NotifyCanExecuteChanged();
        SaveOrderCommand.NotifyCanExecuteChanged();
        if (customer == null) return false;

        try
        {
            // Use the same balance as the customer summary, including payments and returns.
            var finance = await _financeService.GetFinanceInfoAsync(customer.Id, orderId: null!);
            if (requestVersion == _restrictionRequestVersion && SelectedCustomer == customer)
            {
                _customerBalance = finance.Balance;
                AvailableRestriction = finance.AvailableRestriction;
                AddToChartCommand.NotifyCanExecuteChanged();
                SaveOrderCommand.NotifyCanExecuteChanged();
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (requestVersion == _restrictionRequestVersion && SelectedCustomer == customer)
                MessageBox.Show("Не удалось загрузить остаток для расчета ограничения. Повторно выберите клиента.",
                    "Ограничение", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        return false;
    }

    private async Task WarnIfUnpaidPaymentLimitReachedAsync(Customer? customer)
    {
        if (customer == null || IsBarter)
        {
            IsOrderBlockedByUnpaidPaymentLimit = false;
            return;
        }

        var unpaidPaymentLimit = customer.UnpaidPaymentLimit ?? 0;
        if (unpaidPaymentLimit <= 0)
        {
            IsOrderBlockedByUnpaidPaymentLimit = false;
            return;
        }

        var unpaidPaymentTotal = await _orderService.GetUnpaidPaymentTotalAsync(customer.Id);
        if (SelectedCustomer?.Id != customer.Id || IsBarter)
            return;

        if (unpaidPaymentTotal < unpaidPaymentLimit)
        {
            IsOrderBlockedByUnpaidPaymentLimit = false;
            return;
        }

        IsOrderBlockedByUnpaidPaymentLimit = true;

        MessageBox.Show(
            $"У клиента {customer.FullName} сумма неоплаченных платежей {unpaidPaymentTotal:N2}.\n" +
            $"Лимит для этого клиента: {unpaidPaymentLimit:N2}.\n" +
            "Сначала нужно закрыть неоплаченные платежи, затем можно оформить новый заказ.",
            "Лимит неоплаченных платежей достигнут",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private async Task UpdateCustomerLevelIndicatorAsync()
    {
        if (SelectedCustomer == null)
        {
            CustomerLevelArrow = null;
            CustomerLevelArrowColor = Brushes.Transparent;
            ShowCustomerLevelArrow = false;
            CustomerLast30DaysTotal = 0;
            CustomerCalculatedPriceType = null;
            return;
        }

        var info = await _customerService.GetCustomerLevelChangeAsync(SelectedCustomer.Id);

        if (info == null)
        {
            CustomerLevelArrow = null;
            CustomerLevelArrowColor = Brushes.Transparent;
            ShowCustomerLevelArrow = false;
            CustomerLast30DaysTotal = 0;
            CustomerCalculatedPriceType = null;
            return;
        }

        CustomerLast30DaysTotal = info.Last30DaysTotal;
        CustomerCalculatedPriceType = info.CalculatedPriceType;

        if (info.IsUp)
        {
            CustomerLevelArrow = "▲";
            CustomerLevelArrowColor = Brushes.Green;
            ShowCustomerLevelArrow = true;
        }
        else if (info.IsDown)
        {
            CustomerLevelArrow = "▼";
            CustomerLevelArrowColor = Brushes.Red;
            ShowCustomerLevelArrow = true;
        }
        else
        {
            CustomerLevelArrow = null;
            CustomerLevelArrowColor = Brushes.Transparent;
            ShowCustomerLevelArrow = false;
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

        foreach (var product in ProductList
			.Where(p => p.Alternative == alternative)
			.GroupBy(p => p.ArticleNumber)
			.Select(g => g.First()))
        {
            AlternativeProductList.Add(product);
        }

    }

    public event Action OnProductAdded;

    private bool CanAddToChart(ProductModel? product) => IsOrderCustomerReady &&
        (IsBarter || (IsCustomerFinanceReady && IsOfficialPlanReady)) && product?.Quentity > 0;

	[RelayCommand(CanExecute = nameof(CanAddToChart))]
    private void AddToChart(ProductModel product)
    {
        
		if (product == null)
            return;


        if (SelectedCustomer == null)
        {
            MessageBox.Show("Для оформление заказа, сперва выберите клиента.", "Ошибка");
            return;
        }

        if (!IsBarter && !IsCustomerFinanceReady)
        {
            MessageBox.Show("Дождитесь загрузки остатка клиента. Если загрузка не удалась, повторно выберите клиента.",
                "Остаток клиента", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // ❗ Product quantity check
        if (!IsBarter && !IsOfficialPlanReady)
        {
            MessageBox.Show("Дождитесь проверки месячного плана клиента.", "Месячный план", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (product.Quentity == null || product.Quentity == 0)
        {
            MessageBox.Show(
                "Товар отсутствует на складе. Пожалуйста, выберите альтернативный товар.",
                "Невозможно добавить",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        // ❗ Prevent duplicates
        if (SelectedProductList.Contains(product))
        {
            MessageBox.Show(
                "Этот товар уже добавлен в корзину.",
                "Информация",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        if (IsBuyLimitActive() && TotalSum >= GetMaximumOrderTotal())
        {
            ShowBuyLimitReachedMessage();
            return;
        }
        if (ActiveOfficialOrderLimit.HasValue && TotalSum >= ActiveOfficialOrderLimit.Value)
        {
            ShowMonthlyPlanLimitReachedMessage();
            return;
        }

        product.OrderQuentity = 0;
        product.BarterPriceSom = null;

        // Add to chart
        SelectedProductList.Add(product);

        OnProductAdded?.Invoke();
    }


    [RelayCommand]
	private void RemoveProduct(ProductModel product)
	{
		if (product != null && SelectedProductList.Contains(product))
		{
			SelectedProductList.Remove(product);
		}
	}

    public event Action<ProductModel>? OnInvalidQuantityFound;

    private bool CanClearCart() => SelectedProductList.Count > 0 && !_isSavingOrder;

    [RelayCommand(CanExecute = nameof(CanClearCart))]
    private void ClearCart()
    {
        if (!CanClearCart() || !ConfirmCartClear()) return;
        SelectedProductList.Clear();
    }

    protected virtual bool ConfirmCartClear() => MessageBox.Show(
        "Вы действительно хотите очистить корзину?", "Очистка корзины",
        MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;


    [RelayCommand(CanExecute = nameof(CanSaveOrder))]

    private async Task SaveOrder()
    {
        var isBarterOrder = IsBarter;
        if (HasPendingProductRequests)
        {
            MessageBox.Show("Сначала повторите отправку несохранённых заявок на недостающее количество.", "Заявки на товары");
            return;
        }
        if ((isBarterOrder && !CanUseBarter) || (DirectFromStock && !CanUseDirectFromStock))
        {
            MessageBox.Show("Нет права на выбранный режим заказа.", "Права доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (DirectFromStock)
        {
            var confirm = MessageBox.Show(
                "Вы выбрали оформление заказа напрямую со склада.\n\n" +
                "В этом случае количество товара НЕ будет списано со склада.\n\n" +
                "Вы уверены, что хотите продолжить?",
                "Подтверждение оформления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;
        }

        _isSavingOrder = true;
        OnPropertyChanged(nameof(CanChangeBarterMode));
        ClearCartCommand.NotifyCanExecuteChanged();
        try
        {
            if (SelectedProductList == null || !SelectedProductList.Any())
            {
                MessageBox.Show("Список продуктов пуст. Пожалуйста, добавьте хотя бы один товар.", "Ошибка");
                return;
            }

			if (SelectedCustomer == null)
            {
                MessageBox.Show("Для оформление заказа, сперва выберите клиента.", "Ошибка");
                return;
            }

            var customerForOrder = SelectedCustomer;
            var userForOrder = _userSessionService?.UserId ?? 0;
            if (_retailManagerService != null)
            {
                await _retailManagerService.EnsureCustomerAccessAsync(customerForOrder.Id);
                if (!IsRetailManagerContextReady || !CustomerFilter(customerForOrder) || SelectedCustomer != customerForOrder) return;
            }
            if (!isBarterOrder)
            {
                if (!await RefreshAvailableRestrictionAsync(customerForOrder) || SelectedCustomer != customerForOrder)
                    return;
                if (!await RefreshMonthlyPlanStateAsync(enforceCart: false) || SelectedCustomer != customerForOrder)
                    return;
            }
            if (IsBarter != isBarterOrder) return;

            var unpaidPaymentLimit = customerForOrder.UnpaidPaymentLimit ?? 0;
            if (!isBarterOrder && unpaidPaymentLimit > 0)
            {
                var unpaidPaymentTotal = await _orderService.GetUnpaidPaymentTotalAsync(customerForOrder.Id);
                if (SelectedCustomer != customerForOrder)
                    return;
                if (unpaidPaymentTotal >= unpaidPaymentLimit)
                {
                    MessageBox.Show(
                        $"У клиента {customerForOrder.FullName} сумма неоплаченных платежей {unpaidPaymentTotal:N2}.\n" +
                        $"Лимит для этого клиента: {unpaidPaymentLimit:N2}.\n" +
                        "Сначала нужно закрыть неоплаченные платежи, затем можно оформить новый заказ.",
                        "Лимит неоплаченных платежей достигнут",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            // ❗ Validate that every product has quantity > 0
            var noQuantityProduct = SelectedProductList
                .FirstOrDefault(p => p.OrderQuentity <= 0);

            if (noQuantityProduct != null)
            {
                MessageBox.Show(
                    $"Для оформления заказа у каждого товара должно быть указано количество.\n" +
                    $"Проверьте позицию: {noQuantityProduct.ArticleNumber} — {noQuantityProduct.ProductName}.",
                    "Количество не указано",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                foreach (var item in SelectedProductList)
                    item.IsInvalid = item.OrderQuentity <= 0;

                OnInvalidQuantityFound?.Invoke(noQuantityProduct);
                return;
            }


            // ✅ Validate barter prices
            if (isBarterOrder)
            {
                var noPrice = SelectedProductList
                    .FirstOrDefault(p => p.OrderQuentity <= 0 || p.BarterPriceSom is not >= 0);
                if (noPrice != null)
                {
                    MessageBox.Show(
                        $"В режиме бартера у каждой позиции должны быть указаны неотрицательная цена и количество > 0.\n" +
                        $"Проверьте товар: {noPrice.ArticleNumber} — {noPrice.ProductName}.",
                        "Ошибка");
                    return;
                }
            }

            // Compute totals
            var totalSum = SelectedProductList.Sum(p =>
                ((isBarterOrder ? p.BarterPriceSom : p.CustomerPriceSom) ?? 0m) * p.OrderQuentity);
            if (totalSum <= 0)
            {
                MessageBox.Show("Сумма заказа не может быть 0. Пожалуйста, проверьте количество заказа.", "Ошибка");
                return;
            }

            if (!isBarterOrder && IsBuyLimitExceeded(totalSum))
            {
                ShowBuyLimitReachedMessage();
                return;
            }

            // Only validate courier/storekeeper if NOT barter
            if (!isBarterOrder)
            {
                if (!HasValidCourier)
                {
                    MessageBox.Show("Для оформление заказа, сперва укажите доставщика.", "Ошибка");
                    return;
                }
                if (!HasValidStorekeeper)
                {
                    MessageBox.Show("Для оформление заказа, сперва укажите складчика.", "Ошибка");
                    return;
                }
            }

            if (!isBarterOrder && IsMonthlyPlanLimitExceeded(totalSum))
            {
                EnforceMonthlyPlanLimit();
                return;
            }

            var orderEntity = new OrderEntity
            {
                Date = DateTime.Now,
                Rate = ExchangeRate,
                UserId = _userSessionService.UserId,
                CustomerId = customerForOrder.Id,
                WithoutInvoice = IsSellingWithoutInvoice,
                DirectFromStock = DirectFromStock,
                OfficialOrder = !isBarterOrder && OfficialOrder,
                Stock = false,
                IsPaid = false,
                IsBarter = isBarterOrder,
                SuminWords = NumberToWordsConverter.ConvertToRussianWords(totalSum),
                OrderDetails = SelectedProductList.Select(p => new OrderDetailEntity
                {
                    ArticleNumber = p.ArticleNumber,
                    Quentity = p.OrderQuentity,
                    Price = (isBarterOrder ? p.BarterPriceSom : p.CustomerPriceSom) ?? 0m
                }).ToList()
            };

            if (!isBarterOrder)
            {
                orderEntity.CourierId = SelectedCourier!.Id;
                orderEntity.StorekeeperId = SelectedStorekeeper!.Id;
            }

            if (_retailManagerService != null)
            {
                await _retailManagerService.EnsureCustomerAccessAsync(customerForOrder.Id);
                if (!IsRetailManagerContextReady || _userSessionService.UserId != userForOrder ||
                    SelectedCustomer != customerForOrder || !CustomerFilter(customerForOrder)) return;
            }
            var result = await _orderService.AddOrderAsync(orderEntity);

            if (result == null)
            {
                MessageBox.Show("По некоторым причинам заказ не оформлен, попробуйте еще раз!", "Оформление заказа");
                return;
            }

            // ✅ Success message
            MessageBox.Show("Заказ успешно оформлен!", "Оформление заказа");

            SelectedProductList.Clear();
			DirectFromStock = false;
            await RefreshProductsAsync();
            await LoadInactiveCustomersAsync();

            // The order service has already saved the barter payment in the same transaction.
            if (isBarterOrder)
            {
                var savedOrder = await _orderService.GetOrderAsync(result.Id);
                if (savedOrder != null)
                {
                    _dataTransferService.CustomerId = customerForOrder.Id;
                    _dataTransferService.SelectedOrder = new CustomerOrder
                    {
                        Id = savedOrder.Id,
                        Date = savedOrder.Date,
                        Rate = savedOrder.Rate,
                        IsBarter = savedOrder.IsBarter,
                        SuminWords = savedOrder.SuminWords,
                        CustomerFullName = savedOrder.Customer?.FullName,
                        CustomerAddress = savedOrder.Customer?.Address,
                        CustomerPhoneNumber = savedOrder.Customer?.MobilePhone,
                        CustomerCity = savedOrder.Customer?.City,
                        CustomerRegion = savedOrder.Customer?.Region,
                        Customer = savedOrder.Customer == null ? null : (Customer)savedOrder.Customer,
                        UserFullName = $"{savedOrder.User?.FirstName} {savedOrder.User?.LastName}",
                        OrderDetails = savedOrder.OrderDetails?.Select(d => new OrderDetail
                        {
                            ArticleNumber = d.ArticleNumber,
                            ProductName = d.Product?.ProductName,
                            BrandName = d.Product?.Brand?.BrandName,
                            Marka = d.Product?.Marka,
                            Model = d.Product?.Model,
                            Quentity = d.Quentity,
                            Price = d.Price,
                            Total = d.Quentity * d.Price
                        }).ToList() ?? []
                    };

                    var barterVm = _serviceProvider.GetRequiredService<OrderInvoiceViewModel>();
                    await barterVm.LoadInvoiceData();

                    var pdfService = _serviceProvider.GetRequiredService<PdfService>();
                    var pdfBytes = pdfService.GeneratePdfBytes(ReportType.BarterInvoice, barterVm);

                    var preview = new DocumentPreviewView();
                    preview.LoadPdf(pdfBytes);

                    var window = new Window
                    {
                        Title = $"Бартерный документ № {savedOrder.Id}",
                        Content = preview,
                        Width = 900,
                        Height = 900,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };
                    window.ShowDialog();
                }
            }
            await RefreshAvailableRestrictionAsync(SelectedCustomer);
            await RefreshMonthlyPlanStateAsync();
        }
        catch (OfficialMonthlyPlanLimitException ex)
        {
            await RefreshMonthlyPlanStateAsync(enforceCart: false);
            EnforceMonthlyPlanLimit(showMessage: false);
            MessageBox.Show(ex.Message, "Месячный план", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при оформлении заказа: {ex.Message}", "Ошибка");
        }
        finally
        {
            if (SelectedProductList.Count == 0) SetBarterMode(false);
            _isSavingOrder = false;
            OnPropertyChanged(nameof(CanChangeBarterMode));
            ClearCartCommand.NotifyCanExecuteChanged();
        }
    }


    private void OnProductModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
        if (e.PropertyName == nameof(ProductModel.BarterPriceSom))
            SaveOrderCommand.NotifyCanExecuteChanged();
		if (e.PropertyName == nameof(ProductModel.Total))
		{
			UpdateTotalSum();
            if (sender is ProductModel product)
                EnforceBuyLimit(product);
		}
	}

	public void UpdateTotalSum()
	{
		TotalSum = SelectedProductList.Sum(p => p.Total);
        SaveOrderCommand.NotifyCanExecuteChanged();
	}

    private bool IsBuyLimitActive()
    {
        return OrderAllowance?.MaximumTotal.HasValue == true;
    }

    private decimal GetMaximumOrderTotal()
    {
        return OrderAllowance?.MaximumTotal ?? 0m;
    }

    private bool IsBuyLimitExceeded(decimal total)
    {
        return OrderAllowance?.IsExceeded(total) == true;
    }

    private void EnforceBuyLimit(ProductModel? changedProduct = null)
    {
        if (_isAdjustingProductLimit) return;
        if (IsBuyLimitExceeded(TotalSum))
        {
            ReduceCartToLimit(GetMaximumOrderTotal(), changedProduct);
            ShowBuyLimitReachedMessage();
        }
        EnforceMonthlyPlanLimit(changedProduct);
    }

    private bool ReduceCartToLimit(decimal limit, ProductModel? changedProduct = null)
    {
        if (_isAdjustingProductLimit || TotalSum <= limit) return false;
        _isAdjustingProductLimit = true;
        try
        {
            // Reduce the edited row first; after a limit change, reduce the newest rows first.
            var products = SelectedProductList.Reverse();
            if (changedProduct != null)
                products = new[] { changedProduct }.Concat(products.Where(p => p != changedProduct));
            foreach (var product in products)
            {
                if (TotalSum <= limit) break;
                if (product.OrderQuentity <= 0 || product.Total <= 0) continue;

                var availableForProduct = Math.Max(0m, limit - (TotalSum - product.Total));
                var unitPrice = product.Total / product.OrderQuentity;
                product.OrderQuentity = (int)Math.Min(product.OrderQuentity, Math.Floor(availableForProduct / unitPrice));
                UpdateTotalSum();
                // A rounded line total can slightly change the inferred unit price.
                while (product.OrderQuentity > 0 && product.Total > availableForProduct)
                {
                    product.OrderQuentity--;
                    UpdateTotalSum();
                }
            }
        }
        finally
        {
            _isAdjustingProductLimit = false;
        }

        return true;
    }

    protected virtual void ShowBuyLimitReachedMessage()
    {
        var allowance = OrderAllowance;
        if (allowance?.MaximumTotal == null) return;

        MessageBox.Show(
            "Вы не можете превышать допустимую сумму заказа.\n\n" +
            $"Доступное ограничение клиента: {allowance.AvailableRestriction:N2}\n" +
            (allowance.BuyLimit is > 0
                ? $"Лимит суммы заказа с предоплатой: {allowance.BuyLimit.Value + allowance.Prepayment:N2}\n"
                : "") +
            $"Максимальная сумма заказа: {allowance.MaximumTotal:N2}\n" +
            $"Сумма в корзине: {TotalSum:N2}\n" +
            $"Можно добавить еще на сумму: {allowance.Remaining(TotalSum):N2}\n\n" +
            "Для продажи сверх ограничения требуется соответствующее право.",
            "Лимит суммы заказа достигнут",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

	private bool FilterProducts(object item)
	{
		if (item is ProductModel product)
		{
			if (string.IsNullOrWhiteSpace(FilterText))
			{
				return true;
			}

            if (_exactArticleFilter != null)
                return string.Equals(product.ArticleNumber, _exactArticleFilter, StringComparison.OrdinalIgnoreCase);

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
        // Resolve the whole article once per search, before considering individual keywords.
        var searchText = FilterText?.Trim();
        _exactArticleFilter = string.IsNullOrEmpty(searchText) ? null : ProductList
            .FirstOrDefault(p => string.Equals(p.ArticleNumber, searchText, StringComparison.OrdinalIgnoreCase))?.ArticleNumber;
		ProductListView?.Refresh();
        UpdateProductRequestPrompt();
	}
	
	
	private bool CustomerFilter(object obj)
	{
		if (obj is not Customer c) return false;

        if (IsOnlyOfficialCustomer && !c.OfficialCustomer)
            return false;

        if (!IsRetailManagerContextReady) return false;

		// Manager selected -> filter by manager (and optionally by territory)
		bool byManager = SelectedSalesManager == null || c.SalesManagerId == SelectedSalesManager.Id;
		bool byTerritory = string.IsNullOrWhiteSpace(SelectedTerritory)
						   || string.Equals(c.Territory, SelectedTerritory, StringComparison.OrdinalIgnoreCase);

		return byManager && byTerritory;
	}

	private void RebuildTerritories()
	{
		// keep the same collection instance so binding updates automatically
		TerritoryOptions.Clear();
		TerritoryOptions.Add(new TerritoryOption { Value = null, Display = "Все территории" });

			var terrs = AllCustomers
				.Where(c => (SelectedSalesManager == null || c.SalesManagerId == SelectedSalesManager.Id) &&
                    (!IsOnlyOfficialCustomer || c.OfficialCustomer))
				.Select(c => c.Territory)
				.Where(t => !string.IsNullOrWhiteSpace(t))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(t => t);

			foreach (var t in terrs)
				TerritoryOptions.Add(new TerritoryOption { Value = t, Display = t! });

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
            if (_isSavingOrder)
            {
                OnPropertyChanged(nameof(IsBarter));
                return;
            }
            if (value && !CanChangeBarterMode)
            {
                OnPropertyChanged(nameof(IsBarter));
                return;
            }
            SetBarterMode(value);
        }
    }

    private void SetBarterMode(bool value, bool refreshOrdinaryState = true)
    {
        if (_isBarter == value) return;
        _isBarter = value;
        OnPropertyChanged(nameof(IsBarter));
        if (value) OfficialOrder = false;
        var wasAdjusting = _isAdjustingProductLimit;
        _isAdjustingProductLimit = true;
        try
        {
            foreach (var item in SelectedProductList)
            {
                item.IsBarterPriceMode = value;
                item.BarterPriceSom = null;
            }
        }
        finally { _isAdjustingProductLimit = wasAdjusting; }
        if (!value && refreshOrdinaryState)
        {
            _ = RefreshMonthlyPlanStateAsync();
            if (!_isSavingOrder)
            {
                _ = WarnIfUnpaidPaymentLimitReachedAsync(SelectedCustomer);
                EnforceBuyLimit();
            }
        }
        AddToChartCommand.NotifyCanExecuteChanged();
        SaveOrderCommand.NotifyCanExecuteChanged();
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
        _dataTransferService.PaymentCourierId = SelectedCourier?.Id;

		var vm = _serviceProvider.GetRequiredService<SummaryViewModel>();

		await vm.LoadAsync();
		
		_dialogService.Show(vm);

        await RefreshAvailableRestrictionAsync(SelectedCustomer);
	}

	public async Task SelectCustomerByIdAsync(string customerId)
	{
		if (string.IsNullOrWhiteSpace(customerId)) return;
        if (!CanChangeCustomer(customerId, nameof(SelectedCustomer))) return;
        if (_retailManagerService != null)
        {
            if (!IsRetailManagerContextReady) return;
            var allowed = AllCustomers.FirstOrDefault(c => c.Id == customerId && CustomerFilter(c));
            if (allowed != null) SelectedCustomer = allowed;
            return;
        }

		// ensure list is ready
		if (AllCustomers == null || AllCustomers.Count == 0)
			await InitializeAsync();

		var existing = AllCustomers.FirstOrDefault(c => c.Id == customerId);
		if (existing != null && CustomerFilter(existing))
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
    //private async Task LoadInactiveCustomersAsync()
    //{
    //	InactiveDebtCustomers.Clear();
    //	InactivePlannedCustomers.Clear();

    //	InactiveDebtCount = 0;
    //	InactivePlannedCount = 0;

    //	if (SelectedSalesManager == null)
    //		return;

    //	var managerId = SelectedSalesManager.Id;
    //	var territory = SelectedTerritory;

    //	// 1) Customers with unpaid debts
    //	var debtList = await _financeService.NeaktivOtEzhigodPogashenieAsync(managerId, territory);
    //	foreach (var c in debtList)
    //		InactiveDebtCustomers.Add(c);

    //	InactiveDebtCount = InactiveDebtCustomers.Count;

    //	// 2) Customers not buying as planned
    //	var plannedList = await _financeService.NeaktivNeZakupAsync(managerId, territory);
    //	foreach (var c in plannedList)
    //		InactivePlannedCustomers.Add(c);

    //	InactivePlannedCount = InactivePlannedCustomers.Count;
    //}

    private async Task<bool> LoadInactiveCustomersAsync()
    {
        var version = ++_inactiveRequestVersion;
        var scopeVersion = _managerScopeVersion;
        var userId = _userSessionService?.UserId;
        var managerId = SelectedSalesManager?.Id;
        var territory = SelectedTerritory;
        ClearInactiveCustomers();
        if (!IsRetailManagerContextReady || _financeService == null || AllCustomers.Count == 0) return false;
        try
        {
            var debtList = await _financeService.NeaktivOtEzhigodPogashenieAsync(managerId, territory);
            var plannedList = await _financeService.NeaktivNeZakupAsync(managerId, territory);
            if (version != _inactiveRequestVersion || scopeVersion != _managerScopeVersion || userId != _userSessionService?.UserId ||
                managerId != SelectedSalesManager?.Id || territory != SelectedTerritory ||
                !IsRetailManagerContextReady) return false;
            var visible = AllCustomers.Where(c => CustomerFilter(c)).Select(c => c.Id).ToHashSet();
            foreach (var row in debtList.Where(c => visible.Contains(c.CustomerId))) InactiveDebtCustomers.Add(row);
            foreach (var row in plannedList.Where(c => visible.Contains(c.CustomerId))) InactivePlannedCustomers.Add(row);
            InactiveDebtCount = InactiveDebtCustomers.Count; InactivePlannedCount = InactivePlannedCustomers.Count;
            UpdateCustomerPurchaseIndicators();
            return true;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); return false; }
    }

    private void UpdateCustomerPurchaseIndicators()
    {
        var customerId = SelectedCustomer?.Id;
        RequiredPurchase = InactiveDebtCustomers.FirstOrDefault(c => c.CustomerId == customerId)?.Shortfall ?? 0;
        PlannedPurchase = InactivePlannedCustomers.FirstOrDefault(c => c.CustomerId == customerId)?.Shortfall ?? 0;
    }


    private async Task RefreshProductsAsync()
	{
		ProductList.Clear();
		var productDtos = await _productService.GetAllProductAsync();

		foreach (var dto in productDtos)
		{
			var pm = (ProductModel)dto;
			pm.ExchangeRate = ExchangeRate;
			pm.RefreshPrices();
			ProductList.Add(pm);
		}
		ApplyCustomerLevelPrice();
        ApplyFilter();
	}

	private InactiveCustomerDto? _selectedRequiredPurchaseCombo;
	public InactiveCustomerDto? SelectedRequiredPurchaseCombo
	{
		get => _selectedRequiredPurchaseCombo;
		set
		{
			if (_selectedRequiredPurchaseCombo == value) return;
            if (value != null && !CanChangeCustomer(value.CustomerId, nameof(SelectedRequiredPurchaseCombo))) return;
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
            if (value != null && !CanChangeCustomer(value.CustomerId, nameof(SelectedPlannedPurchaseCombo))) return;
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

    [RelayCommand]
    private void ShowPendingOrders()
    {
        var vm = _serviceProvider.GetRequiredService<PendingOrdersViewModel>();
        vm.ManagerId = SelectedSalesManager?.Id;
        _dialogService.Show(vm);
    }

    [RelayCommand(CanExecute = nameof(CanOpenDelivery))]
    private void ShowDeliveryOrders()
    {
        if (!CanOpenDelivery) return;
        var vm = _serviceProvider.GetRequiredService<DeliveryOrdersViewModel>();
        _dialogService.Show(vm);
    }

    [RelayCommand(CanExecute = nameof(CanAssignPickers))]
    private void ShowAssignPickers()
    {
        if (!CanAssignPickers) return;
        var vm = _serviceProvider.GetRequiredService<AssignPickersViewModel>();
        _dialogService.Show(vm);
    }

    public void ResetState()
    {
        ResetProductRequestEntry();
        SelectedProductList.Clear();
        SelectedCustomer = null;
        SelectedCourier = null;
        SelectedStorekeeper = null;
        FilterText = "";
        SearchAllWords = true;
        SetSalesManagerCore(SalesManagerList.FirstOrDefault(m => m.Id == _retailManagerState?.OwnManagerId));
        SelectedTerritory = null;

        RequiredPurchase = 0;
        PlannedPurchase = 0;
        RetailFiltersChanged();
    }

}

