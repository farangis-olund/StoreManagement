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

namespace PresentationWpf.ViewModels;

public partial class InactiveWarehouseProductsReportViewModel : ObservableObject
{
    private readonly ReportService _reportService;
    private readonly UserSessionService _userSession;

    public ObservableCollection<InactiveWarehouseProductDto> Items { get; } = new();

    public InactiveWarehouseProductsReportViewModel(ReportService reportService, UserSessionService userSession)
    {
        _reportService = reportService;

        FromDate = null;
        ToDate = null;
        QuantityLessThan = 1;

        _ = LoadAsync();
        _userSession = userSession;
    }


    [ObservableProperty]
    private DateTime? fromDate;


    [ObservableProperty]
    private DateTime? toDate;


    [ObservableProperty]
    private int quantityLessThan = 1;



    partial void OnFromDateChanged(DateTime? value)
    {
        _ = LoadAsync();
    }

    partial void OnToDateChanged(DateTime? value)
    {
        _ = LoadAsync();
    }

    partial void OnQuantityLessThanChanged(int value)
    {
        _ = LoadAsync();
    }



    private async Task LoadAsync()
    {
        var data = await _reportService.GetInactiveWarehouseProductsAsync(
            QuantityLessThan,
            FromDate,
            ToDate);

        Items.Clear();

        foreach (var item in data)
            Items.Add(item);
    }



    [RelayCommand]
    private void Print()
    {
        var orgName = _userSession.OrganizationDisplayName;

        if (Items == null || Items.Count == 0)
        {
            MessageBox.Show(
                "Нет данных для печати.",
                "Печать отчёта",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // 🔹 Build PDF report
        var document = new InactiveWarehouseReportDocument(this, orgName);

        string folder = Path.Combine(Path.GetTempPath(), "Reports");
        Directory.CreateDirectory(folder);

        string filePath = Path.Combine(
            folder,
            $"InactiveWarehousePreview_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        document.GeneratePdf(filePath);

        // 🔹 Show preview window
        var preview = new DocumentPreviewView(filePath);

        var window = new Window
        {
            Title = "Предварительный просмотр — Неактивные товары склада",
            Content = preview,
            Width = 900,
            Height = 1000,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        window.ShowDialog();
    }

    [RelayCommand]
    private void ExportExcel(DataGrid grid)
    {
        ExcelExportHelper.ExportFromDataGrid(grid, "InactiveProducts.xlsx");
    }
}