using CommunityToolkit.Mvvm.ComponentModel;
using Infrastructure.Dtos;
using Infrastructure.Services;
using System.Collections.ObjectModel;
using LiveCharts;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Reporting;
using PresentationWpf.Reporting;

namespace PresentationWpf.ViewModels;

public partial class SaleTotalByGroupReportViewModel : ObservableObject
{
    private readonly SalesTotalService _salesService;

    private List<SalesTotalDto> _originalData = [];

    public ObservableCollection<PivotRow> PivotTable { get; } = [];
    public List<string> BrandColumns { get; private set; } = [];
    public event Action? PivotTableUpdated;

   
    public SeriesCollection SeriesCollection { get; } = [];
    public string[] Labels { get; set; } = Array.Empty<string>();

    public Func<double, string> Formatter { get; set; }
        = value => value.ToString("N0");

   
    public ObservableCollection<string> Regions { get; } = new();
    public ObservableCollection<string> Brands { get; } = new();

    [ObservableProperty]
    private string? selectedRegion;

    [ObservableProperty]
    private string? selectedBrand;

    
    [ObservableProperty]
    private DateTime? fromDate;

    [ObservableProperty]
    private DateTime? toDate;
   
    public SaleTotalByGroupReportViewModel(SalesTotalService salesService)
    {
        _salesService = salesService;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        _ = LoadDataAsync();
    }

    public IAsyncRelayCommand LoadDataCommand { get; }

    
    // LOAD DATA
   private async Task LoadDataAsync()
    {
        _originalData = await _salesService
            .GetTotalsByRegionAndBrandAsync(FromDate, ToDate);

        BuildComboSources();

        ApplyFiltersAndRefresh();
    }

    private void BuildComboSources()
    {
        Regions.Clear();
        Brands.Clear();

        Regions.Add("Всё");
        Brands.Add("Всё");

        foreach (var region in _originalData.Select(x => x.Region).Distinct().OrderBy(x => x))
            Regions.Add(region ?? "");

        foreach (var brand in _originalData.Select(x => x.Brand).Distinct().OrderBy(x => x))
            Brands.Add(brand ?? "");

        SelectedRegion = "Всё";
        SelectedBrand = "Всё";
    }

    
    // APPLY FILTERS
    private void ApplyFiltersAndRefresh()
    {
        var filtered = _originalData.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedRegion) && SelectedRegion != "Всё")
            filtered = filtered.Where(x => x.Region == SelectedRegion);

        if (!string.IsNullOrWhiteSpace(SelectedBrand) && SelectedBrand != "Всё")
            filtered = filtered.Where(x => x.Brand == SelectedBrand);

        var result = filtered.ToList();
                
        BuildPivotTable(
            result,
            x => x.Region ?? "",
            x => x.Brand ?? "",
            x => x.Total
        );
      
    }

    partial void OnSelectedRegionChanged(string? value)
    {
        ApplyFiltersAndRefresh();
    }

    partial void OnSelectedBrandChanged(string? value)
    {
        ApplyFiltersAndRefresh();
    }

    partial void OnFromDateChanged(DateTime? value)
    {
        _ = LoadDataAsync();
    }

    partial void OnToDateChanged(DateTime? value)
    {
        _ = LoadDataAsync();
    }
       
    private void BuildPivotTable<T>(
    List<T> data,
    Func<T, string> rowSelector,
    Func<T, string> columnSelector,
    Func<T, decimal> valueSelector)
    {
        PivotTable.Clear();

        var pivot = PivotEngine<T>.Create(
            data,
            rowSelector,
            columnSelector,
            valueSelector
        );

        BrandColumns = pivot.Columns;

        foreach (var row in pivot.Rows)
            PivotTable.Add(row);


        ChartBuilder.BuildColumnChart(
            pivot,
            SeriesCollection,
            out var labels);

        Labels = labels;
        OnPropertyChanged(nameof(Labels));

        PivotTableUpdated?.Invoke();
    }
   
}