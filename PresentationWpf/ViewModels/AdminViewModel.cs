using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;


namespace PresentationWpf.ViewModels;

public partial class AdminViewModel : ObservableObject
{
	private readonly IServiceProvider _serviceProvider;

	[ObservableProperty] private object? currentViewModel;

	// --- Selected flags ---
	[ObservableProperty] private bool isUsersSelected;
	[ObservableProperty] private bool isCoefficientsSelected;
	[ObservableProperty] private bool isBonusesSelected;
	[ObservableProperty] private bool isManagersSelected;
	[ObservableProperty] private bool isStorekeepersSelected;
	[ObservableProperty] private bool isReferencesSelected;

	public AdminViewModel(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
		// Открываем по умолчанию "Пользователи"
		OpenUsers();
	}

	// === Пользователи ===
	[RelayCommand]
	private void OpenUsers()
	{
		//ResetSelection();
		//IsUsersSelected = true;
		//CurrentViewModel = _serviceProvider.GetService<UserViewModel>();
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
		//ResetSelection();
		//IsBonusesSelected = true;
		//CurrentViewModel = _serviceProvider.GetService<BonusesViewModel>();
	}

	// === Менеджеры ===
	[RelayCommand]
	private void OpenManagers()
	{
		//ResetSelection();
		//IsManagersSelected = true;
		//CurrentViewModel = _serviceProvider.GetService<ManagerViewModel>();
	}

	// === Складчики ===
	[RelayCommand]
	private void OpenStorekeepers()
	{
		////ResetSelection();
		////IsStorekeepersSelected = true;
		//CurrentViewModel = _serviceProvider.GetService<StorekeeperViewModel>();
	}

	// === Справочники ===
	[RelayCommand]
	private void OpenReferences()
	{
		//ResetSelection();
		//IsReferencesSelected = true;
		//CurrentViewModel = _serviceProvider.GetService<ReferencesViewModel>();
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
	}
}
