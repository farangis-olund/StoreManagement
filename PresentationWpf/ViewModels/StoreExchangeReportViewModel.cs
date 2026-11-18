using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Services;
using Microsoft.Win32;
using PresentationWpf.Documents;
using PresentationWpf.Services;
using PresentationWpf.Views;
using QuestPDF.Fluent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class StoreExchangeReportViewModel : ObservableObject
{
    private readonly StoreExchangeReportService _service;
    private readonly UserSessionService _session;
    private readonly PermissionService _permissions;
    private int _loadVersion;
    public ObservableCollection<StoreExchangeReportRow> Rows { get; } = [];
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string message = "";
    [ObservableProperty] private DateTime asOf = DateTime.Now;
    public bool CanRefresh => !IsBusy && _permissions.Has(PermissionCatalog.Reports);
    public bool CanOutput => CanRefresh && Rows.Count > 0;
    public string TotalsText => $"Артикулов: {Rows.Count}. На складе: {Rows.Sum(r => r.WarehouseQuantity):N0}. " +
        $"Приход: {Rows.Sum(r => r.IncomingQuantity):N0}. Расход: {Rows.Sum(r => r.OutgoingQuantity):N0}. " +
        $"Остаток: {Rows.Sum(r => r.RemainingQuantity):N0}.";

    public StoreExchangeReportViewModel(StoreExchangeReportService service, UserSessionService session, PermissionService permissions)
    {
        _service = service; _session = session; _permissions = permissions;
        PropertyChangedEventManager.AddHandler(permissions, PermissionsChanged, nameof(PermissionService.CurrentPermissions));
        Rows.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(TotalsText)); NotifyCommands(); };
        _ = RefreshAsync();
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommands();
    private void NotifyCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged(); PrintCommand.NotifyCanExecuteChanged(); ExportExcelCommand.NotifyCanExecuteChanged();
    }
    private void PermissionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_permissions.Has(PermissionCatalog.Reports))
        {
            _loadVersion++; Rows.Clear(); IsBusy = false; Message = "Нет права доступа к отчётам.";
        }
        NotifyCommands();
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task RefreshAsync()
    {
        if (!CanRefresh) return;
        var version = ++_loadVersion;
        IsBusy = true; Rows.Clear(); Message = "";
        try
        {
            var rows = await _service.GetAsync();
            if (version != _loadVersion || !_permissions.Has(PermissionCatalog.Reports)) return;
            foreach (var row in rows) Rows.Add(row);
            AsOf = DateTime.Now;
            Message = Rows.Count == 0 ? "Нет товаров." : "";
        }
        catch (Exception ex) { if (version == _loadVersion) Message = ex.GetBaseException().Message; }
        finally { if (version == _loadVersion) IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanOutput))]
    private void Print()
    {
        if (!CanOutput) return;
        try { PrintReport(Rows.ToArray(), AsOf); }
        catch (Exception ex) { Message = $"Не удалось сформировать отчёт: {ex.GetBaseException().Message}"; }
    }

    protected virtual void PrintReport(IReadOnlyList<StoreExchangeReportRow> rows, DateTime asOf)
    {
        var folder = Path.Combine(Path.GetTempPath(), "StoreExchangeReports"); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"StoreExchanges_{DateTime.Now:yyyyMMdd_HHmmss_fff}.pdf");
        new StoreExchangeReportDocument(rows, _session.OrganizationDisplayName, asOf).GeneratePdf(path);
        new Window { Title = "Об обмене товаров магазинами", Content = new DocumentPreviewView(path),
            Width = 900, Height = 1000, WindowStartupLocation = WindowStartupLocation.CenterScreen }.ShowDialog();
    }

    [RelayCommand(CanExecute = nameof(CanOutput))]
    private void ExportExcel()
    {
        if (!CanOutput) return;
        try { ExportReport(Rows.ToArray(), AsOf); }
        catch (Exception ex) { Message = $"Не удалось сохранить Excel: {ex.GetBaseException().Message}"; }
    }

    protected virtual void ExportReport(IReadOnlyList<StoreExchangeReportRow> rows, DateTime asOf)
    {
        var dialog = new SaveFileDialog { Filter = "Книга Excel (*.xlsx)|*.xlsx", DefaultExt = ".xlsx",
            FileName = $"Обмен товаров магазинами_{asOf:yyyy-MM-dd}.xlsx", AddExtension = true };
        if (dialog.ShowDialog() != true) return;
        using var workbook = StoreExchangeReportExcelExport.Create(rows, _session.OrganizationDisplayName, asOf);
        workbook.SaveAs(dialog.FileName);
        Message = "Файл Excel сохранён.";
    }
}
