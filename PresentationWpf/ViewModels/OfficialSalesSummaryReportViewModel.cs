using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Services;
using PresentationWpf.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace PresentationWpf.ViewModels;

public partial class OfficialSalesSummaryReportViewModel : ObservableObject
{
    private readonly ReportService _reportService;

    public OfficialSalesSummaryReportViewModel(ReportService reportService)
    {
        _reportService = reportService;
        FromDate = DateTime.Today;
        ToDate = DateTime.Today;

        _ = LoadAsync();
    }

    [ObservableProperty]
    private DateTime? fromDate;

    [ObservableProperty]
    private DateTime? toDate;

    public ObservableCollection<OfficialSalesReportRowDto> Rows { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        Rows.Clear();

        var data = await _reportService.GetOfficialSalesReportAsync(FromDate, ToDate);

        foreach (var row in data)
            Rows.Add(row);
    }

    [RelayCommand]
    private void Print(FrameworkElement? view)
    {
        if (view == null)
            return;

        if (Rows.Count == 0)
        {
            MessageBox.Show(
                "Нет данных для печати.",
                "Печать",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        PrintHelper.Print(view, "Итоги по продажам");
    }

    [RelayCommand]
    private void Export(DataGrid? grid)
    {
        if (grid == null)
            return;

        ExcelExportHelper.ExportFromDataGrid(grid, "Итоги по продажам.xlsx");
    }
}
