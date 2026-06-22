using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Helpers;
using Infrastructure.Repositories;  
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using PresentationWpf.Services;
using PresentationWpf.Views;
using QuestPDF.Fluent;

namespace PresentationWpf.ViewModels
{
    public class ImportRow
    {
        public string ArticleNumber { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string GroupName { get; set; } = "";
        public string BrandName { get; set; } = "";
        public string Model { get; set; } = "";
        public int Quantity { get; set; }
    }

    public partial class ImportViewModel : ObservableObject
    {
        private readonly OrderService _orderService;
        private readonly StockUpdateLogRepository _logRepo;
        private readonly ProductService _productService;
        private readonly ExportHelper _export;
        private readonly IServiceProvider _sp;
        private readonly OrganizationInfoService _orgService;

        private readonly ReturnsDayReportService _returnsDayReportService;

        public event Action? RequestClose;

        [ObservableProperty] private ObservableCollection<DateTime> availableDates = [];
        [ObservableProperty] private DateTime? selectedDate;
        [ObservableProperty] private ObservableCollection<ImportRow> rows = [];
        [ObservableProperty] private bool isBusy;

        public ImportViewModel(
            OrderService orderService,
            StockUpdateLogRepository logRepo,
            ProductService productService,
            ExportHelper export,
            IServiceProvider sp, OrganizationInfoService orgService, ReturnsDayReportService returnsDayReportService
            )
        {
            _orderService = orderService;
            _logRepo = logRepo;
            _productService = productService;
            _export = export;
            _sp = sp;
            _orgService = orgService;
            _returnsDayReportService = returnsDayReportService;
         

            _ = LoadDatesAsync();
        }

        // ==== Load dates (sold - already updated) ====
        [RelayCommand]
        public async Task LoadDatesAsync()
        {
            try
            {
                IsBusy = true;

                var soldDates = await _orderService.GetSoldDatesAsync();
                var updatedDates = await _logRepo.GetUpdatedDatesAsync();

                var list = soldDates.Select(d => d.Date)
                                    .Except(updatedDates.Select(d => d.Date))
                                    .Distinct()
                                     .OrderByDescending(d => d)
                                    .ToList();

                AvailableDates = new ObservableCollection<DateTime>(list);
                Rows.Clear();
                SelectedDate = null!;
                RaiseCanExecutes();
            }
            finally { IsBusy = false; }
        }

        partial void OnSelectedDateChanged(DateTime? value) => _ = LoadRowsForDateAsync();

        // ==== Load rows for date ====
        [RelayCommand]
        public async Task LoadRowsForDateAsync()
        {
            Rows.Clear();
            RaiseCanExecutes();
            if (SelectedDate is null) return;

            try
            {
                IsBusy = true;

                var sold = await _orderService.GetSoldByDateAsync(SelectedDate.Value.Date);
                Rows = new ObservableCollection<ImportRow>(
                    sold.Select(s => new ImportRow
                    {
                        ArticleNumber = s.ArticleNumber,
                        ProductName = s.ProductName,
                        BrandName = s.BrandName,
                        GroupName = s.GroupName,
                        Model = s.Model,
                        Quantity = s.Quantity
                    }));

            }
            finally
            {
                IsBusy = false;
                RaiseCanExecutes();
            }
        }
        
        [RelayCommand(CanExecute = nameof(CanRunActions))]
        public async Task UpdateAsync()
        {
            if (SelectedDate is null || Rows.Count == 0) return;
            var date = SelectedDate.Value.Date;

            try
            {
                IsBusy = true;

                if (await _logRepo.ExistsAsync(date))
                {
                    MessageBox.Show("На эту дату уже выполнено обновление.", "Инфо");
                    return;
                }

                // Batch update quantities in one SaveChanges
                var items = Rows
                    .Where(r => !string.IsNullOrWhiteSpace(r.ArticleNumber) && r.Quantity != 0)
                    .Select(r => (Article: r.ArticleNumber, Qty: r.Quantity));

                var affected = await _productService.AddQuantitiesByArticlesAsync(items);

                if (affected <= 0)
                {
                    MessageBox.Show("Обновление не внесло изменений.");
                    return;
                }

                await _logRepo.AddIfNotExistsAsync(date, "Импорт: обновление складских остатков");
                MessageBox.Show("Обновление успешно завершено!", "Готово");

                await LoadDatesAsync(); // дата исчезнет из списка
            }
            finally
            {
                IsBusy = false;
                RaiseCanExecutes();
            }
        }


