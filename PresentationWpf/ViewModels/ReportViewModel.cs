using System.Data;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Helpers;
using Infrastructure.Services;
using PresentationWpf.Services; 

namespace PresentationWpf.ViewModels;

public partial class ReportViewModel : ObservableObject
{
    private readonly ExportHelper _export;
    private readonly OrganizationInfoService _organization;

    public event Action? RequestClose;

    [ObservableProperty] private string title = "Отчёт";
    [ObservableProperty] private string organizationName = "";
    [ObservableProperty] private DateTime? selectedDate;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    private DataTable? table;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    private bool isBusy;

    public DateTime DisplayDate => SelectedDate?.Date ?? DateTime.Today;

    public ReportViewModel(ExportHelper export, OrganizationInfoService organization)
    {
        _export = export;
        _organization = organization;
       
    }


    public void Initialize(DataTable table, DateTime? date, string? title = null, string? storeInfo = null)
    {
        Table = table;
        SelectedDate = date;
        OrganizationName = storeInfo;
        if (!string.IsNullOrWhiteSpace(title)) Title = title!;
        OnPropertyChanged(nameof(DisplayDate));
    }

    // keep DisplayDate in sync when the selection changes
    partial void OnSelectedDateChanged(DateTime? value)
        => OnPropertyChanged(nameof(DisplayDate));

    private bool CanRunActions() => !IsBusy && Table is { Rows.Count: > 0 };

    // ==== Export ====
    [RelayCommand(CanExecute = nameof(CanRunActions))]
    private async Task Export()
    {
        try
        {
            if (Table is null || Table.Rows.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта.", "Экспорт",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsBusy = true;

            var ok = await _export.ExportExcel(Table, DisplayDate);
            if (ok)
            {
                MessageBox.Show(
                    $"Файл успешно сохранён за {DisplayDate:dd.MM.yyyy}.",
                    "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    "Экспорт не выполнен. Проверьте путь и доступ.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка экспорта: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==== Print (pass the root view as parameter) ====
    [RelayCommand(CanExecute = nameof(CanRunActions))]
    private void Print(FrameworkElement? view)
    {
        if (Table is null || Table.Rows.Count == 0)
        {
            MessageBox.Show("Нет данных для печати.", "Печать",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            PrintHelper.Print(view, $"{Title} — {DisplayDate:dd.MM.yyyy}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка печати: {ex.Message}", "Печать",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke();
}
