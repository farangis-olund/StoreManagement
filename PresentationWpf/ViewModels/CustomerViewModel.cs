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

	public CustomerViewModel(CustomerService customerService)
	{
		_customerService = customerService;
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

	
}
