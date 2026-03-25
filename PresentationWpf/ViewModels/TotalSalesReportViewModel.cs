using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Services;
using PresentationWpf.Documents;
using PresentationWpf.Views;
using QuestPDF.Fluent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;


namespace PresentationWpf.ViewModels
{
    public partial class TotalSalesReportViewModel : ObservableObject
    {
        private readonly ReportService _service;
        private readonly OrganizationInfoService _orgService;

        public TotalSalesReportViewModel(
            ReportService service,
            OrganizationInfoService orgService)
        {
            _service = service;
            _orgService = orgService;

            FromDate = null;
            ToDate = null;

            _ = LoadAsync();
        }

        [ObservableProperty] private DateTime? fromDate;
        [ObservableProperty] private DateTime? toDate;

        public ObservableCollection<TotalSalesReportRowDto> Rows { get; } = new();

        public async Task LoadAsync()
        {
            Rows.Clear();

            var data = await _service.GetFullTotalSalesReportAsync(FromDate, ToDate);

            foreach (var row in data)
                Rows.Add(row);
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadAsync();
        }

        [RelayCommand]
        private async Task ClearPeriod()
        {
            FromDate = null;
            ToDate = null;
            await LoadAsync();
        }

        [RelayCommand]
        private async Task Print()
        {
            try
            {
                await LoadAsync();

                if (Rows.Count == 0)
                {
                    MessageBox.Show(
                        "Нет данных для печати.",
                        "Печать",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var shopName = await _orgService.GetShopDisplayAsync() ?? "";

                var document = new TotalSalesReportDocument(
                    Rows,
                    FromDate,
                    ToDate,
                    shopName);

                string folder = Path.Combine(Path.GetTempPath(), "TotalSalesReports");
                Directory.CreateDirectory(folder);

                string fromPart = FromDate?.ToString("yyyyMMdd") ?? "all";
                string toPart = ToDate?.ToString("yyyyMMdd") ?? "all";

                string file = Path.Combine(
                    folder,
                    $"TotalSalesReport_{fromPart}_{toPart}_{DateTime.Now:HHmmss}.pdf");

                document.GeneratePdf(file);

                var preview = new DocumentPreviewView(file);

                var window = new Window
                {
                    Title = "Печать отчета",
                    Content = preview,
                    Width = 900,
                    Height = 1000,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при формировании отчета: {ex.Message}",
                    "Печать",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Export()
        {
            // later Excel export
        }
    }
}