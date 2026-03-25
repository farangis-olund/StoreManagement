
namespace PresentationWpf.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Services;
using PresentationWpf.Documents;
using PresentationWpf.Services;
using PresentationWpf.Views;
using QuestPDF.Fluent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;


public partial class WarehousePlaceReportViewModel : ObservableObject
{
    private readonly ReportService _reportService;
    private readonly UserSessionService _userSession;

    private const string ALL = "Все";
    private const int ALL_NUMBER = -1;

    private bool _isInitializing;
    private List<WarehousePlaceInfo> _allPlaces = new();

    public ObservableCollection<WarehousePlaceDto> Items { get; } = new();

    public ObservableCollection<ComboOption<string>> Sections { get; } = new();
    public ObservableCollection<ComboOption<int>> Rows { get; } = new();
    public ObservableCollection<ComboOption<int>> Places { get; } = new();

    [ObservableProperty]
    private string selectedSection = ALL;

    [ObservableProperty]
    private int selectedRow = ALL_NUMBER;

    [ObservableProperty]
    private int selectedPlace = ALL_NUMBER;

    [ObservableProperty]
    private bool includeZeroQuantity = false;

    public WarehousePlaceReportViewModel(
        ReportService reportService,
        UserSessionService userSession)
    {
        _reportService = reportService;
        _userSession = userSession;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _isInitializing = true;

            _allPlaces = await _reportService.GetWarehousePlacesRawAsync();

            LoadSections();
            LoadRows();
            LoadPlaces();

            SelectedSection = ALL;
            SelectedRow = ALL_NUMBER;
            SelectedPlace = ALL_NUMBER;

            _isInitializing = false;

            await LoadAsync();
        }
        catch (Exception ex)
        {
            _isInitializing = false;
            MessageBox.Show(
                $"Ошибка инициализации отчёта:\n{ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void LoadSections()
    {
        Sections.Clear();

        Sections.Add(new ComboOption<string>
        {
            Display = ALL,
            Value = ALL
        });

        foreach (var section in _allPlaces
                     .Select(x => x.Section)
                     .Distinct()
                     .OrderBy(x => x))
        {
            Sections.Add(new ComboOption<string>
            {
                Display = section,
                Value = section
            });
        }
    }

    private void LoadRows()
    {
        Rows.Clear();

        Rows.Add(new ComboOption<int>
        {
            Display = ALL,
            Value = ALL_NUMBER
        });

        IEnumerable<WarehousePlaceInfo> filtered = _allPlaces;

        if (SelectedSection != ALL)
            filtered = filtered.Where(x => x.Section == SelectedSection);

        foreach (var row in filtered
                     .Select(x => x.Row)
                     .Distinct()
                     .OrderBy(x => x))
        {
            Rows.Add(new ComboOption<int>
            {
                Display = row.ToString(),
                Value = row
            });
        }
    }

    private void LoadPlaces()
    {
        Places.Clear();

        Places.Add(new ComboOption<int>
        {
            Display = ALL,
            Value = ALL_NUMBER
        });

        IEnumerable<WarehousePlaceInfo> filtered = _allPlaces;

        if (SelectedSection != ALL)
            filtered = filtered.Where(x => x.Section == SelectedSection);

        if (SelectedRow != ALL_NUMBER)
            filtered = filtered.Where(x => x.Row == SelectedRow);

        foreach (var place in filtered
                     .Select(x => x.Place)
                     .Distinct()
                     .OrderBy(x => x))
        {
            Places.Add(new ComboOption<int>
            {
                Display = place.ToString(),
                Value = place
            });
        }
    }

    partial void OnSelectedSectionChanged(string value)
    {
        if (_isInitializing)
            return;

        LoadRows();
        SelectedRow = ALL_NUMBER;

        LoadPlaces();
        SelectedPlace = ALL_NUMBER;

        _ = LoadAsync();
    }

    partial void OnSelectedRowChanged(int value)
    {
        if (_isInitializing)
            return;

        LoadPlaces();
        SelectedPlace = ALL_NUMBER;

        _ = LoadAsync();
    }

    partial void OnSelectedPlaceChanged(int value)
    {
        if (_isInitializing)
            return;

        _ = LoadAsync();
    }

    partial void OnIncludeZeroQuantityChanged(bool value)
    {
        if (_isInitializing)
            return;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var sectionFilter = SelectedSection == ALL ? null : SelectedSection;
            var rowFilter = SelectedRow == ALL_NUMBER ? 0 : SelectedRow;
            var placeFilter = SelectedPlace == ALL_NUMBER ? 0 : SelectedPlace;

            var data = await _reportService.GetWarehousePlaceReportAsync(
                sectionFilter,
                rowFilter,
                placeFilter,
                IncludeZeroQuantity);

            Items.Clear();

            foreach (var item in data)
                Items.Add(item);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка загрузки данных:\n{ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Print()
    {
        var orgName = _userSession.OrganizationDisplayName;

        if (Items.Count == 0)
        {
            MessageBox.Show(
                "Нет данных для печати.",
                "Печать отчёта",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var document = new WarehousePlaceReportDocument(this, orgName);

        string folder = Path.Combine(Path.GetTempPath(), "Reports");
        Directory.CreateDirectory(folder);

        string filePath = Path.Combine(
            folder,
            $"WarehousePlaceReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        document.GeneratePdf(filePath);

        var preview = new DocumentPreviewView(filePath);

        var window = new Window
        {
            Title = "Предварительный просмотр - Отчёт о месте на складе",
            Content = preview,
            Width = 900,
            Height = 1000,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        window.ShowDialog();
    }

    [RelayCommand]
    private void ExportExcel(DataGrid? grid)
    {
        if (grid == null)
            return;

        ExcelExportHelper.ExportFromDataGrid(grid, "WarehousePlace.xlsx");
    }
}
public static class WarehousePlaceParser
{
    public static (string section, int row, int place) Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ("", 0, 0);

        // Example: A12/7
        var section = input.Substring(0, 1);

        var rest = input.Substring(1); // "12/7"
        var parts = rest.Split('/');

        int.TryParse(parts.ElementAtOrDefault(0), out int row);
        int.TryParse(parts.ElementAtOrDefault(1), out int place);

        return (section, row, place);
    }
}

public class ComboOption<T>
{
    public string Display { get; set; } = "";
    public T? Value { get; set; }
}

