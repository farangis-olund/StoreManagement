
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
    public bool CanViewReturns => PermissionService.Has(PermissionCatalog.Returns);
    public bool CanViewCustomers => PermissionService.Has(PermissionCatalog.Customers);
    public bool CanViewInventory => PermissionService.Has(PermissionCatalog.Inventory);
    public bool CanViewProductRequests => PermissionService.Has(PermissionCatalog.ProductRequests);
    public bool CanViewExpenses => PermissionService.Has(PermissionCatalog.Expenses);
    public bool CanViewReports => PermissionService.Has(PermissionCatalog.Reports);
    public bool CanViewStatistics => PermissionService.Has(PermissionCatalog.Statistics);
    public bool CanViewAdmin => PermissionService.Has(PermissionCatalog.Admin);
    public bool CanViewSettings => PermissionService.Has(PermissionCatalog.Settings);

    public void RefreshPermissions()
    {
        OnPropertyChanged(nameof(CanViewOrders));
        OnPropertyChanged(nameof(CanViewReturns));
        OnPropertyChanged(nameof(CanViewCustomers));
        OnPropertyChanged(nameof(CanViewInventory));
        OnPropertyChanged(nameof(CanViewProductRequests));
        NavigateToProductRequestsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanViewExpenses));
        OnPropertyChanged(nameof(CanViewReports));
        OnPropertyChanged(nameof(CanViewStatistics));
        OnPropertyChanged(nameof(CanViewAdmin));
        OnPropertyChanged(nameof(CanViewSettings));
        if (CurrentViewModel is ProductRequestsViewModel && !CanViewProductRequests)
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

	[RelayCommand]
	private void NavigateToAdmin()
	{
		var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
		mainViewModel.CurrentViewModel = _serviceProvider.GetRequiredService<AdminViewModel>();
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
