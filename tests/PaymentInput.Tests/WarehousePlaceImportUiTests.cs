using ClosedXML.Excel;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Services;
using PresentationWpf.Documents;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;
using QuestPDF.Fluent;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class WarehousePlaceImportUiTests
{
    public static void Run()
    {
        var previousContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            RunOnDispatcher();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
    }

    private static void RunOnDispatcher()
    {
        var permissions = new PermissionService(); permissions.SetPermissions([PermissionCatalog.Inventory]);
        var session = new UserSessionService(null!, null!, null!);
        var state = new WarehousePlaceImportState(session);
        var service = new Places();
        var vm = new CapturedLocation(service, state, permissions, session);
        var view = new StockLocationView { DataContext = vm };
        Layout(view, 1280, 800);
        var errorsButton = Descendants<Button>(view).Single(b => ReferenceEquals(b.Command, vm.ShowImportErrorsCommand));
        Check(vm.CanEdit && !vm.CanShowImportErrors && !errorsButton.IsEnabled, "The error-list button must start disabled.");
        vm.SelectedArticle = "000123";
        Check(vm.NewWarehousePlace == "OLD" && vm.StockItems.Count == 1, "Existing manual selection must show the current location.");
        var folder = Path.Combine(AppContext.BaseDirectory, "test-excel"); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "warehouse-place-import.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Места");
            sheet.Cell(1, 1).Value = "Артикул"; sheet.Cell(1, 2).Value = "Место";
            sheet.Cell(2, 1).Value = "000123"; sheet.Cell(2, 2).Value = "A12/1";
            sheet.Cell(3, 1).Value = "ABC-456"; sheet.Cell(3, 2).Value = "A12/2";
            sheet.Cell(4, 1).Value = "000-NOT-FOUND"; sheet.Cell(4, 2).Value = "B2/3";
            sheet.Cell(5, 1).Value = "NO-PLACE";
            workbook.SaveAs(path);
        }
        service.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var import = vm.ImportFileAsync(path);
        PumpUntil(() => service.ImportCalls == 1);
        var reopened = new CapturedLocation(service, state, permissions, session);
        Check(state.IsImporting && !vm.CanEdit && !reopened.CanEdit && !vm.CloseCommand.CanExecute(null),
            "Import must lock manual changes and prevent a second import even after reopening Inventory.");
        Wait(reopened.ImportFileAsync(path));
        Check(service.ImportCalls == 1, "A reopened view must not launch a concurrent import.");
        service.Pending.SetResult(new(2, 0, [new(4, "000-NOT-FOUND", "B2/3", "Артикул не найден в базе.", WarehousePlaceImportIssueKind.ArticleNotFound)])
        {
            UpdatedProducts = [new() { ArticleNumber = "000123", ProductName = "Тестовый товар", WarehousePlace = "A12/1" },
                new() { ArticleNumber = "ABC-456", ProductName = "Второй товар", WarehousePlace = "A12/2" }]
        });
        Wait(import);
        Layout(view, 1280, 800);
        Check(vm.Completed?.RowCount == 4 && vm.Completed.UpdatedCount == 2 && vm.Completed.MissingCount == 1 &&
            vm.Completed.Issues.Count == 2 && service.Imported!.Count == 3 && service.Imported[0].RowNumber == 2,
            "Completion must combine missing articles and invalid rows, preserve leading zeros, and skip the header.");
        Check(vm.CanShowImportErrors && errorsButton.IsEnabled && reopened.CanShowImportErrors &&
            vm.SelectedArticle == null && vm.NewWarehousePlace == null && vm.StockItems.Count == 2 && reopened.StockItems.Count == 2 &&
            vm.StockItems[0].ArticleNumber == "000123" && vm.StockItems[0].WarehousePlace == "A12/1" &&
            vm.StockItems[1].ArticleNumber == "ABC-456" && vm.StockItems[1].WarehousePlace == "A12/2" &&
            Descendants<DataGrid>(view).Single().Items.Count == 2,
            "Import must show every successful product with new places, clear the old single-product selection, exclude failures and refresh reopened views.");
        var fresh = new CapturedLocation(service, state, permissions, session);
        Check(fresh.StockItems.Count == 2, "Reopening Inventory must retain the imported-product table.");
        vm.SelectedArticle = "000123";
        Check(vm.StockItems.Count == 1 && vm.NewWarehousePlace == "A12/1", "Manual selection must still show the selected product.");
        vm.SelectedArticle = null;
        Check(vm.StockItems.Count == 2, "Clearing the manual selection must restore the imported list.");
        vm.ShowImportErrorsCommand.Execute(null);
        Check(ReferenceEquals(vm.Opened, state.Snapshot), "The error button must open the stored result, without repeating import.");
        Layout(view, 1280, 800);
        Check(Descendants<DataGridRow>(view).Count() == 2, "Both imported products must be visible as actual grid rows.");
        Render(view, "warehouse-place-import.png", 1280, 800);
        var report = new CapturedErrors(state.Snapshot!, permissions);
        var window = new WarehousePlaceImportErrorsWindow { DataContext = report };
        var content = (FrameworkElement)window.Content;
        Layout(content, 960, 560);
        var grid = Descendants<DataGrid>(content).Single();
        Check(grid.IsReadOnly && grid.Columns.Count == 4 && report.Issues.Count == 2,
            "The result window must show read-only Excel row, article, location and reason columns.");
        report.PrintCommand.Execute(null);
        Check(report.PrintCalls == 1, "The result list must offer printing.");
        Render(content, "warehouse-place-import-errors.png", 960, 560);
        var previous = state.Snapshot;
        var bad = Path.Combine(folder, "warehouse-place-bad.xlsx"); File.WriteAllText(bad, "not an Excel file");
        Wait(vm.ImportFileAsync(bad));
        Check(vm.Failed != null && ReferenceEquals(previous, state.Snapshot) && vm.CanShowImportErrors,
            "An unreadable file must report failure and preserve the previous usable error list.");
        permissions.SetPermissions([]);
        report.PrintCommand.Execute(null);
        Check(!vm.CanEdit && !vm.CanShowImportErrors && report.Issues.Count == 0 && report.PrintCalls == 1,
            "Inventory revocation must disable import/results/printing, including direct command calls.");
        permissions.SetPermissions([PermissionCatalog.Inventory]);
        vm.SelectedArticle = "000123";
        vm.NewWarehousePlace = "MANUAL"; Wait(vm.UpdateWarehousePlaceAsync());
        Check(vm.NewWarehousePlace == "MANUAL" && service.Place == "MANUAL", "Enter-to-save manual location updates must remain supported.");
        vm.SelectedArticle = null;
        Check(vm.StockItems.Count == 2 && vm.StockItems.Single(p => p.ArticleNumber == "000123").WarehousePlace == "MANUAL",
            "Returning to the imported list must preserve subsequent manual location corrections.");
        var pdfFolder = Path.Combine(AppContext.BaseDirectory, "test-pdfs"); Directory.CreateDirectory(pdfFolder);
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        new WarehousePlaceImportErrorsDocument(previous!, "Opel").GeneratePdf(Path.Combine(pdfFolder, "warehouse-place-import-errors.pdf"));
        var many = previous! with { RowCount = 122, Issues = Enumerable.Range(1, 120).Select(i => new WarehousePlaceImportIssue(i,
            $"000-ARTICLE-{i:00000}", "A12/" + i, "Артикул не найден в базе.", WarehousePlaceImportIssueKind.ArticleNotFound)).ToArray() };
        new WarehousePlaceImportErrorsDocument(many, "Opel").GeneratePdf(Path.Combine(pdfFolder, "warehouse-place-import-errors-multipage.pdf"));

        var cleanPath = Path.Combine(folder, "warehouse-place-clean.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Места"); sheet.Cell(1, 1).Value = "000123"; sheet.Cell(1, 2).Value = "C3/3";
            workbook.SaveAs(cleanPath);
        }
        service.Pending = null;
        Wait(vm.ImportFileAsync(cleanPath));
        Check(vm.Completed!.Issues.Count == 0 && !vm.CanShowImportErrors && !reopened.CanShowImportErrors &&
            vm.SelectedArticle == null && vm.StockItems.Count == 1 && vm.StockItems[0].WarehousePlace == "C3/3" && fresh.StockItems.Count == 1,
            "A clean import must replace both the old issues and the imported-product table, without retaining previous rows.");
        var token = state.BeginImport(); session.Logout();
        Check(token.IsCancellationRequested && state.Snapshot == null && vm.StockItems.Count == 0 && fresh.StockItems.Count == 0,
            "Changing accounts must cancel an active import and clear previous results and imported rows.");
        state.EndImport(); window.Close();
        Console.WriteLine("PASS: warehouse-place import UI, initial/error/clean button states, shared busy lock, header skipping, combined issues, fresh place, session-only history, unreadable file/revocation guards and printable multipage results");
    }

    private sealed class Places() : WarehousePlaceService(null!, null!)
    {
        public string Place = "OLD";
        public int ImportCalls;
        public IReadOnlyList<WarehousePlaceImportRow>? Imported;
        public TaskCompletionSource<WarehousePlaceImportResult>? Pending;
        public override Task<IReadOnlyList<string>> GetArticlesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["000123"]);
        public override Task<Product?> GetProductAsync(string article, CancellationToken ct = default) =>
            Task.FromResult<Product?>(new Product { ArticleNumber = article, ProductName = "Тестовый товар", WarehousePlace = Place });
        public override Task<int> UpdatePlaceAsync(string article, string place, CancellationToken ct = default) { Place = place; return Task.FromResult(1); }
        public override async Task<WarehousePlaceImportResult> ImportAsync(IReadOnlyList<WarehousePlaceImportRow> rows, CancellationToken ct = default)
        {
            ImportCalls++; Imported = rows;
            var result = Pending == null ? new WarehousePlaceImportResult(rows.Count, 0, [])
                { UpdatedProducts = rows.Select(r => new Product { ArticleNumber = r.ArticleNumber, ProductName = "Тестовый товар", WarehousePlace = r.WarehousePlace }).ToArray() }
                : await Pending.Task;
            ct.ThrowIfCancellationRequested(); Place = rows.First().WarehousePlace;
            return result;
        }
    }
    private sealed class CapturedLocation(Places service, WarehousePlaceImportState state, PermissionService permissions, UserSessionService session)
        : StockLocationViewModel(service, state, permissions, session)
    {
        public WarehousePlaceImportSnapshot? Completed, Opened;
        public string? Failed;
        protected override void NotifyImportCompleted(WarehousePlaceImportSnapshot snapshot) => Completed = snapshot;
        protected override void NotifyImportFailed(string message) => Failed = message;
        protected override void OpenImportErrors(WarehousePlaceImportSnapshot snapshot) => Opened = snapshot;
    }
    private sealed class CapturedErrors(WarehousePlaceImportSnapshot snapshot, PermissionService permissions)
        : WarehousePlaceImportErrorsViewModel(snapshot, "Opel", permissions)
    {
        public int PrintCalls;
        protected override void PrintReport() => PrintCalls++;
    }
    private static void Wait(Task task) { PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void PumpUntil(Func<bool> complete)
    {
        if (complete()) return;
        var frame = new DispatcherFrame(); var started = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => { if (complete() || DateTime.UtcNow - started > TimeSpan.FromSeconds(15)) frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        Check(complete(), "The UI operation timed out.");
    }
    private static void Layout(FrameworkElement view, int width, int height)
    {
        view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
    }
    private static void Render(FrameworkElement view, string name, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, name)); encoder.Save(stream);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) yield return typed;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
