using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Services;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class AdminViewModel : ObservableObject
{
	private readonly IServiceProvider _serviceProvider;
    private readonly CleanService _cleanService;

    [ObservableProperty] private object? currentViewModel;

	// --- Selected flags ---
	[ObservableProperty] private bool isUsersSelected;
	[ObservableProperty] private bool isCoefficientsSelected;
	[ObservableProperty] private bool isBonusesSelected;
	[ObservableProperty] private bool isManagersSelected;
	[ObservableProperty] private bool isStorekeepersSelected;
	[ObservableProperty] private bool isReferencesSelected;
	[ObservableProperty] private bool isDeleteDataSelected;
    [ObservableProperty] private bool isDBSelected;
    public AdminViewModel(IServiceProvider serviceProvider, CleanService cleanService)
	{
		_serviceProvider = serviceProvider;
		_cleanService = cleanService;
	
	}

	// === Пользователи ===
	[RelayCommand]
	private void OpenUsers()
	{
		ResetSelection();
		IsUsersSelected = true;
		CurrentViewModel = _serviceProvider.GetService<UserViewModel>();
	}

	// === Коэффициенты ===
	[RelayCommand]
	private void OpenCoefficients()
	{
		ResetSelection();
		IsCoefficientsSelected = true;
		CurrentViewModel = _serviceProvider.GetService<CoefficientViewModel>();
	}

	// === Бонусы ===
	[RelayCommand]
	private void OpenBonuses()
	{
		ResetSelection();
		IsBonusesSelected = true;
		CurrentViewModel = _serviceProvider.GetService<BonusesViewModel>();
	}

	// === Менеджеры ===
	[RelayCommand]
	private void OpenManagers()
	{
		ResetSelection();
		IsManagersSelected = true;
		CurrentViewModel = _serviceProvider.GetService<ManagerViewModel>();
	}

	// === Складчики ===
	[RelayCommand]
	private void OpenBrands()
	{
		ResetSelection();
		IsStorekeepersSelected = true;
		CurrentViewModel = _serviceProvider.GetService<BrandViewModel>();
	}

	// === Справочники ===
	[RelayCommand]
	private void OpenReferences()
	{
		ResetSelection();
		IsReferencesSelected = true;
		CurrentViewModel = _serviceProvider.GetService<ReferenceViewModel>();
	}

    // === База данных ===
    [RelayCommand]
    private void OpenDB()
    {
        ResetSelection();
        IsDBSelected = true;
        CurrentViewModel = _serviceProvider.GetService<DataBaseViewModel>();
    }

    // === Очистка ===
    [RelayCommand]
    private async Task OpenDeleteAll()
    {
        ResetSelection();
        IsDeleteDataSelected = true;

        // Direct MessageBox confirmation (no dialog service)
        var result = MessageBox.Show(
            "Вы уверены, что хотите удалить ВСЕ данные?",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            await _cleanService.DeleteAllAsync();
            MessageBox.Show("Все данные успешно удалены.", "Готово");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка");
        }


    }


    // --- Helper ---
    private void ResetSelection()
	{
		IsUsersSelected = false;
		IsCoefficientsSelected = false;
		IsBonusesSelected = false;
		IsManagersSelected = false;
		IsStorekeepersSelected = false;
		IsReferencesSelected = false;
        IsDeleteDataSelected = false;
        IsDBSelected = false;
    }
}
