using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Constants;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;

namespace PresentationWpf.ViewModels;

public partial class ReportsViewModel
{
    private readonly List<ReportNavigationItem> _reportCatalog = [];
    public ObservableCollection<ReportNavigationItem> ReportLinks { get; } = [];
    public bool IsReportHome => CurrentReportView == null;

    private void InitializeReportLinks()
    {
        void Add(string title, Type type, ICommand command, bool standard = false) => _reportCatalog.Add(new(title, type, standard,
            new RelayCommand(() =>
            {
                if (_permissionService.Has(PermissionCatalog.Reports) && (!standard || CanViewStandardReports)) command.Execute(null);
            })));
        Add("Итоги по клиентам", typeof(SalesSummaryViewModel), OpenCustomerSalesSummaryCommand, true);
        Add("Итоги по группам", typeof(SalesByGroupCustomerReportViewModel), TotalByGroupCommand, true);
        Add("Неактивные товары склада", typeof(InactiveWarehouseProductsReportViewModel), InactiveWarehouseProductsCommand);
        Add("Итоги по доставщикам и складчикам", typeof(CourierStorekeeperReportViewModel), CourierStorekeeperReportCommand, true);
        Add("Отчет по месту на складе", typeof(WarehousePlaceReportViewModel), WarehousePlaceReportCommand);
        Add("Отчет о возвратах за день", typeof(ReturnsDayReportViewModel), ReturnsDayReportCommand, true);
        Add("Итоговый отчёт", typeof(TotalSalesReportViewModel), OpenSalesSummaryCommand, true);
        Add("Итоги по продажам", typeof(OfficialSalesSummaryReportViewModel), OpenOfficialSalesSummaryCommand);
        Add("Об обмене товаров магазинами", typeof(StoreExchangeReportViewModel), StoreExchangeReportCommand);
        PropertyChangedEventManager.AddHandler(_permissionService, ReportPermissionsChanged, nameof(Services.PermissionService.CurrentPermissions));
        RefreshReportLinks();
    }

    private void ReportPermissionsChanged(object? sender, PropertyChangedEventArgs e) => RefreshReportLinks();
    private void RefreshReportLinks()
    {
        var visible = _reportCatalog.Where(r => _permissionService.Has(PermissionCatalog.Reports) &&
            (!r.RequiresStandardReports || CanViewStandardReports))
            .OrderBy(r => r.Title, StringComparer.Create(CultureInfo.GetCultureInfo("ru-RU"), true)).ToArray();
        ReportLinks.Clear();
        foreach (var item in visible) ReportLinks.Add(item);
        OnPropertyChanged(nameof(CanViewStandardReports));
        if (CurrentReportView != null && !visible.Any(r => r.ViewModelType.IsInstanceOfType(CurrentReportView))) ShowReportHome();
    }

    partial void OnCurrentReportViewChanged(object? value)
    {
        foreach (var item in _reportCatalog) item.IsSelected = value != null && item.ViewModelType.IsInstanceOfType(value);
        OnPropertyChanged(nameof(IsReportHome));
    }

    [RelayCommand]
    private void ShowReportHome() { ResetSelection(); CurrentReportView = null; }

    [RelayCommand]
    private void StoreExchangeReport()
    {
        if (!_permissionService.Has(PermissionCatalog.Reports)) return;
        ResetSelection();
        CurrentReportView = _serviceProvider.GetRequiredService<StoreExchangeReportViewModel>();
    }
}

public partial class ReportNavigationItem(string title, Type viewModelType, bool requiresStandardReports, ICommand openCommand) : ObservableObject
{
    public string Title { get; } = title;
    public Type ViewModelType { get; } = viewModelType;
    public bool RequiresStandardReports { get; } = requiresStandardReports;
    public ICommand OpenCommand { get; } = openCommand;
    [ObservableProperty] private bool isSelected;
}
