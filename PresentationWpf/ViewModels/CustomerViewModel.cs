using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Services;
using System.Collections.ObjectModel;
using System.Windows;



namespace PresentationWpf.ViewModels;

public partial class CustomerViewModel : ObservableObject
{
	private readonly CustomerService _customerService;
	private readonly CoefficientService _coefficientService;
	public CustomerViewModel(CustomerService customerService, CoefficientService coefficientService)
	{
		_customerService = customerService;
		_coefficientService = coefficientService;
		LoadData();
	}

	[ObservableProperty]
	private ObservableCollection<Customer> customers = [];

	[ObservableProperty]
	private Customer? selectedCustomer;
	
	[ObservableProperty]
	public ObservableCollection<PriceLevelEntity> priceLevels = [];

	[ObservableProperty]
	private ObservableCollection<SalesManagerEntity> salesManagers = [];
	
	[ObservableProperty]
	public ObservableCollection<string> distinctCities = [];
	
	[ObservableProperty]
	public ObservableCollection<string> distinctRegions  = [];
	
	[ObservableProperty]
	public ObservableCollection<string> distinctTerritories  = [];

	private async void LoadData()
	{
		await LoadPriceLevelsAsync();
		await LoadSalesManagersAsync();
		await LoadCustomersAsync();
	}

	private async Task LoadCustomersAsync()
	{
		var customerList = await _customerService.GetAllCustomersAsync(); 

		foreach (var customer in customerList)
		{
			Customers.Add(customer);
		}

		UpdateDistinctLists();
	}

	private async Task LoadPriceLevelsAsync()
	{
		var levels = await _customerService.GetAllPriceLevelsAsync();
		PriceLevels = new ObservableCollection<PriceLevelEntity>(levels);
	}

	private async Task LoadSalesManagersAsync()
	{
		var managers = await _customerService.GetAllSalesManagersAsync();
		SalesManagers = new ObservableCollection<SalesManagerEntity>(managers);
	}


	[RelayCommand]
	private void CreateCustomer()
	{
		SelectedCustomer = new CustomerEntity(); 
	}

	[RelayCommand]
	private async Task SaveCustomerAsync()
	{
		if (SelectedCustomer is null)
			return;

		var savedCustomer = await _customerService.AddCustomerAsync(SelectedCustomer);

		// If not already in list, add it
		if (!Customers.Any(c => c.Id == savedCustomer.Id))
			Customers.Add(savedCustomer);

		var updated = await _customerService.GetCustomerByIdAsync(savedCustomer.Id);
		if (updated != null)
			SelectedCustomer = updated;
		MessageBox.Show("Клиент успешно сохранён.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
	}

	[RelayCommand]
	private async Task DeleteCustomerAsync()
	{
		if (SelectedCustomer is null)
			return;

		await _customerService.DeleteCustomerAsync(SelectedCustomer);

		var customerToRemove = Customers.FirstOrDefault(c => c.Id == SelectedCustomer.Id);
		if (customerToRemove is not null)
		{
			Customers.Remove(customerToRemove);
			MessageBox.Show("Клиент успешно удалён.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
		} 

		SelectedCustomer = null;
	}

	private void UpdateDistinctLists()
	{
		var cities = (Customers ?? Enumerable.Empty<Customer>())
			.Where(c => !string.IsNullOrWhiteSpace(c.City))
			.Select(c => c.City!)
			.Distinct()
			.OrderBy(c => c);

		DistinctCities = new ObservableCollection<string>(cities);

		var regions = (Customers ?? Enumerable.Empty<Customer>())
			.Where(c => !string.IsNullOrWhiteSpace(c.Region))
			.Select(c => c.Region!)
			.Distinct()
			.OrderBy(r => r);

		DistinctRegions = new ObservableCollection<string>(regions);

		var territories = (Customers ?? Enumerable.Empty<Customer>())
			.Where(c => !string.IsNullOrWhiteSpace(c.Territory))
			.Select(c => c.Territory!)
			.Distinct()
			.OrderBy(t => t);

		DistinctTerritories = new ObservableCollection<string>(territories);
	}

	[RelayCommand(CanExecute = nameof(CanCalculateCoefficients))]
	private async Task CalculateCoefficientsForCustomerAsync()
	{
		if (SelectedCustomer is null) return;

		await _coefficientService.CalculateEzhPogashForCustomerAsync(SelectedCustomer.Id);
		await _coefficientService.CalculateZakupForCustomerAsync(SelectedCustomer.Id);
		await _coefficientService.CalculateZaplanZakupForCustomerAsync(SelectedCustomer.Id);

		var updated = await _customerService.GetCustomerByIdAsync(SelectedCustomer.Id);
		if (updated != null)
			SelectedCustomer = updated;
	}

	// This is called automatically to check if the button should be enabled
	private bool CanCalculateCoefficients() => SelectedCustomer is not null;

	// Refresh CanExecute when SelectedCustomer changes
	partial void OnSelectedCustomerChanged(Customer? value)
	{
		CalculateCoefficientsForCustomerCommand.NotifyCanExecuteChanged();
	}

}
