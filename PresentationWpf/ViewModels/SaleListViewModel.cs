
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace PresentationWpf.ViewModels;

public partial class SaleListViewModel(IServiceProvider serviceProvider) : ObservableObject
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
	
	[ObservableProperty]
	private RetailViewModel retailViewModelInstance = null!;

	[ObservableProperty]
	private WholesaleViewModel wholesaleViewModelInstance = null!;

	public async Task InitializeAsync()
	{
		RetailViewModelInstance = _serviceProvider.GetRequiredService<RetailViewModel>();
		await RetailViewModelInstance.InitializeAsync();

		WholesaleViewModelInstance = _serviceProvider.GetRequiredService<WholesaleViewModel>();
		//await WholesaleViewModelInstance.InitializeAsync();
	}
}
