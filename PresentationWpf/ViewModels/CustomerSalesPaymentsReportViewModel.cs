using CommunityToolkit.Mvvm.ComponentModel;
using Infrastructure.Dtos;
using Infrastructure.Services;
using LiveCharts;
using LiveCharts.Wpf;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace PresentationWpf.ViewModels;
public partial class CustomerSalesPaymentsReportViewModel : ObservableObject
{
    private readonly SalesTotalService _service;

    public CustomerSalesPaymentsReportViewModel(SalesTotalService service)
    {
        _service = service;
        Formatter = v => v.ToString("N0");

        _ = LoadAsync();
    }

    // =========================
    // DATA
    // =========================
    private List<SalesPaymentDto> _original = new();

    public ObservableCollection<SalesPaymentDto> Rows { get; } = new();

    // =========================
    // CHART
    // =========================
    public SeriesCollection SeriesCollection { get; } = new();
    public string[] Labels { get; set; } = Array.Empty<string>();
    public Func<double, string> Formatter { get; }

    // =========================
    // FILTERS
    // =========================
    public ObservableCollection<string> Regions { get; } = new();
    public ObservableCollection<string> Firmas { get; } = new();

    [ObservableProperty] private DateTime? fromDate;
    [ObservableProperty] private DateTime? toDate;
    [ObservableProperty] private string? selectedRegion;
    [ObservableProperty] private string? selectedFirma;

    // 🔥 NEW
    [ObservableProperty] private bool useDateGrouping;

    // =========================
    // LOAD
    // =========================
    private async Task LoadAsync()
    {
        _original = await _service.GetSalesVsPaymentsReportAsync(FromDate, ToDate);

        BuildFilterSources();
        ApplyFilters();
    }

