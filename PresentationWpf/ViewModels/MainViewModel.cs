
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace PresentationWpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;

    public MainViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        NavigateToInitialView();
    }

	[ObservableProperty]
	private ObservableObject _currentViewModel = null!;

	
	// used by XAML to highlight the active nav item
	public string CurrentViewKey => _currentViewModel?.GetType().Name ?? string.Empty;

	[ObservableProperty]
	public bool _isLoggedIn = false;
	private void NavigateToInitialView()
    {
        if (IsLoggedIn) 
            CurrentViewModel = _serviceProvider.GetRequiredService<WelcomeViewModel>();
		CurrentViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();

	}

	[RelayCommand]
	private void NavigateToProductsList()
	{
		var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
		//mainViewModel.CurrentViewModel = _serviceProvider.GetRequiredService<ProductListViewModel>();
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
		CurrentViewModel = saleListViewModel;
	}

    [RelayCommand]
    private void NavigateCustomersList()
    {
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        mainViewModel.CurrentViewModel = _serviceProvider.GetRequiredService<CustomerViewModel>();
    }

    [RelayCommand]
    private void NavigateToOrdersList()
    {
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        //mainViewModel.CurrentViewModel = _serviceProvider.GetRequiredService<OrderListViewModel>();
    }

	[RelayCommand]
	private void NavigateToReturn()
	{
		var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
		mainViewModel.CurrentViewModel = _serviceProvider.GetRequiredService<ReturnViewModel>();
	}

	
}
