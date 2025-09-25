using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Helpers;
using Infrastructure.Repositories;  
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using PresentationWpf.Views;

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
            IServiceProvider sp
            )
        {
            _orderService = orderService;
            _logRepo = logRepo;
            _productService = productService;
            _export = export;
            _sp = sp;
         

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


        public event Action<DataTable, DateTime?, string>? RequestOpenReport;

        [RelayCommand(CanExecute = nameof(CanRunActions))]
        private void OpenReport()
        {
            if (Rows.Count == 0)
            {
                MessageBox.Show("Нет данных для отчёта.", "Отчёт",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dt = ToDataTable(Rows); // your helper with RU headers

            // 1) Build Report VM
            var reportVm = _sp.GetRequiredService<ReportViewModel>();
            reportVm.Initialize(dt, SelectedDate, "Обновление товаров");

            // 2) Build the view
            var reportView = new ReportView
            {
                DataContext = reportVm
            };

            // 3) Show in a centered modal window (same pattern as Retail)
            var window = new Window
            {
                Title = reportVm.Title,
                Content = reportView,
                Owner = Application.Current.MainWindow,            // important!
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 1000,
                Height = 700,
                ResizeMode = ResizeMode.CanResize,
                ShowInTaskbar = false
            };

            // close when VM asks to close
            void OnClose() { reportVm.RequestClose -= OnClose; window.Close(); }
            reportVm.RequestClose += OnClose;

            window.ShowDialog();
        }


        [RelayCommand] private void Close() => RequestClose?.Invoke();

        private bool CanRunActions() => SelectedDate is not null && Rows.Count > 0 && !IsBusy;

        private void RaiseCanExecutes()
        {
            UpdateCommand.NotifyCanExecuteChanged();
            OpenReportCommand.NotifyCanExecuteChanged();
           
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
    }
}
