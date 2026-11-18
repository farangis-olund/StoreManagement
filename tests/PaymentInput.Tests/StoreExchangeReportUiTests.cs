using System.IO;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClosedXML.Excel;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Services;
using PresentationWpf.Documents;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;
using QuestPDF.Fluent;

internal static class StoreExchangeReportUiTests
{
    public static void Run()
    {
        var permissions = new PermissionService(); permissions.SetPermissions([PermissionCatalog.Reports]);
        var provider = new ReportProvider();
        var navigation = new ReportsViewModel(provider, null!, permissions);
        var home = new ReportsView { DataContext = navigation };
        Layout(home);
        Check(navigation.IsReportHome && navigation.ReportLinks.Count == 9 && navigation.ReportLinks.Select(r => r.Title)
                .SequenceEqual(navigation.ReportLinks.Select(r => r.Title).OrderBy(x => x, StringComparer.Create(CultureInfo.GetCultureInfo("ru-RU"), true))),
            "The report home must contain every report, alphabetically sorted in Russian.");
        Check(Descendants<UniformGrid>(home).Single().Rows == 2 && Descendants<RadioButton>(home).Count() == 10 &&
            Descendants<TextBlock>(home).SelectMany(t => t.Inlines.OfType<Hyperlink>()).Count() == 9,
            "The menu must have two rows plus a home button, with matching clickable report links below.");
        Render(home, "reports-home.png");
        foreach (var item in navigation.ReportLinks.ToArray())
        {
            item.OpenCommand.Execute(null);
            Check(item.ViewModelType.IsInstanceOfType(navigation.CurrentReportView) && item.IsSelected &&
                navigation.ReportLinks.Count(r => r.IsSelected) == 1, "A report link must open and highlight its matching menu entry.");
            navigation.ShowReportHomeCommand.Execute(null);
            Check(navigation.IsReportHome && navigation.ReportLinks.All(r => !r.IsSelected), "The home button must return to the link list.");
        }
        var standard = navigation.ReportLinks.First(r => r.RequiresStandardReports);
        standard.OpenCommand.Execute(null);
        permissions.SetPermissions([PermissionCatalog.Reports, PermissionCatalog.OnlyOfficialCustomer]);
        Check(navigation.IsReportHome && navigation.ReportLinks.Count == 4, "Existing restricted-report visibility must apply to links and menu alike.");
        var calls = provider.Calls;
        standard.OpenCommand.Execute(null);
        Check(provider.Calls == calls, "Hidden report links must not bypass report restrictions.");
        permissions.SetPermissions([]);
        Check(navigation.ReportLinks.Count == 0 && navigation.IsReportHome, "Revoking Reports must remove navigation entries.");
        permissions.SetPermissions([PermissionCatalog.Reports]);

        var service = new ReportData();
        var vm = new CapturedReport(service, permissions);
        var view = new StoreExchangeReportView { DataContext = vm }; Layout(view);
        var grid = Descendants<DataGrid>(view).Single();
        Check(grid.IsReadOnly && grid.Columns.Count == 6 && vm.Rows.Count == 3 && vm.CanOutput &&
            vm.Rows[2].IncomingQuantity == 0 && vm.Rows[2].OutgoingQuantity == 0 && vm.Rows[2].RemainingQuantity == 30,
            "The exchange report must load six read-only columns and enable print/Excel for loaded rows.");
        vm.PrintCommand.Execute(null); vm.ExportExcelCommand.Execute(null);
        Check(vm.Printed!.SequenceEqual(vm.Rows) && vm.Exported!.SequenceEqual(vm.Rows),
            "Print and Excel must use the exact displayed snapshot.");
        Render(view, "store-exchange-report.png");
        var pending = new TaskCompletionSource<IReadOnlyList<StoreExchangeReportRow>>(); service.Next = pending;
        var loading = vm.RefreshAsync();
        Check(vm.Rows.Count == 0 && !vm.CanOutput && !vm.PrintCommand.CanExecute(null), "Loading must clear stale rows and disable output.");
        permissions.SetPermissions([]);
        pending.SetResult(ReportData.Data);
        loading.GetAwaiter().GetResult();
        Check(vm.Rows.Count == 0 && !vm.CanOutput, "A delayed response must not restore data after report access is revoked.");

        var output = Path.Combine(AppContext.BaseDirectory, "test-pdfs"); Directory.CreateDirectory(output);
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        new StoreExchangeReportDocument(ReportData.Data, "Opel", new(2026, 11, 17, 12, 30, 0))
            .GeneratePdf(Path.Combine(output, "store-exchange-sample.pdf"));
        var many = Enumerable.Range(1, 160).Select(i => new StoreExchangeReportRow(i, $"000-ARTICLE-{i:00000}", i, i + 5, 3)).ToArray();
        new StoreExchangeReportDocument(many, "Opel", DateTime.Now).GeneratePdf(Path.Combine(output, "store-exchange-multipage.pdf"));
        new StoreExchangeReportDocument([], "Opel", DateTime.Now).GeneratePdf(Path.Combine(output, "store-exchange-empty.pdf"));
        var path = Path.Combine(AppContext.BaseDirectory, "store-exchange-report.xlsx");
        using (var workbook = StoreExchangeReportExcelExport.Create(ReportData.Data, "Opel", DateTime.Today)) workbook.SaveAs(path);
        using (var workbook = new XLWorkbook(path))
        {
            var sheet = workbook.Worksheet(1);
            Check(sheet.Cell(6, 2).GetString() == "0002301765" && sheet.Cell(6, 2).DataType == XLDataType.Text &&
                sheet.Cell(7, 2).GetString() == "170 451", "Excel must preserve zeros and spaces in articles.");
            Check(sheet.Cell(6, 3).GetValue<long>() == 20 && sheet.Cell(6, 6).GetValue<long>() == 18 &&
                sheet.Cell(7, 6).GetValue<long>() == -5 && sheet.Cell(8, 2).GetString() == "NO-EXCHANGE" &&
                sheet.Cell(8, 4).GetValue<long>() == 0 && sheet.Cell(8, 5).GetValue<long>() == 0 &&
                sheet.Cell(8, 6).GetValue<long>() == 30 && sheet.Cell(9, 6).GetValue<long>() == 43 &&
                !sheet.Cell(6, 6).HasFormula, "Excel quantities and totals must remain numeric and agree with the displayed balance.");
        }
        Console.WriteLine("PASS: two-row report menu and alphabetical links open matching views and respect existing restrictions; exchange report snapshot, stale-response protection, numeric Excel/leading zeros, and single/multiple-page PDFs");
    }

