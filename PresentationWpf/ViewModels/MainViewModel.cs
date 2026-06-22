
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PresentationWpf.Services;
using System.Diagnostics;
using System.Security;

namespace PresentationWpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDbContextFactory<DatabaseContext> _dbFactory;
    public PermissionService PermissionService { get; }
    public MainViewModel(IServiceProvider serviceProvider, IDbContextFactory<DatabaseContext> dbFactory, PermissionService permissionService)
    {
        _serviceProvider = serviceProvider;
        _dbFactory = dbFactory;
        PermissionService = permissionService;

        _ = NavigateToInitialView();
    }

	[ObservableProperty]
	private ObservableObject _currentViewModel = null!;


    public bool CanViewOrders => PermissionService.Has("Orders");
    public bool CanViewReturns => PermissionService.Has("Returns");
    public bool CanViewCustomers => PermissionService.Has("Customers");
    public bool CanViewInventory => PermissionService.Has("Inventory");
    public bool CanViewExpenses => PermissionService.Has("Expenses");
    public bool CanViewReports => PermissionService.Has("Reports");
    public bool CanViewStatistics => PermissionService.Has("Statistics");
    public bool CanViewAdmin => PermissionService.Has("Admin");
    public bool CanViewSettings => PermissionService.Has("Settings");

    public void RefreshPermissions()
    {
        OnPropertyChanged(nameof(CanViewOrders));
        OnPropertyChanged(nameof(CanViewReturns));
        OnPropertyChanged(nameof(CanViewCustomers));
        OnPropertyChanged(nameof(CanViewInventory));
        OnPropertyChanged(nameof(CanViewExpenses));
        OnPropertyChanged(nameof(CanViewReports));
        OnPropertyChanged(nameof(CanViewStatistics));
        OnPropertyChanged(nameof(CanViewAdmin));
        OnPropertyChanged(nameof(CanViewSettings));
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
    private void NavigateCustomersList()
    {
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        var customerVM = _serviceProvider.GetRequiredService<CustomerViewModel>();
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
    public void GoHome()
    {
        CurrentViewModel = _serviceProvider.GetRequiredService<WelcomeViewModel>(); // clears content
    }

    [RelayCommand]
    private void Logout()
    {
         // Update login state
        IsLoggedIn = false;

        // Navigate to LoginView
        CurrentViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();
    }

}
