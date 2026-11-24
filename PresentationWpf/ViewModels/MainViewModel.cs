
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PresentationWpf.Services;
using System.Diagnostics;
using System.ComponentModel;
using System.Security;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDbContextFactory<DatabaseContext> _dbFactory;
    public PermissionService PermissionService { get; }
    private readonly SessionPermissionRefresher _permissionRefresher;
    public MainViewModel(IServiceProvider serviceProvider, IDbContextFactory<DatabaseContext> dbFactory, PermissionService permissionService,
        SessionPermissionRefresher permissionRefresher)
    {
        _serviceProvider = serviceProvider;
        _dbFactory = dbFactory;
        PermissionService = permissionService;
        _permissionRefresher = permissionRefresher;
        PropertyChangedEventManager.AddHandler(PermissionService,
            (_, _) => RefreshPermissions(), nameof(PermissionService.CurrentPermissions));

        _ = NavigateToInitialView();
    }

	[ObservableProperty]
	private ObservableObject _currentViewModel = null!;


    public bool CanViewOrders => PermissionService.Has(PermissionCatalog.Orders);
    public bool CanViewExpiry => CanViewOrders || CanViewInventory;
    public bool CanViewReturns => PermissionService.Has(PermissionCatalog.Returns);
    public bool CanViewCustomers => PermissionService.Has(PermissionCatalog.Customers);
    public bool CanViewInventory => PermissionService.Has(PermissionCatalog.Inventory);
    public bool CanViewProductRequests => PermissionService.Has(PermissionCatalog.ProductRequests);
    public bool CanOpenMonthlyPlans => PermissionService.Has(PermissionCatalog.OfficialMonthlyPlansView) ||
        PermissionService.Has(PermissionCatalog.OfficialMonthlyPlansManage);
    public bool CanViewExpenses => PermissionService.Has(PermissionCatalog.Expenses);
    public bool CanViewReports => PermissionService.Has(PermissionCatalog.Reports);
    public bool CanViewStatistics => PermissionService.Has(PermissionCatalog.Statistics);
    public bool CanViewAdmin => PermissionService.Has(PermissionCatalog.Admin);
    public bool CanViewSettings => PermissionService.Has(PermissionCatalog.Settings);

    private bool HasSalesMenuItems => CanViewOrders || CanViewReturns || CanViewCustomers || CanOpenMonthlyPlans;
    private bool HasInventoryMenuItems => CanViewInventory || CanViewProductRequests || CanViewExpenses;
    private bool HasReportsMenuItems => CanViewReports || CanViewStatistics || CanViewAdmin;

    // A separator starts a visible group only when another visible group precedes it.
    public bool ShowInventorySeparator => HasSalesMenuItems && HasInventoryMenuItems;
    public bool ShowReportsSeparator => (HasSalesMenuItems || HasInventoryMenuItems) && HasReportsMenuItems;
    public bool ShowSettingsSeparator => (HasSalesMenuItems || HasInventoryMenuItems || HasReportsMenuItems) && CanViewSettings;

    public void RefreshPermissions()
    {
        OnPropertyChanged(nameof(CanViewOrders));
        OnPropertyChanged(nameof(CanViewExpiry));
        NavigateToExpiryCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanViewReturns));
        OnPropertyChanged(nameof(CanViewCustomers));
        OnPropertyChanged(nameof(CanViewInventory));
        OnPropertyChanged(nameof(CanViewProductRequests));
        OnPropertyChanged(nameof(CanOpenMonthlyPlans));
        NavigateToMonthlyPlansCommand.NotifyCanExecuteChanged();
        NavigateToProductRequestsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanViewExpenses));
        OnPropertyChanged(nameof(CanViewReports));
        OnPropertyChanged(nameof(CanViewStatistics));
        OnPropertyChanged(nameof(CanViewAdmin));
        NavigateToAdminCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanViewSettings));
        OnPropertyChanged(nameof(ShowInventorySeparator));
        OnPropertyChanged(nameof(ShowReportsSeparator));
        OnPropertyChanged(nameof(ShowSettingsSeparator));
        if (CurrentViewModel is ProductRequestsViewModel && !CanViewProductRequests)
            GoHome();
        if (CurrentViewModel is ProductExpiryViewModel && !CanViewExpiry) GoHome();
        if (CurrentViewModel is OfficialMonthlyPlansViewModel && !CanOpenMonthlyPlans)
            GoHome();
        if (CurrentViewModel is AdminViewModel && !CanViewAdmin)
            GoHome();
    }

    public async Task InitializeAsync()
    {
        await NavigateToInitialView();
    }


    // used by XAML to highlight the active nav item
    public string CurrentViewKey => _currentViewModel?.GetType().Name ?? string.Empty;

	[ObservableProperty]
	public bool _isLoggedIn = false;
	private async Task NavigateToInitialView()
    {
        if (IsLoggedIn)
            CurrentViewModel = _serviceProvider.GetRequiredService<WelcomeViewModel>();
        else
        {
            if (await NoUsersAsync())
                CurrentViewModel = _serviceProvider.GetRequiredService<CreateAdminViewModel>();
            else
                CurrentViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();
        }
       
        
    }

    private async Task<bool> NoUsersAsync()
    {
        var db = await _dbFactory.CreateDbContextAsync();

        var count = await db.Users.CountAsync();
        Debug.WriteLine($"User count in DB = {count}");

        return count == 0;
    }



    [RelayCommand]
	private void NavigateToStock()
	{
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        var customerVM = _serviceProvider.GetRequiredService<StockViewModel>();
        customerVM.RefreshPermissions();
        mainViewModel.CurrentViewModel = customerVM;

	}

    [RelayCommand(CanExecute = nameof(CanViewExpiry))]
    private async Task NavigateToExpiry()
    {
        if (!IsLoggedIn || !CanViewExpiry) return;
        try { await _permissionRefresher.RefreshAsync(); }
        catch (Exception)
        {
            MessageBox.Show("Не удалось обновить права доступа. Проверьте подключение к базе данных.", "Права доступа");
            return;
        }
        if (!IsLoggedIn || !CanViewExpiry) return;
        var model = _serviceProvider.GetRequiredService<ProductExpiryViewModel>();
        model.SetPriorityMode(true); model.GoHomeCommand = GoHomeCommand;
        CurrentViewModel = model;
    }

    [RelayCommand(CanExecute = nameof(CanViewProductRequests))]
    private async Task NavigateToProductRequests()
    {
        try
        {
            await _permissionRefresher.RefreshAsync();
        }
        catch (Exception)
        {
            MessageBox.Show("Не удалось обновить права доступа. Проверьте подключение к базе данных.",
                "Права доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!CanViewProductRequests)
        {
            GoHome();
            return;
        }
        CurrentViewModel = _serviceProvider.GetRequiredService<ProductRequestsViewModel>();
    }

    [RelayCommand(CanExecute = nameof(CanOpenMonthlyPlans))]
    private async Task NavigateToMonthlyPlans()
    {
        try { await _permissionRefresher.RefreshAsync(); }
        catch (Exception)
        {
            MessageBox.Show("Не удалось обновить права доступа. Проверьте подключение к базе данных.",
                "Права доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!CanOpenMonthlyPlans) { GoHome(); return; }
        CurrentViewModel = _serviceProvider.GetRequiredService<OfficialMonthlyPlansViewModel>();
    }

	[RelayCommand(CanExecute = nameof(CanViewAdmin))]
	private async Task NavigateToAdmin()
	{
        if (!CanViewAdmin) return;
        try { await _permissionRefresher.RefreshAsync(); }
        catch (Exception)
        {
            MessageBox.Show("Не удалось обновить права доступа. Проверьте подключение к базе данных.",
                "Права доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (CanViewAdmin) CurrentViewModel = _serviceProvider.GetRequiredService<AdminViewModel>();
	}


	[RelayCommand]
	private async Task NavigateToSaleList()
    {
       	var saleListViewModel = _serviceProvider.GetRequiredService<RetailViewModel>();
        await saleListViewModel.InitializeAsync();
		saleListViewModel.ResetState();
        CurrentViewModel = saleListViewModel;
	}

    [RelayCommand]
    private async Task NavigateCustomersList()
    {
        try
        {
            await _permissionRefresher.RefreshAsync();
        }
        catch (Exception)
        {
            MessageBox.Show("Не удалось обновить права доступа. Проверьте подключение к базе данных.",
                "Права доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!CanViewCustomers)
        {
            GoHome();
            return;
        }
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        var customerVM = _serviceProvider.GetRequiredService<CustomerViewModel>();
        await customerVM.LoadDataAsync();
        customerVM.RefreshPermissions();
        mainViewModel.CurrentViewModel = customerVM;
    }

    [RelayCommand]
    private void NavigateToStatistics()
    {
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        mainViewModel.CurrentViewModel = _serviceProvider.GetRequiredService<StatisticsViewModel>();
    }

	[RelayCommand]
	private void NavigateToReturn()
	{
		var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
		mainViewModel.CurrentViewModel = _serviceProvider.GetRequiredService<ReturnViewModel>();
	}

    [RelayCommand]
    private void NavigateToExpenses()
    {
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        var expensesVM  = _serviceProvider.GetRequiredService<ExpenseCrudViewModel>();
        expensesVM.RefreshPermissions();
        mainViewModel.CurrentViewModel = expensesVM;
    }

    [RelayCommand]
    private void NavigateToReports()
    {
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

        mainViewModel.CurrentViewModel = _serviceProvider.GetRequiredService<ReportsViewModel>();
    }

    [RelayCommand]
    private void NavigateToMyNewArticles()
    {
        if (!IsLoggedIn) return;
        var viewModel = _serviceProvider.GetRequiredService<MyNewArticlesViewModel>();
        viewModel.GoHomeCommand = GoHomeCommand;
        CurrentViewModel = viewModel;
    }

    [RelayCommand]
    public void GoHome()
    {
        CurrentViewModel = _serviceProvider.GetRequiredService<WelcomeViewModel>(); // clears content
    }

    [RelayCommand]
    private void Logout()
    {
        _serviceProvider.GetRequiredService<UserSessionService>().Logout();
        PermissionService.SetPermissions(Array.Empty<string>());
         // Update login state
        IsLoggedIn = false;

        // Navigate to LoginView
        CurrentViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();
    }

}