    private sealed class ReportProvider : IServiceProvider
    {
        public int Calls;
        public object GetService(Type type) { Calls++; return RuntimeHelpers.GetUninitializedObject(type); }
    }
    private sealed class ReportData : StoreExchangeReportService
    {
        public static readonly StoreExchangeReportRow[] Data = [new(1, "0002301765", 20, 7, 5), new(2, "170 451", 0, 5, 0),
            new(3, "NO-EXCHANGE", 30, 0, 0)];
        public TaskCompletionSource<IReadOnlyList<StoreExchangeReportRow>>? Next;
        public ReportData() : base(null!, null!) { }
        public override Task<IReadOnlyList<StoreExchangeReportRow>> GetAsync(CancellationToken ct = default)
        {
            var delay = Next; Next = null;
            return delay?.Task ?? Task.FromResult<IReadOnlyList<StoreExchangeReportRow>>(Data);
        }
    }
    private sealed class CapturedReport(ReportData data, PermissionService permissions)
        : StoreExchangeReportViewModel(data, new UserSessionService(null!, null!, null!), permissions)
    {
        public IReadOnlyList<StoreExchangeReportRow>? Printed, Exported;
        protected override void PrintReport(IReadOnlyList<StoreExchangeReportRow> rows, DateTime asOf) => Printed = rows;
        protected override void ExportReport(IReadOnlyList<StoreExchangeReportRow> rows, DateTime asOf) => Exported = rows;
    }
    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(1280, 850)); view.Arrange(new Rect(0, 0, 1280, 850)); view.UpdateLayout();
    }
    private static void Render(FrameworkElement view, string name)
    {
        var bitmap = new RenderTargetBitmap(1280, 850, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, name)); png.Save(stream);
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
