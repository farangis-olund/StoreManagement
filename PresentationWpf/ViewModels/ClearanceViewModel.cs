using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
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

namespace PresentationWpf.ViewModels;

public partial class ClearanceViewModel : ObservableObject
{
    private readonly ClearanceService _service;
    private readonly PermissionService _permissions;
    private readonly UserSessionService _session;
    private ClearanceBonusReport? _report;
    private bool _reportSaved;
    private bool _settingReport;
    private bool _initialized;
    private int _operation;
    private IReadOnlyList<string> _articleNumbers = Array.Empty<string>();

    public ObservableCollection<ClearanceProductRow> Products { get; } = [];
    public ObservableCollection<ClearanceBonusRow> BonusRows { get; } = [];
    public ObservableCollection<ClearanceReportSummary> History { get; } = [];
    public IReadOnlyList<string> Comparisons { get; } = new[] { "=", "≤" };
    [ObservableProperty] private string comparison = "=";
    [ObservableProperty] private string thresholdText = "0";
    [ObservableProperty] private string articleText = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArticleSuggestionSuffix))]
    private string articleSuggestion = "";
    public string ArticleSuggestionSuffix => ArticleSuggestion.Length > ArticleText.TrimStart().Length
        ? ArticleSuggestion[ArticleText.TrimStart().Length..] : "";
    [ObservableProperty] private DateTime? fromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? toDate = DateTime.Today;
    [ObservableProperty] private string percentText = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool hasUnsavedProducts;
    [ObservableProperty] private string message = "";
    [ObservableProperty] private ClearanceReportSummary? selectedHistory;
    [ObservableProperty] private string lastSavedText = "Сохранённых отчётов пока нет.";
    [ObservableProperty] private string reportStatus = "Выберите период и процент, затем сформируйте отчёт.";
    public bool CanAccess => _permissions.Has(PermissionCatalog.Admin) && _permissions.Has(PermissionCatalog.AdminClearance);
    public bool CanWork => CanAccess && !IsBusy;
    public bool CanEditThreshold => CanWork && Comparison != "=";
    public bool CanSaveProducts => CanWork && HasUnsavedProducts;
    public bool CanCalculate => CanWork && !HasUnsavedProducts;
    public bool CanSaveReport => CanWork && _report is { Rows.Count: > 0 } && !_reportSaved;
    public bool CanOutput => CanWork && _report is { Rows.Count: > 0 };
    public bool CanOpenHistory => CanWork && SelectedHistory != null;
    public string ProductsTotal => $"Артикулов: {Products.Count:N0} · Остаток: {Products.Sum(x => (long)x.StockQuantity):N0}";
    public string ProductsStatus => HasUnsavedProducts ? "Есть изменения. Нажмите «Сохранить список»." : "Показан сохранённый список распродажи.";
    public string BonusTotal => _report == null ? "" :
        $"Итого: {_report.TotalQuantity:N0} шт. · Сумма: {_report.TotalAmount:N2} · Бонус: {_report.TotalBonus:N2}";

    public ClearanceViewModel(ClearanceService service, PermissionService permissions, UserSessionService session)
    {
        _service = service; _permissions = permissions; _session = session;
        PropertyChangedEventManager.AddHandler(permissions, PermissionsChanged, nameof(PermissionService.CurrentPermissions));
        Products.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ProductsTotal));
    }

    public Task InitializeAsync() => _initialized || !CanWork ? Task.CompletedTask : RunAsync(async current =>
    {
        var products = await _service.GetProductsAsync();
        if (!current()) return;
        SetProducts(products, false);
        var history = await _service.GetHistoryAsync();
        if (!current()) return;
        SetHistory(history);
        if (history.FirstOrDefault() is { } last) PercentText = last.Percent.ToString("0.##", CultureInfo.CurrentCulture);
        var articles = await _service.GetArticleNumbersAsync();
        if (!current()) return;
        _articleNumbers = articles;
        UpdateArticleSuggestion();
        _initialized = true;
    });

    partial void OnArticleTextChanged(string value) => UpdateArticleSuggestion();

    private void UpdateArticleSuggestion()
    {
        var prefix = ArticleText.TrimStart();
        var match = string.IsNullOrWhiteSpace(prefix) || !CanAccess ? null : _articleNumbers.FirstOrDefault(article =>
            article.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        ArticleSuggestion = match != null && match.Length > prefix.Length ? match : "";
        // Typing another character can keep the same match while shortening its visible suffix.
        OnPropertyChanged(nameof(ArticleSuggestionSuffix));
    }

    partial void OnComparisonChanged(string value)
    {
        if (value == "=") ThresholdText = "0";
        OnPropertyChanged(nameof(CanEditThreshold));
    }
    partial void OnIsBusyChanged(bool value) => NotifyCommands();
    partial void OnHasUnsavedProductsChanged(bool value) { OnPropertyChanged(nameof(ProductsStatus)); NotifyCommands(); }
    partial void OnSelectedHistoryChanged(ClearanceReportSummary? value) => NotifyCommands();
    partial void OnFromDateChanged(DateTime? value) => InvalidateReport();
    partial void OnToDateChanged(DateTime? value) => InvalidateReport();
    partial void OnPercentTextChanged(string value) => InvalidateReport();

    private void InvalidateReport()
    {
        if (_settingReport) return;
        _report = null; _reportSaved = false; BonusRows.Clear();
        ReportStatus = "Параметры изменены. Сформируйте отчёт заново.";
        OnPropertyChanged(nameof(BonusTotal)); NotifyCommands();
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private Task GenerateProductsAsync() => RunAsync(async current =>
    {
        if (!int.TryParse(ThresholdText, NumberStyles.None, CultureInfo.InvariantCulture, out var threshold) || threshold < 0)
            throw new ArgumentException("Укажите целое неотрицательное количество продаж.");
        if (HasUnsavedProducts && !Confirm("Заменить несохранённый список результатом отбора?")) return;
        var rows = await _service.GenerateProductsAsync(Comparison == "=" ? 0 : threshold);
        if (!current()) return;
        SetProducts(rows, true);
        Message = rows.Count == 0 ? "По этому условию товары не найдены. Сохранение очистит список распродажи." : "Список сформирован. Проверьте его и сохраните.";
    });

    [RelayCommand(CanExecute = nameof(CanWork))]
    private Task ReloadProductsAsync() => RunAsync(async current =>
    {
        if (HasUnsavedProducts && !Confirm("Отменить изменения и загрузить сохранённый список?")) return;
        var rows = await _service.GetProductsAsync();
        if (current()) SetProducts(rows, false);
    });

    [RelayCommand(CanExecute = nameof(CanWork))]
    private Task AddProductAsync() => RunAsync(async current =>
    {
        var row = await _service.FindProductAsync(ArticleText);
        if (!current()) return;
        if (Products.Any(p => string.Equals(p.ArticleNumber, row.ArticleNumber, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Этот артикул уже есть в списке.");
        var index = 0;
        while (index < Products.Count && StringComparer.OrdinalIgnoreCase.Compare(Products[index].ArticleNumber, row.ArticleNumber) < 0) index++;
        Products.Insert(index, row); ArticleText = ""; HasUnsavedProducts = true; InvalidateReport();
    });

    [RelayCommand(CanExecute = nameof(CanWork))]
    private void RemoveProduct(ClearanceProductRow? row)
    {
        if (!CanWork || row == null || !Products.Remove(row)) return;
        HasUnsavedProducts = true; InvalidateReport();
    }

    [RelayCommand(CanExecute = nameof(CanSaveProducts))]
    private Task SaveProductsAsync()
    {
        if (!CanSaveProducts) return Task.CompletedTask;
        return RunAsync(async current =>
        {
            if (!Confirm(Products.Count == 0 ? "Очистить сохранённый список распродажи?" : $"Сохранить список распродажи: {Products.Count} артикулов?")) return;
            await _service.SaveProductsAsync(Products.Select(p => p.ArticleNumber).ToArray());
            if (!current()) return;
            HasUnsavedProducts = false; InvalidateReport(); Message = "Список распродажи сохранён.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanCalculate))]
    private Task CalculateBonusAsync()
    {
        if (!CanCalculate) return Task.CompletedTask;
        return RunAsync(async current =>
        {
            if (FromDate == null || ToDate == null) throw new ArgumentException("Укажите обе даты периода.");
            if (!TryPercent(PercentText, out var percent)) throw new ArgumentException("Укажите корректный процент бонуса.");
            InvalidateReport();
            var report = await _service.CalculateBonusAsync(FromDate.Value, ToDate.Value, percent);
            if (!current()) return;
            ShowReport(report, false);
            if (report.Rows.Count == 0) Message = "За выбранный период нет продаж товаров из сохранённого списка распродажи.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanSaveReport))]
    private Task SaveReportAsync()
    {
        if (!CanSaveReport) return Task.CompletedTask;
        var report = _report!;
        return RunAsync(async current =>
        {
            if (!Confirm($"Сохранить отчёт за {report.FromDate:dd.MM.yyyy} — {report.ToDate:dd.MM.yyyy}?\nБонус: {report.TotalBonus:N2}")) return;
            await _service.SaveReportAsync(report);
            if (!current()) return;
            _reportSaved = true; ReportStatus = "Отчёт сохранён."; NotifyCommands();
            var history = await _service.GetHistoryAsync();
            if (current()) { SetHistory(history); Message = "Данные о бонусах сохранены."; }
        });
    }

    [RelayCommand(CanExecute = nameof(CanWork))]
    private Task RefreshHistoryAsync() => RunAsync(async current =>
    {
        var history = await _service.GetHistoryAsync();
        if (current()) SetHistory(history);
    });

    [RelayCommand(CanExecute = nameof(CanOpenHistory))]
    private Task OpenHistoryAsync()
    {
        if (!CanOpenHistory) return Task.CompletedTask;
        var id = SelectedHistory!.Id;
        return RunAsync(async current =>
        {
            var report = await _service.GetReportAsync(id);
            if (current()) ShowReport(report, true);
        });
    }

    private void ShowReport(ClearanceBonusReport report, bool saved)
    {
        _settingReport = true;
        try { FromDate = report.FromDate; ToDate = report.ToDate; PercentText = report.Percent.ToString("0.##", CultureInfo.CurrentCulture); }
        finally { _settingReport = false; }
        _report = report; _reportSaved = saved; BonusRows.Clear();
        foreach (var row in report.Rows) BonusRows.Add(row);
        ReportStatus = saved ? "Сохранённый отчёт." : "Отчёт сформирован. Его можно сохранить.";
        OnPropertyChanged(nameof(BonusTotal)); NotifyCommands();
    }
    private void SetProducts(IEnumerable<ClearanceProductRow> rows, bool dirty)
    {
        Products.Clear(); foreach (var row in rows) Products.Add(row);
        HasUnsavedProducts = dirty; InvalidateReport();
    }
    private void SetHistory(IEnumerable<ClearanceReportSummary> rows)
    {
        var selected = SelectedHistory?.Id;
        History.Clear(); foreach (var row in rows) History.Add(row);
        SelectedHistory = History.FirstOrDefault(x => x.Id == selected) ?? History.FirstOrDefault();
        LastSavedText = History.FirstOrDefault() is { } last
            ? $"Последний сохранённый период: {last.FromDate:dd.MM.yyyy} — {last.ToDate:dd.MM.yyyy} · {last.Percent:0.##}%"
            : "Сохранённых отчётов пока нет.";
    }

    [RelayCommand(CanExecute = nameof(CanOutput))]
    private void Print()
    {
        if (!CanOutput) return;
        try { PrintReport(_report!); }
        catch (Exception ex) { Message = ex.GetBaseException().Message; }
    }
    protected virtual void PrintReport(ClearanceBonusReport report)
    {
        var folder = Path.Combine(Path.GetTempPath(), "ClearanceReports"); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"ClearanceBonus_{Guid.NewGuid():N}.pdf");
        new ClearanceBonusDocument(report, _session.OrganizationDisplayName).GeneratePdf(path);
        new Window { Title = "Бонусы за распродажу", Content = new DocumentPreviewView(path), Width = 950, Height = 900,
            WindowStartupLocation = WindowStartupLocation.CenterScreen }.ShowDialog();
    }
    [RelayCommand(CanExecute = nameof(CanOutput))]
    private void ExportExcel()
    {
        if (!CanOutput) return;
        try { ExportReport(_report!); }
        catch (Exception ex) { Message = ex.GetBaseException().Message; }
    }
    protected virtual void ExportReport(ClearanceBonusReport report)
    {
        var dialog = new SaveFileDialog { Filter = "Книга Excel (*.xlsx)|*.xlsx", DefaultExt = ".xlsx", AddExtension = true,
            FileName = $"Бонусы за распродажу_{report.FromDate:yyyy-MM-dd}_{report.ToDate:yyyy-MM-dd}.xlsx" };
        if (dialog.ShowDialog() != true) return;
        using var workbook = ClearanceBonusExcelExport.Create(report, _session.OrganizationDisplayName);
        workbook.SaveAs(dialog.FileName); Message = "Файл Excel сохранён.";
    }
    protected virtual bool Confirm(string text) => MessageBox.Show(text, "Распродажа", MessageBoxButton.YesNo,
        MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    public static bool TryPercent(string text, out decimal percent) => decimal.TryParse(text.Trim().Replace(',', '.'),
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out percent);

    private async Task RunAsync(Func<Func<bool>, Task> action)
    {
        if (!CanWork) return;
        var version = ++_operation;
        bool Current() => version == _operation && CanAccess;
        IsBusy = true; Message = "";
        try { await action(Current); }
        catch (Exception ex) { if (Current()) Message = ex.GetBaseException().Message; }
        finally { if (Current()) IsBusy = false; }
    }
    private void PermissionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!CanAccess)
        {
            ++_operation; _initialized = false; IsBusy = false; Products.Clear(); History.Clear(); SelectedHistory = null;
            _articleNumbers = Array.Empty<string>(); ArticleSuggestion = "";
            HasUnsavedProducts = false; InvalidateReport(); LastSavedText = ""; Message = "Нет доступа к распродаже.";
        }
        NotifyCommands();
    }
    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(CanWork)); OnPropertyChanged(nameof(CanEditThreshold));
        OnPropertyChanged(nameof(CanSaveProducts)); OnPropertyChanged(nameof(CanCalculate)); OnPropertyChanged(nameof(CanSaveReport));
        OnPropertyChanged(nameof(CanOutput)); OnPropertyChanged(nameof(CanOpenHistory));
        GenerateProductsCommand.NotifyCanExecuteChanged(); ReloadProductsCommand.NotifyCanExecuteChanged();
        AddProductCommand.NotifyCanExecuteChanged(); RemoveProductCommand.NotifyCanExecuteChanged(); SaveProductsCommand.NotifyCanExecuteChanged();
        CalculateBonusCommand.NotifyCanExecuteChanged(); SaveReportCommand.NotifyCanExecuteChanged();
        RefreshHistoryCommand.NotifyCanExecuteChanged(); OpenHistoryCommand.NotifyCanExecuteChanged();
        PrintCommand.NotifyCanExecuteChanged(); ExportExcelCommand.NotifyCanExecuteChanged();
    }
}
