using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Reporting;
using Infrastructure.Services;
using LiveCharts;
using PresentationWpf.Reporting;
using System.Collections.ObjectModel;
using System.Globalization;
namespace PresentationWpf.ViewModels;
public partial class SalesDynamicsStatisticsViewModel : ObservableObject
{
    private readonly SalesTotalService _service;

    private List<SalesDynamicsDto> _originalData = [];

    public SeriesCollection SeriesCollection { get; } = [];

    public string[] Labels { get; set; } = Array.Empty<string>();

    public Func<double, string> Formatter { get; set; }
        = value => value.ToString("N0");

    // FIRMA FILTER
    public ObservableCollection<string> Firmas { get; } = [];

    [ObservableProperty]
    private string? selectedFirma;

    [ObservableProperty]
    private DateTime? fromDate;

    [ObservableProperty]
    private DateTime? toDate;

    public SalesDynamicsStatisticsViewModel(SalesTotalService service)
    {
        _service = service;
        LoadDataCommand = new AsyncRelayCommand(LoadAsync);
        _ = LoadAsync();
    }

    public IAsyncRelayCommand LoadDataCommand { get; }

    private async Task LoadAsync()
    {
        _originalData = await _service
            .GetSalesDynamicsAsync(FromDate, ToDate);

        BuildFirmaSource();

        ApplyFiltersAndRefresh();
    }

    private void BuildFirmaSource()
    {
        Firmas.Clear();

        Firmas.Add("Всё");

        foreach (var firma in _originalData
            .Select(x => x.Firma)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x))
        {
            Firmas.Add(firma!);
        }

        SelectedFirma ??= "Всё";
    }

    private void ApplyFiltersAndRefresh()
    {
        var filtered = _originalData.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedFirma) && SelectedFirma != "Всё")
            filtered = filtered.Where(x => x.Firma == SelectedFirma);

        BuildChartWithPivotEngine(filtered.ToList());
    }

    private void BuildChartWithPivotEngine(List<SalesDynamicsDto> data)
    {
        var pivot = PivotEngine<SalesDynamicsDto>.Create(
            data,
            x => $"{x.Year}-{x.Month:D2}",
            x => x.Region ?? "",
            x => x.Total
        );

        ChartBuilder.BuildLineChart(
            pivot,
            SeriesCollection,
            out var labels);

        var culture = new CultureInfo("ru-RU");

        Labels = labels
            .Select(l =>
            {
                var parts = l.Split('-');
                int year = int.Parse(parts[0]);
                int month = int.Parse(parts[1]);

                return new DateTime(year, month, 1)
                    .ToString("MMM", culture);
            })
            .ToArray();

        OnPropertyChanged(nameof(Labels));
    }

    partial void OnSelectedFirmaChanged(string? value)
    {
        ApplyFiltersAndRefresh();
    }

    partial void OnFromDateChanged(DateTime? value)
    {
        _ = LoadAsync();
    }

    partial void OnToDateChanged(DateTime? value)
    {
        _ = LoadAsync();
    }
}