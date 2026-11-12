using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Constants;
using Infrastructure.Security;
using Infrastructure.Services;
using PresentationWpf.Services;
using PresentationWpf.Views;
using System.ComponentModel;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class ReferenceViewModel : ObservableObject
{
    private readonly NavigationService _nav;
    private readonly CurrencyService _currencyService;
    private readonly UserSessionService _userSessionService;
    private readonly AdminAccessPolicy _access;
    private string? _currentPermission;
    [ObservableProperty] private object? selectedReferenceView;

    public ReferenceViewModel(NavigationService nav, CurrencyService currencyService, UserSessionService userSessionService,
        PermissionService permissions)
    {
        _nav = nav; _currencyService = currencyService; _userSessionService = userSessionService;
        _access = new AdminAccessPolicy(permissions);
        PropertyChangedEventManager.AddHandler(permissions, (_, _) => RefreshPermissions(), nameof(PermissionService.CurrentPermissions));
    }
    public bool CanViewOrganization => _access.CanOpen(PermissionCatalog.AdminReferenceOrganization);
    public bool CanViewRoles => _access.CanOpen(PermissionCatalog.AdminReferenceRoles);
    public bool CanViewCouriers => _access.CanOpen(PermissionCatalog.AdminReferenceCouriers);
    public bool CanViewPickers => _access.CanOpen(PermissionCatalog.AdminReferencePickers);
    public bool CanViewManagers => _access.CanOpen(PermissionCatalog.AdminReferenceManagers);
    public bool CanViewPriceLevels => _access.CanOpen(PermissionCatalog.AdminReferencePriceLevels);
    public bool CanViewCurrency => _access.CanOpen(PermissionCatalog.AdminReferenceCurrency);

    [RelayCommand(CanExecute = nameof(CanViewOrganization))]
    private void OpenOrganization() => Open<OrganizationInfoView>(PermissionCatalog.AdminReferenceOrganization);
    [RelayCommand(CanExecute = nameof(CanViewManagers))]
    private void OpenManager() => Open<ManagerInfoView>(PermissionCatalog.AdminReferenceManagers);
    [RelayCommand(CanExecute = nameof(CanViewCouriers))]
    private void OpenCourier() => Open<CourierInfoView>(PermissionCatalog.AdminReferenceCouriers);
    [RelayCommand(CanExecute = nameof(CanViewPickers))]
    private void OpenAssignPicker() => Open<AssignPickerInfoView>(PermissionCatalog.AdminReferencePickers);
    [RelayCommand(CanExecute = nameof(CanViewRoles))]
    private void OpenRole() => Open<RoleManagementView>(PermissionCatalog.AdminReferenceRoles);
    [RelayCommand(CanExecute = nameof(CanViewPriceLevels))]
    private void OpenLevel() => Open<PriceLevelView>(PermissionCatalog.AdminReferencePriceLevels);

    private void Open<T>(string permission) where T : FrameworkElement
    {
        if (!_access.CanOpen(permission)) return;
        var view = _nav.Open<T>();
        _currentPermission = permission; SelectedReferenceView = view;
    }

    [RelayCommand(CanExecute = nameof(CanViewCurrency))]
    private async Task OpenCurrencyAsync()
    {
        if (!CanViewCurrency) return;
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "Введите текущий курс евро в сомони (например, 11.20):", "Курс валюты", "11.00");
        if (string.IsNullOrWhiteSpace(input) || !CanViewCurrency) return;
        input = input.Trim().Replace(',', '.');
        if (double.TryParse(input, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var rate))
        {
            await _currencyService.AddExchangeRateAsync("EUR", rate);
            await _userSessionService.RefreshExchangeRateAsync("EUR");
            MessageBox.Show($"Курс валюты сохранён: 1 EUR = {rate:N2} TJS", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else MessageBox.Show("Введите корректное числовое значение курса.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    private void RefreshPermissions()
    {
        OnPropertyChanged(nameof(CanViewOrganization)); OnPropertyChanged(nameof(CanViewRoles));
        OnPropertyChanged(nameof(CanViewCouriers)); OnPropertyChanged(nameof(CanViewPickers));
        OnPropertyChanged(nameof(CanViewManagers)); OnPropertyChanged(nameof(CanViewPriceLevels)); OnPropertyChanged(nameof(CanViewCurrency));
        OpenOrganizationCommand.NotifyCanExecuteChanged(); OpenRoleCommand.NotifyCanExecuteChanged();
        OpenCourierCommand.NotifyCanExecuteChanged(); OpenAssignPickerCommand.NotifyCanExecuteChanged();
        OpenManagerCommand.NotifyCanExecuteChanged(); OpenLevelCommand.NotifyCanExecuteChanged(); OpenCurrencyCommand.NotifyCanExecuteChanged();
        if (_currentPermission != null && !_access.CanOpen(_currentPermission))
        {
            SelectedReferenceView = null; _currentPermission = null;
        }
    }
}