        [RelayCommand(CanExecute = nameof(CanRunActions))]
        public async Task ExportAsync()
        {
            if (SelectedDate is null || Rows.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта!", "Экспорт",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Convert rows to minimal table (ONLY 2 columns)
            var dt = ToMinimalDataTable(Rows);

            IsBusy = true;
            try
            {
                var ok = await _export.ExportExcel(dt, SelectedDate.Value);

                if (!ok)
                {
                    MessageBox.Show("Не удалось выполнить экспорт.\n"
                                    + "Проверьте путь для экспорта в настройках.",
                                    "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show("Экспорт успешно завершён!",
                                "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                IsBusy = false;
                RaiseCanExecutes();
            }
        }



        public event Action<DataTable, DateTime?, string>? RequestOpenReport;

        [RelayCommand(CanExecute = nameof(CanRunActions))]
        private async Task OpenReport()
        {
            if (Rows.Count == 0)
            {
                MessageBox.Show("Нет данных для отчёта.", "Отчёт",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string storeInfo = await _orgService.GetShopDisplayAsync();

            var dt = ToDataTable(Rows); // your helper with RU headers

            // 1️⃣ Build Report ViewModel
            var reportVm = _sp.GetRequiredService<ReportViewModel>();
            reportVm.Initialize(dt, SelectedDate, "Обновление товаров", storeInfo);

            // 2️⃣ Generate PDF report document
            var document = new Documents.ReportDocument(reportVm);

            string folder = Path.Combine(Path.GetTempPath(), "Reports");
            Directory.CreateDirectory(folder);

            string filePath = Path.Combine(folder, $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            document.GeneratePdf(filePath);
            PrintPdfSilent(filePath);
            
            // 3️⃣ Show in PDF preview window
            var preview = new DocumentPreviewView(filePath);
            var window = new Window
            {
                Title = reportVm.Title,
                Content = preview,
                
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 900,
                Height = 900,
                ResizeMode = ResizeMode.CanResize,
                ShowInTaskbar = false
            };

            window.Show();

            try
            {
                await _returnsDayReportService.PrintReturnsDayReportAsync();
                await _returnsDayReportService.ShowReturnsDayReportAsync();
               
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
        private async Task ShowReturnReport()
        {

            try
            {
              await _returnsDayReportService.ShowReturnsDayReportAsync();

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
        private void PrintPdfSilent(string filePath)
        {
            using var document = PdfiumViewer.PdfDocument.Load(filePath);
            using var printDocument = document.CreatePrintDocument();

            printDocument.PrintController = new System.Drawing.Printing.StandardPrintController();
            printDocument.Print();
        }

        [RelayCommand] private void Close() => RequestClose?.Invoke();

        private bool CanRunActions() => SelectedDate is not null && Rows.Count > 0 && !IsBusy;

        private void RaiseCanExecutes()
        {
            UpdateCommand.NotifyCanExecuteChanged();
            OpenReportCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
        }

        private static DataTable ToDataTable(ObservableCollection<ImportRow> rows)
        {
            var dt = new DataTable();
            dt.Columns.Add("Артикул", typeof(string));
            dt.Columns.Add("Наименование", typeof(string));
            dt.Columns.Add("Группа", typeof(string));
            dt.Columns.Add("Бренд", typeof(string));
            dt.Columns.Add("Модель", typeof(string));
            dt.Columns.Add("Количество", typeof(int));

            foreach (var r in rows)
                dt.Rows.Add(r.ArticleNumber, r.ProductName, r.GroupName, r.BrandName, r.Model, r.Quantity);

            return dt;
        }

        private static DataTable ToMinimalDataTable(ObservableCollection<ImportRow> rows)
        {
            var dt = new DataTable();
            dt.Columns.Add("Артикул", typeof(string));
            dt.Columns.Add("Количество", typeof(int));

            foreach (var r in rows)
                dt.Rows.Add(r.ArticleNumber, r.Quantity);

            return dt;
        }

    }
}
