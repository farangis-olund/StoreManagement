
using CommunityToolkit.Mvvm.ComponentModel;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using LiveCharts.Wpf;
using LiveCharts;
using System.Collections.ObjectModel;
using Infrastructure.Services;
using Infrastructure.Reporting;
using PresentationWpf.Reporting;


namespace PresentationWpf.ViewModels;
public partial class SalesManagerReportViewModel : ObservableObject
{
    private readonly SalesTotalService _service;

    public SalesManagerReportViewModel(SalesTotalService service)
    {
        _service = service;
        Formatter = value => value.ToString("N2");

        _ = LoadAsync();
    }

    // ================================
    // RAW DATA
    // ================================
    private List<SalesManagerReportRow> _originalData = new();

    // ================================
    // GRID
    // ================================
    public ObservableCollection<Dictionary<string, object>> PivotRows { get; } = new();
    public List<string> CompanyColumns { get; private set; } = new();
    public event Action? PivotTableUpdated;

    // ================================
    // CHART
    // ================================
    public SeriesCollection SeriesCollection { get; } = new();
    public string[] Labels { get; set; } = Array.Empty<string>();
    public Func<double, string> Formatter { get; }

    // ================================
    // FILTERS
    // ================================
    public ObservableCollection<string> Firmas { get; } = new();
    public ObservableCollection<string> Managers { get; } = new();

    [ObservableProperty] private DateTime? fromDate;
    [ObservableProperty] private DateTime? toDate;
    [ObservableProperty] private string? selectedFirma;
    [ObservableProperty] private string? selectedManager;

    // ================================
    // LOAD
    // ================================
    private async Task LoadAsync()
    {
        _originalData = await _service.GetManagerCommissionReportAsync(
            FromDate,
            ToDate,
            null,
            null);

        BuildComboSources();
        ApplyFiltersAndRefresh();
    }

    // ================================
    // COMBO SOURCES
    // ================================
    private void BuildComboSources()
    {
        Firmas.Clear();
        Managers.Clear();

        Firmas.Add("Всё");
        Managers.Add("Всё");

        foreach (var company in _originalData
            .SelectMany(x => x.CompanyData.Keys)
            .Distinct()
            .OrderBy(x => x))
        {
            Firmas.Add(company);
        }

        foreach (var manager in _originalData
            .Select(x => x.ManagerName)
            .Distinct()
            .OrderBy(x => x))
        {
            Managers.Add(manager);
        }

        SelectedFirma ??= "Всё";
        SelectedManager ??= "Всё";
    }

    // ================================
    // FILTER APPLY
    // ================================
    private void ApplyFiltersAndRefresh()
    {
        var filtered = _originalData.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedFirma) && SelectedFirma != "Всё")
            filtered = filtered.Where(x => x.CompanyData.ContainsKey(SelectedFirma));

        if (!string.IsNullOrWhiteSpace(SelectedManager) && SelectedManager != "Всё")
            filtered = filtered.Where(x => x.ManagerName == SelectedManager);

        var list = filtered.ToList();

        BuildPivot(list);
        BuildChartWithPivotEngine(list);
    }

    private void BuildPivot(List<SalesManagerReportRow> data)
    {
        PivotRows.Clear();

        if (!string.IsNullOrWhiteSpace(SelectedFirma) && SelectedFirma != "Всё")
        {
            CompanyColumns = new List<string> { SelectedFirma };
        }
        else
        {
            CompanyColumns = data
                .SelectMany(x => x.CompanyData.Keys)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        var companySalesTotals = new Dictionary<string, decimal>();
        decimal grandSalesTotal = 0m;
        decimal grandReturnTotal = 0m;
        decimal grandCommissionTotal = 0m;

        foreach (var manager in data)
        {
            var row = new Dictionary<string, object>
            {
                ["Manager"] = manager.ManagerName
            };

            decimal managerSalesTotal = 0m;
            decimal managerReturnTotal = 0m;
            decimal managerCommissionTotal = 0m;

            foreach (var company in CompanyColumns)
            {
                decimal sales = 0m;

                if (manager.CompanyData.TryGetValue(company, out var info))
                {
                    sales = info.SalesAmount;
                    managerSalesTotal += info.SalesAmount;
                    managerReturnTotal += info.ReturnAmount;
                    managerCommissionTotal += info.CommissionAmount;
                }

                row[$"{company}_Sales"] = sales;

                companySalesTotals[company] =
                    companySalesTotals.GetValueOrDefault(company) + sales;
            }

            row["SalesTotal"] = managerSalesTotal;
            row["ReturnTotal"] = managerReturnTotal;
            row["CommissionTotal"] = managerCommissionTotal;

            PivotRows.Add(row);

            grandSalesTotal += managerSalesTotal;
            grandReturnTotal += managerReturnTotal;
            grandCommissionTotal += managerCommissionTotal;
        }

        if (CompanyColumns.Any())
        {
            var totalRow = new Dictionary<string, object>
            {
                ["Manager"] = "ИТОГО",
                ["SalesTotal"] = grandSalesTotal,
                ["ReturnTotal"] = grandReturnTotal,
                ["CommissionTotal"] = grandCommissionTotal
            };

            foreach (var company in CompanyColumns)
            {
                totalRow[$"{company}_Sales"] = companySalesTotals.GetValueOrDefault(company);
            }

            PivotRows.Add(totalRow);
        }

        PivotTableUpdated?.Invoke();
    }
    // ================================
    // CHART USING YOUR CHARTBUILDER
    // ================================
    private void BuildChartWithPivotEngine(List<SalesManagerReportRow> data)
    {
        var flatData = data
            .SelectMany(manager =>
                manager.CompanyData.Select(company =>
                    new
                    {
                        Manager = manager.ManagerName,
                        Company = company.Key,
                        Sales = company.Value.SalesAmount
                    }))
            .ToList();

        var pivot = PivotEngine<dynamic>.Create(
            flatData,
            x => x.Manager,
            x => x.Company,
            x => x.Sales
        );

        ChartBuilder.BuildColumnChart(
            pivot,
            SeriesCollection,
            out var labels);

        Labels = labels;
        OnPropertyChanged(nameof(Labels));
    }

    // ================================
    // PROPERTY CHANGE TRIGGERS
    // ================================
    partial void OnFromDateChanged(DateTime? value) => _ = LoadAsync();
    partial void OnToDateChanged(DateTime? value) => _ = LoadAsync();
    partial void OnSelectedFirmaChanged(string? value) => ApplyFiltersAndRefresh();
    partial void OnSelectedManagerChanged(string? value) => ApplyFiltersAndRefresh();
}