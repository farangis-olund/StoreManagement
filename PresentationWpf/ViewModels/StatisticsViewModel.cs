using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace PresentationWpf.ViewModels;

public partial class StatisticsViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;

    // === Current statistics view ===
    [ObservableProperty]
    private object? currentStatisticsView;

    // === Selected flags (UI highlight) ===
    [ObservableProperty] private bool isTotalByRegionSelected;
    [ObservableProperty] private bool isTotalByManagerSelected;
    [ObservableProperty] private bool isSalesDinamicsSelected;
    [ObservableProperty] private bool isCustomerSalesReimbSelected;
    public StatisticsViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    // ================= STATISTICS =================

    // === Итоги по регионам ===
    [RelayCommand]
    private void TotalByRegion()
    {
        ResetSelection();
        IsTotalByRegionSelected = true;

        CurrentStatisticsView =
            _serviceProvider.GetService<SaleTotalByGroupReportViewModel>();
    }

    // === Итоги по менеджерам ===
    [RelayCommand]
    private void TotalByManager()
    {
        ResetSelection();
        IsTotalByManagerSelected = true;

        CurrentStatisticsView =
            _serviceProvider.GetService<SalesManagerReportViewModel>();
    }

    // === Динамика продажи ===
    [RelayCommand]
    private void SalesDinamics()
    {
        ResetSelection();
        IsSalesDinamicsSelected = true;

        CurrentStatisticsView =
            _serviceProvider.GetService<SalesDynamicsStatisticsViewModel>();
    }


    // === Динамика киентам ===
    [RelayCommand]
    private void CustomerSalesReimb()
    {
        ResetSelection();
        IsCustomerSalesReimbSelected = true;

        CurrentStatisticsView =
            _serviceProvider.GetService<CustomerSalesPaymentsReportViewModel>();
    }

    // ================= HELPERS =================

    private void ResetSelection()
    {
        IsTotalByRegionSelected = false;
        IsTotalByManagerSelected = false;
        IsSalesDinamicsSelected = false;
        IsCustomerSalesReimbSelected = false;
    }
}