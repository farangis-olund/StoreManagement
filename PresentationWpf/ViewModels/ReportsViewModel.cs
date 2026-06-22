using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Spreadsheet;
using Infrastructure.Dtos;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using PresentationWpf.Documents;
using PresentationWpf.Services;
using PresentationWpf.Views;
using QuestPDF.Fluent;
using System.IO;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ReturnsDayReportService _returnsDayReportService;
    private readonly PermissionService _permissionService;
    // === Current report ===
    [ObservableProperty]
    private object? currentReportView;

    // === Selected flags (for UI highlight) ===
    [ObservableProperty] private bool isSalesSummarySelected;
    [ObservableProperty] private bool isCustomerSalesSummarySelected;
    [ObservableProperty] private bool isTotalByGroupSelected;
    [ObservableProperty] private bool isTotalByRegionSelected;
    [ObservableProperty] private bool isDebtSelected;
    [ObservableProperty] private bool isCourierStorekeeperReportSelected;
    [ObservableProperty] private bool isInactiveWarehouseProductsSelected;
    [ObservableProperty] private bool isWarehousePlaceReportSelected;
    [ObservableProperty] private bool isReturnsDayReportSelected;
    [ObservableProperty] private bool isOfficialSalesSummarySelected;

    public bool CanViewStandardReports => !_permissionService.Has("OnlyOfficialCustomer");

    public ReportsViewModel(
        IServiceProvider serviceProvider,
        ReturnsDayReportService returnsDayReportService,
        PermissionService permissionService)
    {
        _serviceProvider = serviceProvider;
        _returnsDayReportService = returnsDayReportService;
        _permissionService = permissionService;
    }

    // ================= REPORTS =================

    // === Итоги по продажам ===
    [RelayCommand]
    private void OpenSalesSummary()
    {
        ResetSelection();
        IsSalesSummarySelected = true;
        CurrentReportView = _serviceProvider.GetService<TotalSalesReportViewModel>();
    }

    [RelayCommand]
    private void OpenOfficialSalesSummary()
    {
        ResetSelection();
        IsOfficialSalesSummarySelected = true;
        CurrentReportView = _serviceProvider.GetService<OfficialSalesSummaryReportViewModel>();
    }

    // === Итоги по  клиентам ===
    [RelayCommand]
    private void OpenCustomerSalesSummary()
    {
        ResetSelection();
        IsCustomerSalesSummarySelected = true;
        CurrentReportView = _serviceProvider.GetService<SalesSummaryViewModel>();
    }

    // === Итоги по группам ===
    [RelayCommand]
    private void TotalByGroup()
    {
        ResetSelection();
        IsTotalByGroupSelected = true;
        CurrentReportView = _serviceProvider.GetService<SalesByGroupCustomerReportViewModel>();
    }

    // === Итоги по регионам ===
    [RelayCommand]
    private void TotalByRegion()
    {
        ResetSelection();
        IsDebtSelected = true;
        //CurrentReportView = _serviceProvider.GetService<SaleTotalByGroupReportViewModel>();
    }

    // === Оплаты ===
    [RelayCommand]
    private void CourierStorekeeperReport()
    {
        ResetSelection();
        IsCourierStorekeeperReportSelected = true;
        CurrentReportView = _serviceProvider.GetService<CourierStorekeeperReportViewModel>();
    }
        
    [RelayCommand]
    private void InactiveWarehouseProducts()
    {
        ResetSelection();
        IsInactiveWarehouseProductsSelected = true;
        CurrentReportView = _serviceProvider.GetService<InactiveWarehouseProductsReportViewModel>();
    }

    [RelayCommand]
    private void WarehousePlaceReport()
    {
        ResetSelection();
        IsWarehousePlaceReportSelected = true;
        CurrentReportView = _serviceProvider.GetService<WarehousePlaceReportViewModel>();
    }

    [RelayCommand]
    private void ReturnsDayReport()
    {
        ResetSelection();
        IsReturnsDayReportSelected = true;
        CurrentReportView = _serviceProvider.GetService<ReturnsDayReportViewModel>();
     
    }
    
    // ================= HELPERS =================

    private void ResetSelection()
    {
        IsSalesSummarySelected = false;
        IsCustomerSalesSummarySelected = false;
        IsTotalByGroupSelected = false;
        IsDebtSelected = false;
        IsCourierStorekeeperReportSelected = false;
        IsInactiveWarehouseProductsSelected = false;
        IsWarehousePlaceReportSelected = false;
        IsReturnsDayReportSelected = false;
        IsOfficialSalesSummarySelected = false;
    }
}
