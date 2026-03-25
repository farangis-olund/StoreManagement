using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Reporting;
using Infrastructure.Services;
using System.Collections.ObjectModel;

namespace PresentationWpf.ViewModels;

public partial class SalesByGroupCustomerReportViewModel : ObservableObject
{
    private readonly SalesTotalService _service;

    // Original data (filtered only by date)
    private List<SalesByGroupCustomerDto> _originalData = new();

    // Pivot table data
    public ObservableCollection<PivotRow> PivotTable { get; } = new();

    public List<string> CustomerColumns { get; private set; } = new();

    public event Action? PivotTableUpdated;

    // Filter sources
    public ObservableCollection<string> Firmas { get; } = new();
    public ObservableCollection<string> Regions { get; } = new();

    // Selected filters
    [ObservableProperty]
    private DateTime? fromDate;

    [ObservableProperty]
    private DateTime? toDate;

    [ObservableProperty]
    private string? selectedFirma;

    [ObservableProperty]
    private string? selectedRegion;

    [ObservableProperty]
    private bool totalsByQuantity;

    partial void OnTotalsByQuantityChanged(bool value)
    {
        ApplyFiltersAndRefresh();
    }

    // Load command
    public IAsyncRelayCommand LoadDataCommand { get; }

    // Constructor
    public SalesByGroupCustomerReportViewModel(SalesTotalService service)
    {
        _service = service;
        LoadDataCommand = new AsyncRelayCommand(LoadAsync);
        _ = LoadAsync();
    }

    // Load data from database (date filter only)
    private async Task LoadAsync()
    {
        _originalData = await _service.GetSalesByGroupCustomerReportAsync(
            FromDate,
            ToDate,
            null,
            null
        );

        BuildComboSources();
        ApplyFiltersAndRefresh();
    }

    // Build Firma and Region combo sources
    private void BuildComboSources()
    {
        Firmas.Clear();
        Regions.Clear();

        Firmas.Add("Всё");
        Regions.Add("Всё");

        foreach (var firma in _originalData
            .Select(x => x.Firma)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x))
        {
            Firmas.Add(firma!);
        }

        foreach (var region in _originalData
            .Select(x => x.Region)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x))
        {
            Regions.Add(region!);
        }

        SelectedFirma ??= "Всё";
        SelectedRegion ??= "Всё";
    }

    // Apply client-side filters (Firma and Region)
    private void ApplyFiltersAndRefresh()
    {
        var filtered = _originalData.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedFirma) && SelectedFirma != "Всё")
            filtered = filtered.Where(x => x.Firma == SelectedFirma);

        if (!string.IsNullOrWhiteSpace(SelectedRegion) && SelectedRegion != "Всё")
            filtered = filtered.Where(x => x.Region == SelectedRegion);

        BuildPivot(filtered.ToList());
    }

    // Build pivot table
    private void BuildPivot(List<SalesByGroupCustomerDto> data)
    {
        PivotTable.Clear();

        var pivot = PivotEngine<SalesByGroupCustomerDto>.Create(
            data,
            x => x.CustomerCode ?? "",
            x => x.ProductGroup ?? "",
            x => TotalsByQuantity ? x.Quantity : x.Total
        );

        CustomerColumns = pivot.Columns;

        foreach (var row in pivot.Rows)
            PivotTable.Add(row);

        PivotTableUpdated?.Invoke();
    }

    // Reload data when date changes
    partial void OnFromDateChanged(DateTime? value)
    {
        _ = LoadAsync();
    }

    partial void OnToDateChanged(DateTime? value)
    {
        _ = LoadAsync();
    }

    // Reapply filters when combo changes
    partial void OnSelectedFirmaChanged(string? value)
    {
        ApplyFiltersAndRefresh();
    }

    partial void OnSelectedRegionChanged(string? value)
    {
        ApplyFiltersAndRefresh();
    }
}