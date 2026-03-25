using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Services;

namespace PresentationWpf.ViewModels;

public partial class ManagerViewModel : ObservableObject
{
	private readonly ManagerService _managerService;
	private readonly CustomerService _customerService;
	private readonly BrandService _brandService;

	public ManagerViewModel(ManagerService managerService,
							CustomerService customerService,
							BrandService brandService)
	{
		_managerService = managerService;
		_customerService = customerService;
		_brandService = brandService;

		_ = InitializeAsync();

	}

	private async Task InitializeAsync()
	{
		// 🟢 Load customers first (needed by other parts)
		await LoadCustomersAsync();

		// Load other data
		await LoadBrandsAsync();
		await LoadManagersAsync();

		// 🟢 Ensure any existing clients (if already loaded) get the customer list
		foreach (var c in Clients)
			c.AllClients = AllClients;

		// 🟢 Watch for future rows added in the grid
		Clients.CollectionChanged += (s, e) =>
		{
			if (e.NewItems != null)
			{
				foreach (ManagerCustomerDto item in e.NewItems)
				{
					item.AllClients = AllClients;
				}
			}
		};
	}

	// === Collections ===
	[ObservableProperty] private ObservableCollection<SalesManagerEntity> managers = [];
	[ObservableProperty] private ObservableCollection<CustomerEntity> allClients = [];
	[ObservableProperty] private ObservableCollection<BrandEntity> allFirms = [];
	[ObservableProperty] private ObservableCollection<ManagerCustomerDto> clients = [];
	[ObservableProperty] private ObservableCollection<ManagerBrandEntity> firms = [];

	// === Manager info ===
	[ObservableProperty] private string? selectedManagerId;
	[ObservableProperty] private string? managerCode;
	[ObservableProperty] private string? managerName;
	[ObservableProperty] private string? address;
	[ObservableProperty] private string? contacts;
	[ObservableProperty] private double salePercentValue;
    [ObservableProperty] private ManagerCustomerDto? selectedClient;

    [ObservableProperty]
	private bool isManagerSelected;
	// === Load ===
	private async Task LoadManagersAsync()
	{
		Managers = new ObservableCollection<SalesManagerEntity>(await _managerService.GetManagersAsync());
	}

	private async Task LoadCustomersAsync()
	{
		AllClients = new ObservableCollection<CustomerEntity>(await _customerService.GetAllCustomersAsync());
	}

	private async Task LoadBrandsAsync()
	{
		AllFirms = new ObservableCollection<BrandEntity>(await _brandService.GetAllBrandsAsync());
	}

	// === On manager selected ===
	partial void OnSelectedManagerIdChanged(string? value)
	{
		IsManagerSelected = !string.IsNullOrEmpty(value);

		if (!string.IsNullOrEmpty(value))
			_ = LoadSelectedManagerAsync(value);
		else
		{
			// Clear grids if manager is unselected
			Clients.Clear();
			Firms.Clear();
		}
	}


	private async Task LoadSelectedManagerAsync(string id)
	{
		var manager = await _managerService.GetManagerByIdAsync(id);
		if (manager == null) return;

		ManagerCode = manager.Id;
		ManagerName = manager.FullName;
		Address = manager.Address;
		Contacts = manager.Contacts;

		// 🟢 1. Load ALL customers (for name lookup)
		var allCustomers = await _customerService.GetAllCustomersAsync();
		//AllClients = new ObservableCollection<CustomerEntity>(allCustomers);

		// 🟢 2. Load only unassigned customers for ComboBox selection
		var unassigned = await _managerService.GetUnassignedCustomersAsync();
		AllClients = new ObservableCollection<CustomerEntity>(unassigned);
		// 🟢 3. Create the DTOs for existing manager customers
		Clients = new ObservableCollection<ManagerCustomerDto>(
			manager.ManagerCustomers.Select(mc => new ManagerCustomerDto
			{
				ManagerId = mc.ManagerId,
				CustomerId = mc.CustomerId,
				CustomerName = allCustomers.FirstOrDefault(c => c.Id == mc.CustomerId)?.FullName,
				AllClients = new ObservableCollection<CustomerEntity>(unassigned)
			})
		);

		// 🟢 4. Also make sure new rows in the grid get proper AllClients binding
		Clients.CollectionChanged += (s, e) =>
		{
			if (e.NewItems != null)
			{
				foreach (ManagerCustomerDto item in e.NewItems)
					item.AllClients = new ObservableCollection<CustomerEntity>(unassigned);
			}
		};

		// 🟢 5. Load firm list
		Firms = new ObservableCollection<ManagerBrandEntity>(manager.ManagerBrands);
	}