    private void BuildFilterSources()
    {
        Regions.Clear();
        Firmas.Clear();

        // Default option
        Regions.Add("Всё");
        Firmas.Add("Всё");

        // =========================
        // REGIONS (clean)
        // =========================
        foreach (var r in _original
            .Where(x => !string.IsNullOrWhiteSpace(x.Region))
            .Select(x => x.Region!.Trim())
            .Distinct()
            .OrderBy(x => x))
        {
            Regions.Add(r);
        }

        // =========================
        // FIRMAS (FIXED 🔥)
        // =========================
        foreach (var f in _original
            .Where(x => !string.IsNullOrWhiteSpace(x.Firma)) // 🚀 remove empty/null
            .Select(x => x.Firma!.Trim())
            .Distinct()
            .OrderBy(x => x))
        {
            Firmas.Add(f);
        }

        // =========================
        // DEFAULT SELECTION
        // =========================
        SelectedRegion ??= "Всё";
        SelectedFirma ??= "Всё";
    }
    // =========================
    // FILTER APPLY
    // =========================
    private void ApplyFilters()
    {
        var filtered = _original.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedRegion) && SelectedRegion != "Всё")
            filtered = filtered.Where(x => x.Region == SelectedRegion);

        if (!string.IsNullOrWhiteSpace(SelectedFirma) && SelectedFirma != "Всё")
            filtered = filtered.Where(x => x.Firma == SelectedFirma);

        var list = filtered.ToList();

        BuildGrid(list);

        if (UseDateGrouping)
            BuildChartByMonths(list);
        else
            BuildChartByCustomers(list);
    }

    private void BuildGrid(List<SalesPaymentDto> data)
    {
        Rows.Clear();

        var grouped = data
            .GroupBy(x => x.CustomerCode)
            .OrderBy(g => g.Key);

        foreach (var g in grouped)
        {
            Rows.Add(new SalesPaymentDto
            {
                CustomerCode = g.Key,
                Sales = g.Sum(x => x.Sales),
                Payments = g.Sum(x => x.Payments)
            });
        }
    }
    public AxesCollection AxisX { get; set; } = new();
    // =========================
    // CHART: CUSTOMER
    // =========================
    private void BuildChartByCustomers(List<SalesPaymentDto> data)
    {
        SeriesCollection.Clear();

        var grouped = data
            .GroupBy(x => x.CustomerCode)
            .OrderBy(g => g.Key)
            .ToList();

        var sales = new ChartValues<double>();
        var payments = new ChartValues<double>();
        var labels = new List<string>();

        int index = 0;
        var sections = new SectionsCollection();

        foreach (var g in grouped)
        {
            labels.Add(g.Key);

            sales.Add((double)g.Where(x => x.Sales > 0).Sum(x => x.Sales));
            payments.Add((double)g.Where(x => x.Payments > 0).Sum(x => x.Payments));

            // 🔥 OPTIONAL: light alternating background (like rows)
            sections.Add(new AxisSection
            {
                FromValue = index - 0.5,
                ToValue = index + 0.5,
                Fill = index % 2 == 0
                    ? new SolidColorBrush(Color.FromArgb(15, 0, 0, 0))
                    : Brushes.Transparent
            });

            index++;
        }

        // =========================
        // SERIES
        // =========================
        SeriesCollection.Add(new ColumnSeries
        {
            Title = "Продажа",
            Values = sales,
            Fill = Brushes.SteelBlue
        });

        SeriesCollection.Add(new ColumnSeries
        {
            Title = "Платежи",
            Values = payments,
            Fill = Brushes.IndianRed
        });

        // =========================
        // AXIS (IMPORTANT)
        // =========================
        Labels = labels.ToArray();
        OnPropertyChanged(nameof(Labels));

        AxisX = new AxesCollection
    {
        new Axis
        {
            Title = "Клиенты",
            Labels = Labels,
            //Sections = sections, // 🔥 optional shading
            LabelsRotation = 45
        }
    };

        OnPropertyChanged(nameof(AxisX));
    }


    // CHART: MONTHS
    private void BuildChartByMonths(List<SalesPaymentDto> data)
    {
        SeriesCollection.Clear();

        var grouped = data
            .Where(x => x.Sales > 0)
            .GroupBy(x => new
            {
                x.OrderDate.Year,
                x.OrderDate.Month
            })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .ToList();

        var sales = new ChartValues<double>();
        var labels = new List<string>();

        // 🔥 needed for shading
        var sections = new SectionsCollection();

        int globalIndex = 0;
        bool alternate = false;

        foreach (var monthGroup in grouped)
        {
            var monthName = new DateTime(monthGroup.Key.Year, monthGroup.Key.Month, 1)
                .ToString("MMM");

            var customers = monthGroup
                .GroupBy(x => x.CustomerCode)
                .OrderBy(g => g.Key)
                .ToList();

            int startIndex = globalIndex;

            bool first = true;

            foreach (var cust in customers)
            {
                var label = first
                    ? $"{monthName}\n{cust.Key}"   // show month once
                    : $"\n{cust.Key}";

                first = false;

                labels.Add(label);
                sales.Add((double)cust.Sum(x => x.Sales));

                globalIndex++;
            }

            int endIndex = globalIndex - 1;

            // 🔥 ADD BACKGROUND BLOCK
            sections.Add(new AxisSection
            {
                FromValue = startIndex - 0.5,
                ToValue = endIndex + 0.5,
                Fill = alternate
                    ? new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)) // light gray
                    : Brushes.Transparent
            });

            alternate = !alternate;
        }

        // =========================
        // SERIES
        // =========================
        SeriesCollection.Add(new ColumnSeries
        {
            Title = "Продажа",
            Values = sales,
            Fill = Brushes.SteelBlue
        });

        // =========================
        // AXIS CONFIG (IMPORTANT)
        // =========================
        Labels = labels.ToArray();
        OnPropertyChanged(nameof(Labels));

        AxisX = new AxesCollection
    {
        new Axis
        {
            Labels = Labels,
            Sections = sections, // 🔥 APPLY SHADING HERE
            LabelsRotation = 0
        }
    };

        OnPropertyChanged(nameof(AxisX));
    }

    // =========================
    // TRIGGERS
    // =========================
    partial void OnFromDateChanged(DateTime? value) => _ = LoadAsync();
    partial void OnToDateChanged(DateTime? value) => _ = LoadAsync();
    partial void OnSelectedRegionChanged(string? value) => ApplyFilters();
    partial void OnSelectedFirmaChanged(string? value) => ApplyFilters();
    partial void OnUseDateGroupingChanged(bool value) => ApplyFilters();
}