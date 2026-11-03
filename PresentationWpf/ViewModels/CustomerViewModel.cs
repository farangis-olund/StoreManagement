using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Services;
using Infrastructure.Security;
using PresentationWpf.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;



namespace PresentationWpf.ViewModels;

public partial class CustomerViewModel : ObservableObject
{
	private readonly CustomerService _customerService;
	private readonly CoefficientService _coefficientService;
    private readonly CustomerFieldPermissionPolicy _fieldPermissions;
    private CustomerEntity? _originalCustomer;
    private bool _isLoading;
    public PermissionService PermissionService { get; }
    public CustomerViewModel(CustomerService customerService, CoefficientService coefficientService, PermissionService permissionService, CustomerFieldPermissionPolicy fieldPermissions)
	{
        _customerService = customerService;
		_coefficientService = coefficientService;
        _fieldPermissions = fieldPermissions;
        PermissionService = permissionService;
        PropertyChangedEventManager.AddHandler(PermissionService,
            (_, _) => RefreshPermissions(), nameof(PermissionService.CurrentPermissions));
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

	public async Task LoadDataAsync()
	{
        if (_isLoading)
            return;

        _isLoading = true;
        try
        {
            await LoadPriceLevelsAsync();
            await LoadSalesManagersAsync();
            await LoadCustomersAsync();
        }
        finally
        {
            _isLoading = false;
        }
	}

	private async Task LoadCustomersAsync()
	{
        Customers.Clear();

		var customerList = await _customerService.GetAllCustomersAsync(); 

		foreach (var customer in customerList.Where(c => !IsOnlyOfficialCustomer || c.OfficialCustomer))
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


	[RelayCommand(CanExecute = nameof(CanCreateCustomer))]
	private void CreateCustomer()
	{
        if (!CanCreateCustomer()) return;
		SelectedCustomer = new Customer { ContractDate = null };
        MessageBox.Show("Заполните все обязательные поля, затем нажмите на кнопу Сохранить.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

    }

    [RelayCommand]
    private async Task SaveCustomerAsync()
    {
        if (SelectedCustomer is null)
            return;

        var validationErrors = ValidateCustomer();
        if (validationErrors.Any())
        {
            MessageBox.Show(
                string.Join("\n", validationErrors),
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        CustomerEntity? savedCustomer;
        try
        {
            savedCustomer = await _customerService.AddCustomerAsync(SelectedCustomer);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(ex.Message, "Сохранение клиента", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (savedCustomer is null)
        {
            MessageBox.Show(
                "Ошибка при сохранении клиента.",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            return;
        }

        var updated = await _customerService.GetCustomerByIdAsync(savedCustomer.Id);

        if (updated is not null)
            savedCustomer = updated;

        var existingCustomer = Customers.FirstOrDefault(c => c.Id == savedCustomer.Id);

        if (existingCustomer is null)
        {
            Customers.Add(savedCustomer);
        }
        else
        {
            var index = Customers.IndexOf(existingCustomer);
            Customers[index] = savedCustomer;
        }

        SelectedCustomer = savedCustomer;

        UpdateDistinctLists();

        MessageBox.Show(
            "Клиент успешно сохранён.",
            "Успех",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
    }
    public CustomerFieldAccess DebtAccess => Access(CustomerField.Debt);
    public CustomerFieldAccess RestrictionAccess => Access(CustomerField.Restriction);
    public CustomerFieldAccess BuyLimitAccess => Access(CustomerField.BuyLimit);
    public CustomerFieldAccess UnpaidPaymentLimitAccess => Access(CustomerField.UnpaidPaymentLimit);
    public CustomerFieldAccess CoefficientsAccess => Access(CustomerField.Coefficients);
    public CustomerFieldAccess ContractDateAccess => Access(CustomerField.ContractDate);
    public CustomerFieldAccess PriceLevelAccess => Access(CustomerField.PriceLevel);
    public CustomerFieldAccess SalesManagerAccess => Access(CustomerField.SalesManager);
    public CustomerFieldAccess TerritoryAccess => Access(CustomerField.Territory);
    public CustomerFieldAccess ExcludeDailyRepaymentAccess => Access(CustomerField.ExcludeDailyRepayment);

    public bool CanInputPurchaseCoefficient => SelectedCustomer != null &&
        CanInputValue(CustomerField.Coefficients, _originalCustomer?.DailyPurchaseCoefficient);
    public bool CanInputRepaymentCoefficient => SelectedCustomer != null &&
        CanInputValue(CustomerField.Coefficients, _originalCustomer?.DailyRepaymentCoefficient);
    public bool CanInputPlannedPurchaseCoefficient => SelectedCustomer != null &&
        CanInputValue(CustomerField.Coefficients, _originalCustomer?.DailyPlannedPurchaseCoefficient);

    // Delete-only users may erase text; the save policy still rejects replacement values without Edit.
    private bool CanInputValue(CustomerField field, object? originalValue) =>
        _fieldPermissions.CanEnterValue(field, originalValue) ||
        (!CustomerFieldPermissionPolicy.IsEmpty(field, originalValue) && _fieldPermissions.CanDelete(field));

    private bool CanCreateCustomer() => _fieldPermissions.CanCreateCustomer;

    private CustomerFieldAccess Access(CustomerField field)
    {
        var originalValues = CustomerFieldPermissionPolicy.Values(field, _originalCustomer);
        var canInput = field == CustomerField.ExcludeDailyRepayment && _originalCustomer?.ExcludeDailyRepayment == true
            ? _fieldPermissions.CanDelete(field)
            : originalValues.Any(value => CanInputValue(field, value));
        return new CustomerFieldAccess(_fieldPermissions.CanView(field), SelectedCustomer != null && canInput);
    }
    public bool IsOnlyOfficialCustomer =>
        PermissionService.Has(PermissionCatalog.OnlyOfficialCustomer) ||
        PermissionService.Has("onlyOfficialCustomer");

    public string CustomerDisplayMemberPath => IsOnlyOfficialCustomer ? "FullName" : "DisplayText";

    public void RefreshPermissions()
    {
        OnPropertyChanged(nameof(DebtAccess));
        OnPropertyChanged(nameof(RestrictionAccess));
        OnPropertyChanged(nameof(BuyLimitAccess));
        OnPropertyChanged(nameof(UnpaidPaymentLimitAccess));
        OnPropertyChanged(nameof(CoefficientsAccess));
        OnPropertyChanged(nameof(ContractDateAccess));
        OnPropertyChanged(nameof(PriceLevelAccess));
        OnPropertyChanged(nameof(SalesManagerAccess));
        OnPropertyChanged(nameof(TerritoryAccess));
        OnPropertyChanged(nameof(ExcludeDailyRepaymentAccess));
        OnPropertyChanged(nameof(CanInputPurchaseCoefficient));
        OnPropertyChanged(nameof(CanInputRepaymentCoefficient));
        OnPropertyChanged(nameof(CanInputPlannedPurchaseCoefficient));
        OnPropertyChanged(nameof(CustomerDisplayMemberPath));
        CreateCustomerCommand.NotifyCanExecuteChanged();
        CalculateCoefficientsForCustomerCommand.NotifyCanExecuteChanged();
    }


    private List<string> ValidateCustomer()
    {
        var errors = new List<string>();
        if (SelectedCustomer == null)
        {
            errors.Add("Выберите клиента.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(SelectedCustomer.FullName))
            errors.Add("Поле 'ФИО' обязательно.");

        if (string.IsNullOrWhiteSpace(SelectedCustomer.MobilePhone))
            errors.Add("Поле 'Мобильный телефон' обязательно.");

        if (string.IsNullOrWhiteSpace(SelectedCustomer.Address))
            errors.Add("Поле 'Адрес' обязательно.");

        if (string.IsNullOrWhiteSpace(SelectedCustomer.City))
            errors.Add("Поле 'Город' обязательно.");

        if (string.IsNullOrWhiteSpace(SelectedCustomer.Region))
            errors.Add("Поле 'Область' обязательно.");

        if (string.IsNullOrWhiteSpace(SelectedCustomer.PriceLevelId))
            errors.Add("Поле 'Уровень' обязательно.");
        else if (!PriceLevels.Any(level => level.Level == SelectedCustomer.PriceLevelId))
            errors.Add("Выберите уровень из списка.");

        if (string.IsNullOrWhiteSpace(SelectedCustomer.Territory))
            errors.Add("Поле 'Территория' обязательно.");

        if (string.IsNullOrWhiteSpace(SelectedCustomer.SalesManagerId))
            errors.Add("Поле 'Менеджер по продажам' обязательно.");
        else if (!SalesManagers.Any(manager => manager.Id == SelectedCustomer.SalesManagerId))
            errors.Add("Выберите менеджера по продажам из списка.");

        return errors;
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
        if (!CanCalculateCoefficients() || SelectedCustomer is null) return;

        try
        {
            var customer = SelectedCustomer;
            var updated = await _coefficientService.CalculateForCustomerAsync(customer.Id);
            customer.DailyPurchaseCoefficient = updated.DailyPurchaseCoefficient;
            customer.DailyRepaymentCoefficient = updated.DailyRepaymentCoefficient;
            customer.DailyPlannedPurchaseCoefficient = updated.DailyPlannedPurchaseCoefficient;
            if (SelectedCustomer == customer && _originalCustomer != null)
            {
                _originalCustomer.DailyPurchaseCoefficient = updated.DailyPurchaseCoefficient;
                _originalCustomer.DailyRepaymentCoefficient = updated.DailyRepaymentCoefficient;
                _originalCustomer.DailyPlannedPurchaseCoefficient = updated.DailyPlannedPurchaseCoefficient;
                RefreshPermissions();
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(ex.Message, "Расчет коэффициентов", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        catch (Exception)
        {
            MessageBox.Show("Не удалось рассчитать коэффициенты. Проверьте подключение к базе данных.",
                "Расчет коэффициентов", MessageBoxButton.OK, MessageBoxImage.Error);
        }
	}

	// This is called automatically to check if the button should be enabled
    private bool CanCalculateCoefficients() => !string.IsNullOrWhiteSpace(SelectedCustomer?.Id) &&
        CustomerFieldPermissionPolicy.Values(CustomerField.Coefficients, _originalCustomer)
            .All(value => _fieldPermissions.CanEnterValue(CustomerField.Coefficients, value));

	// Refresh CanExecute when SelectedCustomer changes
	partial void OnSelectedCustomerChanged(Customer? value)
	{
        _originalCustomer = value != null && !string.IsNullOrWhiteSpace(value.Id) ? (CustomerEntity)value : null;
        RefreshPermissions();
	}

}

public sealed record CustomerFieldAccess(bool CanView, bool CanInput);
