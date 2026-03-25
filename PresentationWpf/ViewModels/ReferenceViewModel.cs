using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Services;
using PresentationWpf.Services;
using PresentationWpf.Views;
using System.Windows;

namespace PresentationWpf.ViewModels;


public partial class ReferenceViewModel : ObservableObject
{
    private readonly NavigationService _nav;
    private readonly CurrencyService _currencyService;
    private readonly UserSessionService _userSessionService;

    public ReferenceViewModel(NavigationService nav, CurrencyService currencyService, UserSessionService userSessionService)
    {
        _nav = nav;
        _currencyService = currencyService;
        _userSessionService = userSessionService;
    }

    [ObservableProperty]
    private object? selectedReferenceView;

    // Organization
    [RelayCommand]
    private void OpenOrganization()
        => SelectedReferenceView = _nav.Open<OrganizationInfoView>();

    // Manager
    [RelayCommand]
    private void OpenManager()
        => SelectedReferenceView = _nav.Open<ManagerInfoView>();

    // Courier
    [RelayCommand]
    private void OpenCourier()
        => SelectedReferenceView = _nav.Open<CourierInfoView>();

    // Assign Picker
    [RelayCommand]
    private void OpenAssignPicker()
        => SelectedReferenceView = _nav.Open<AssignPickerInfoView>();

    // Roles
    [RelayCommand]
    private void OpenRole()
        => SelectedReferenceView = _nav.Open<RoleManagementView>();


    // Levels
    [RelayCommand]
    private void OpenLevel()
        => SelectedReferenceView = _nav.Open<PriceLevelView>();


    [RelayCommand]
    private async Task OpenCurrencyAsync()
    {
        string? input = Microsoft.VisualBasic.Interaction.InputBox(
            "Введите текущий курс евро в сомони (например, 11.20):",
            "Курс валюты", "11.00");

        if (string.IsNullOrWhiteSpace(input))
            return;

        // Normalize decimal separator (both "." and "," are accepted)
        input = input.Trim().Replace(',', '.');

        if (double.TryParse(
                input,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double rate))
        {
            await _currencyService.AddExchangeRateAsync("EUR", rate);
            await _userSessionService.RefreshExchangeRateAsync("EUR");
            MessageBox.Show($"Курс валюты сохранён: 1 EUR = {rate:N2} TJS",
                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Введите корректное числовое значение курса.",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

}