	// === Update when user selects a different customer ===
	public void UpdateCustomerReferences()
	{
		if (AllClients == null || Clients == null)
			return;

		foreach (var mc in Clients)
		{
			mc.CustomerName = AllClients.FirstOrDefault(c => c.Id == mc.CustomerId)?.FullName;
		}
	}

	// === Save ===
	[RelayCommand]
	private async Task SaveCustomers()
	{
		UpdateCustomerReferences();

		if (string.IsNullOrEmpty(SelectedManagerId))
			return;

		// 🟢 Filter out invalid or empty rows before saving
		var validCustomers = Clients
			.Where(c => !string.IsNullOrWhiteSpace(c.CustomerId))
			.Select(c => new ManagerCustomerEntity
			{
				ManagerId = SelectedManagerId, // always set
				CustomerId = c.CustomerId
			})
			.ToList();

		// 🟢 Save only valid ones
		await _managerService.SaveManagerCustomersAsync(SelectedManagerId, validCustomers);

		MessageBox.Show("Клиенты успешно сохранены!", "Сохранено",
			MessageBoxButton.OK, MessageBoxImage.Information);

		// 🟢 Optionally refresh after save
		_ = LoadSelectedManagerAsync(SelectedManagerId);
	}



	// === Firms ===
	[RelayCommand]
	private async Task SaveFirmAsync()
	{
		if (string.IsNullOrEmpty(SelectedManagerId)) return;
		if (Firms == null || Firms.Count == 0) return;

		foreach (var f in Firms)
			f.ManagerId = SelectedManagerId;

		await _managerService.SaveManagerBrandsAsync(SelectedManagerId, Firms);

		MessageBox.Show("Данные по фирмам успешно сохранены!", "Сохранено",
			MessageBoxButton.OK, MessageBoxImage.Information);
	}

	[RelayCommand]
	private async Task AddAllFirmsAsync()
	{
		await DeleteAllFirmsAsync();

        if (string.IsNullOrEmpty(SelectedManagerId)) return;
		if (AllFirms == null || !AllFirms.Any()) return;

		var percent = SalePercentValue > 0 ? SalePercentValue : 0;

		Firms.Clear();

		foreach (var brand in AllFirms)
		{
			Firms.Add(new ManagerBrandEntity
			{
				ManagerId = SelectedManagerId,
				BrandId = brand.Id,
				SalesPercentage = percent
			});
		}

		await _managerService.SaveManagerBrandsAsync(SelectedManagerId, Firms);
	}

	[RelayCommand]
	private async Task DeleteAllFirmsAsync()
	{
		if (string.IsNullOrEmpty(SelectedManagerId)) return;

		await _managerService.DeleteAllBrandsAsync(SelectedManagerId);
		Firms.Clear();
        
    }

    [RelayCommand]
    private async Task DeleteCustomerAsync()
    {
        if (string.IsNullOrEmpty(SelectedManagerId))
        {
            MessageBox.Show("Выберите менеджера перед удалением клиента.",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Clients == null || Clients.Count == 0)
        {
            MessageBox.Show("У данного менеджера нет клиентов для удаления.",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selected = SelectedClient;
        if (selected == null)
        {
            MessageBox.Show("Выберите клиента для удаления.",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Удалить клиента '{selected.CustomerName}' из менеджера?",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        await _managerService.DeleteCustomerAsync(SelectedManagerId, selected.CustomerId);
        Clients.Remove(selected);

        MessageBox.Show("Клиент успешно удалён из менеджера.",
            "Удалено", MessageBoxButton.OK, MessageBoxImage.Information);
    }


    [RelayCommand]
    private async Task DeleteAllCustomersAsync()
    {
        if (string.IsNullOrEmpty(SelectedManagerId))
        {
            MessageBox.Show("Выберите менеджера перед удалением клиентов.",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Clients == null || Clients.Count == 0)
        {
            MessageBox.Show("У данного менеджера нет клиентов для удаления.",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            "Вы действительно хотите удалить всех клиентов у выбранного менеджера?",
            "Подтверждение удаления всех клиентов",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        await _managerService.DeleteAllCustomersAsync(SelectedManagerId);
        Clients.Clear();

        MessageBox.Show("Все клиенты успешно удалены у менеджера.",
            "Удалено", MessageBoxButton.OK, MessageBoxImage.Information);
    }

}


public partial class ManagerCustomerDto : ObservableObject
{
	[ObservableProperty]
	private string managerId = null!;

	[ObservableProperty]
	private string customerId = null!;

	[ObservableProperty]
	private string? customerName;

	[ObservableProperty]
	private ObservableCollection<CustomerEntity>? allClients;

	partial void OnCustomerIdChanged(string value)
	{
		if (AllClients == null || AllClients.Count == 0)
			return;

		var match = AllClients.FirstOrDefault(c => c.Id == value);
		CustomerName = match?.FullName ?? string.Empty;
	}
}
