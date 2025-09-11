using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Services;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class CoefficientViewModel : ObservableObject
{
	private readonly CoefficientService _service;

	// === 1) Дневной коэффициент закупа ===
	[ObservableProperty] private decimal dailyPurchaseCoefficient;
	[ObservableProperty] private int dailyPurchaseDays;

	// === 2) Дневной коэффициент погашения (по остаткам) ===
	[ObservableProperty] private decimal dailyRepaymentOstatokNach1;
	[ObservableProperty] private decimal dailyRepaymentOstatokKon1;
	[ObservableProperty] private int dailyRepaymentDays1;

	[ObservableProperty] private decimal dailyRepaymentOstatokNach2;
	[ObservableProperty] private decimal dailyRepaymentOstatokKon2;
	[ObservableProperty] private int dailyRepaymentDays2;

	[ObservableProperty] private decimal dailyRepaymentOstatokNach3;
	[ObservableProperty] private decimal dailyRepaymentOstatokKon3;
	[ObservableProperty] private int dailyRepaymentDays3;

	[ObservableProperty] private decimal dailyRepaymentOstatokNach4;
	[ObservableProperty] private decimal dailyRepaymentOstatokKon4;
	[ObservableProperty] private int dailyRepaymentDays4;

	[ObservableProperty] private decimal dailyRepaymentOstatokNach5;
	[ObservableProperty] private decimal dailyRepaymentOstatokKon5;
	[ObservableProperty] private int dailyRepaymentDays5;

	// === 3) Дневной коэффициент запланированного закупа ===
	[ObservableProperty] private decimal dailyPlannedPurchaseCoefficient;
	[ObservableProperty] private int dailyPlannedPurchaseDays;

	public CoefficientViewModel(CoefficientService service)
	{
		_service = service;
		_ = InitializeAsync();
	}

	public async Task InitializeAsync()
	{
		var e = await _service.GetOrCreateAsync();

		DailyPurchaseCoefficient = e.KoefZakupa;
		DailyPurchaseDays = e.KoefZakupaDni;

		DailyRepaymentOstatokNach1 = e.KoefEzhPogashOstatokNach1;
		DailyRepaymentOstatokKon1 = e.KoefEzhPogashOstatokKon1;
		DailyRepaymentDays1 = e.KoefEzhPogashDin1;

		DailyRepaymentOstatokNach2 = e.KoefEzhPogashOstatokNach2;
		DailyRepaymentOstatokKon2 = e.KoefEzhPogashOstatokKon2;
		DailyRepaymentDays2 = e.KoefEzhPogashDin2;

		DailyRepaymentOstatokNach3 = e.KoefEzhPogashOstatokNach3;
		DailyRepaymentOstatokKon3 = e.KoefEzhPogashOstatokKon3;
		DailyRepaymentDays3 = e.KoefEzhPogashDin3;

		DailyRepaymentOstatokNach4 = e.KoefEzhPogashOstatokNach4;
		DailyRepaymentOstatokKon4 = e.KoefEzhPogashOstatokKon4;
		DailyRepaymentDays4 = e.KoefEzhPogashDin4;

		DailyRepaymentOstatokNach5 = e.KoefEzhPogashOstatokNach5;
		DailyRepaymentOstatokKon5 = e.KoefEzhPogashOstatokKon5;
		DailyRepaymentDays5 = e.KoefEzhPogashDin5;

		DailyPlannedPurchaseCoefficient = e.KoefZaplanZakup;
		DailyPlannedPurchaseDays = e.KoefZaplanZakupDni;
	}

	[RelayCommand]
	private async Task SaveDailyPurchase()
	{
		var e = await _service.GetOrCreateAsync();
		e.KoefZakupa = DailyPurchaseCoefficient;
		e.KoefZakupaDni = DailyPurchaseDays;
		await _service.SaveAsync(e);
		await _service.CalculateZakupForAllAsync();
		await InitializeAsync();
		MessageBox.Show("Дневной коэффициент закупа сохранён!", "Уведомление",
			MessageBoxButton.OK, MessageBoxImage.Information);
	}

	[RelayCommand]
	private async Task SaveDailyRepayment()
	{
		var e = await _service.GetOrCreateAsync();
		e.KoefEzhPogashOstatokNach1 = DailyRepaymentOstatokNach1;
		e.KoefEzhPogashOstatokKon1 = DailyRepaymentOstatokKon1;
		e.KoefEzhPogashDin1 = DailyRepaymentDays1;

		e.KoefEzhPogashOstatokNach2 = DailyRepaymentOstatokNach2;
		e.KoefEzhPogashOstatokKon2 = DailyRepaymentOstatokKon2;
		e.KoefEzhPogashDin2 = DailyRepaymentDays2;

		e.KoefEzhPogashOstatokNach3 = DailyRepaymentOstatokNach3;
		e.KoefEzhPogashOstatokKon3 = DailyRepaymentOstatokKon3;
		e.KoefEzhPogashDin3 = DailyRepaymentDays3;

		e.KoefEzhPogashOstatokNach4 = DailyRepaymentOstatokNach4;
		e.KoefEzhPogashOstatokKon4 = DailyRepaymentOstatokKon4;
		e.KoefEzhPogashDin4 = DailyRepaymentDays4;

		e.KoefEzhPogashOstatokNach5 = DailyRepaymentOstatokNach5;
		e.KoefEzhPogashOstatokKon5 = DailyRepaymentOstatokKon5;
		e.KoefEzhPogashDin5 = DailyRepaymentDays5;

		await _service.SaveAsync(e);
		await _service.CalculateEzhPogashForAllAsync();
		await InitializeAsync();
		MessageBox.Show("Дневной коэффициент погашения сохранён!", "Уведомление",
			MessageBoxButton.OK, MessageBoxImage.Information);
	}

	[RelayCommand]
	private async Task SaveDailyPlannedPurchase()
	{
		var e = await _service.GetOrCreateAsync();
		e.KoefZaplanZakup = DailyPlannedPurchaseCoefficient;
		e.KoefZaplanZakupDni = DailyPlannedPurchaseDays;
		await _service.SaveAsync(e);
		await _service.CalculateZaplanZakupForAllAsync();
		await InitializeAsync();
		MessageBox.Show("Дневной коэффициент запланированного закупа сохранён!", "Уведомление",
			MessageBoxButton.OK, MessageBoxImage.Information);
	}
}
