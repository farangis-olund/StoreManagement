using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Services;
using PresentationWpf.Documents;
using PresentationWpf.Dtos;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;
using QuestPDF.Fluent;

internal static class ClearanceUiTests
{
    public static int Run()
    {
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            LoadResources();
            AdminPermissionUiTests.Run();
            var permissions = new PermissionService(); permissions.SetPermissions([PermissionCatalog.Admin, PermissionCatalog.AdminClearance]);
            var service = new Data();
            var vm = new Probe(service, permissions);
            Wait(vm.InitializeAsync());
            var view = new ClearanceView { DataContext = vm };
            Layout(view);
            Check(vm.Products.Count == 2 && !vm.HasUnsavedProducts && vm.CanCalculate, "Initial saved products did not load.");
            var articleBox = (TextBox)view.FindName("ArticleBox");
            var keySource = new TestPresentationSource { RootVisual = view };
            articleBox.Text = "00"; articleBox.CaretIndex = articleBox.Text.Length; Layout(view);
            Check(vm.ArticleSuggestion == "000123" && vm.ArticleSuggestionSuffix == "0123" && vm.ArticleText == "00",
                "The grey suggestion must complete the prefix without changing the typed article.");
            var suggestion = (TextBlock)view.FindName("ArticleSuggestionText");
            Check(suggestion.Visibility == Visibility.Visible && suggestion.Text == "0123" &&
                ((SolidColorBrush)suggestion.Foreground).Color == Colors.LightGray,
                "The article completion must be visible in grey after the typed prefix.");
            Render(view, "clearance-article-suggestion.png", 1000, 720);
            articleBox.Text = "000"; Layout(view);
            Check(vm.ArticleSuggestionSuffix == "123", "Typing another character must shorten the same suggestion.");
            articleBox.CaretIndex = articleBox.Text.Length;
            articleBox.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, keySource, Environment.TickCount, Key.Tab)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(vm.ArticleText == "000123" && articleBox.Text == "000123" && vm.ArticleSuggestionSuffix == "" && !vm.HasUnsavedProducts,
                "Tab must accept the full article without adding it to the list.");
            articleBox.Text = "catalog"; Layout(view);
            Check(vm.ArticleSuggestion == "CATALOG-ONLY", "Suggestions must include articles outside the saved clearance list, ignoring case.");
            articleBox.CaretIndex = articleBox.Text.Length;
            articleBox.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, keySource, Environment.TickCount, Key.Right)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(vm.ArticleText == "CATALOG-ONLY", "Right Arrow must accept the full article.");
            articleBox.Text = "NO-MATCH"; Layout(view);
            Check(vm.ArticleSuggestionSuffix == "", "A missing match must clear the old suggestion.");
            articleBox.Text = ""; Layout(view);
            Check(!vm.CanEditThreshold && vm.ThresholdText == "0", "Never-sold selection must lock zero.");
            vm.Comparison = "≤"; vm.ThresholdText = "3";
            Wait(vm.GenerateProductsCommand.ExecuteAsync(null));
            Check(service.Threshold == 3 && vm.HasUnsavedProducts && !vm.CanCalculate && vm.CanSaveProducts, "Generation must produce an editable draft.");
            vm.ArticleText = " 001-ADD "; Wait(vm.AddProductCommand.ExecuteAsync(null));
            Check(vm.Products.Any(p => p.ArticleNumber == "001-ADD"), "Manual article addition failed.");
            vm.RemoveProductCommand.Execute(vm.Products[0]);
            Wait(vm.SaveProductsCommand.ExecuteAsync(null));
            Check(!vm.HasUnsavedProducts && service.SavedProducts!.SequenceEqual(vm.Products.Select(x => x.ArticleNumber)), "List save differs from the displayed draft.");
            Render(view, "clearance-products.png", 1450, 850);
            Render(view, "clearance-products-compact.png", 1000, 720);
            var tabs = Descendants<TabControl>(view).Single(); tabs.SelectedIndex = 1;
            vm.PercentText = "2,5";
            Wait(vm.CalculateBonusCommand.ExecuteAsync(null)); Layout(view);
            Check(vm.BonusRows.Count == 3 && vm.CanSaveReport && vm.CanOutput, "Bonus preview did not enable output/save.");
            vm.PrintCommand.Execute(null); vm.ExportExcelCommand.Execute(null);
            Check(vm.Printed == service.Report && vm.Exported == service.Report, "Output recalculated the report.");
            Wait(vm.SaveReportCommand.ExecuteAsync(null));
            Check(service.Saved == service.Report && !vm.CanSaveReport, "Save must use the exact preview and disable duplicate saving.");
            Render(view, "clearance-bonus.png", 1450, 850);
            Render(view, "clearance-bonus-compact.png", 1000, 720);
            CheckOutput(service.Report);
            vm.PercentText = "bad";
            Check(vm.BonusRows.Count == 0 && !vm.CanSaveReport && !vm.CanOutput, "Changing an input must invalidate stale results.");
            Wait(vm.CalculateBonusCommand.ExecuteAsync(null));
            Check(vm.BonusRows.Count == 0 && vm.Message.Contains("процент"), "Bad percentage reused the previous value.");
            vm.SelectedHistory = vm.History[0]; Wait(vm.OpenHistoryCommand.ExecuteAsync(null));
            Check(vm.BonusRows.Count == 3 && !vm.CanSaveReport && vm.CanOutput && vm.PercentText == "2,5", "Historical report did not restore its exact parameters.");
            permissions.SetPermissions([PermissionCatalog.Admin]);
            Check(vm.Products.Count == 0 && vm.BonusRows.Count == 0 && vm.History.Count == 0 && !vm.CanOutput, "Revocation retained data.");
            var calls = service.Calls;
            Wait(vm.CalculateBonusCommand.ExecuteAsync(null)); Wait(vm.SaveProductsCommand.ExecuteAsync(null));
            vm.PrintCommand.Execute(null); vm.ExportExcelCommand.Execute(null);
            Check(calls == service.Calls, "Direct commands bypassed revoked permission.");
            permissions.SetPermissions([PermissionCatalog.Admin, PermissionCatalog.AdminClearance]);
            Wait(vm.InitializeAsync());
            var delayed = new TaskCompletionSource<IReadOnlyList<ClearanceProductRow>>(); service.Pending = delayed.Task;
            var task = vm.GenerateProductsCommand.ExecuteAsync(null);
            permissions.SetPermissions([]); delayed.SetResult([new("STALE", "", "", 1, 0)]); Wait(task);
            Check(vm.Products.Count == 0 && !vm.IsBusy, "A late query restored revoked content.");
            CheckRetailHighlight();
            Console.WriteLine("PASS: clearance UI, 9 admin tabs, drafts/add/remove/save, preview/history, invalidation, revoked and late requests, exact PDF/Excel snapshots, retail highlighting and 4 layout renders");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void CheckOutput(ClearanceBonusReport report)
    {
        using var workbook = ClearanceBonusExcelExport.Create(report, "Тестовый магазин");
        var sheet = workbook.Worksheet(1);
        Check(sheet.Cell(7, 2).GetString() == "000123" && sheet.Cell(7, 4).GetValue<decimal>() == 100.01m &&
            sheet.LastRowUsed()!.Cell(4).GetValue<decimal>() == report.TotalAmount && sheet.LastRowUsed()!.Cell(5).GetValue<decimal>() == report.TotalBonus,
            "Excel lost text articles or exact monetary values.");
        var folder = Path.Combine(AppContext.BaseDirectory, "clearance-artifacts"); Directory.CreateDirectory(folder);
        workbook.SaveAs(Path.Combine(folder, "clearance-bonus.xlsx"));
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var longReport = report with { Rows = Enumerable.Range(0, 150).Select(i =>
            new ClearanceBonusRow(i / 5, "Продавец " + i / 5, "000123-" + i, 1, 100.01m, 2.50m)).ToArray() };
        var pdf = new ClearanceBonusDocument(longReport, "Тестовый магазин").GeneratePdf();
        Check(pdf.Length > 5000, "Multi-page bonus report failed to render.");
        File.WriteAllBytes(Path.Combine(folder, "clearance-bonus.pdf"), pdf);
    }
    private static void CheckRetailHighlight()
    {
        var product = (ProductModel)new Product { ArticleNumber = "000123", ProductName = "Фильтр масляный", BrandName = "BOSCH",
            IsClearance = true, Quentity = 15, PriceLevel1 = 10, PriceLevel2 = 12 };
        Check(product.IsClearance, "The product mapping lost the clearance flag.");
        var normal = (ProductModel)new Product { ArticleNumber = "A-45", ProductName = "Колодки тормозные", BrandName = "ATE", Quentity = 6 };
        var retail = new RetailView { DataContext = new HighlightContext() };
        var cart = (DataGrid)retail.FindName("CartGrid");
        var search = (DataGrid)retail.FindName("SearchResultsGrid");
        cart.ItemsSource = new[] { product, normal }; search.ItemsSource = new[] { product, normal };
        Layout(retail, 1600, 1300);
        var background = Color.FromRgb(255, 205, 210);
        var selectedBackground = Color.FromRgb(239, 154, 154);
        foreach (var grid in new[] { cart, search })
        {
            var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(product);
            Check(NotRed(row.Background) && Descendants<DataGridCell>(row).All(c => NotRed(c.Background)) &&
                Descendants<System.Windows.Shapes.Ellipse>(row).Count(x => x.Visibility == Visibility.Visible) == 1,
                "Clearance must use a dot without red row backgrounds in cart and search.");
            grid.SelectedItem = product; Layout(retail, 1600, 1300);
            Check(NotRed(row.Background) && Descendants<DataGridCell>(row).All(c => NotRed(c.Background)) &&
                Descendants<System.Windows.Shapes.Ellipse>(row).Count(x => x.Visibility == Visibility.Visible) == 1,
                "Selecting clearance must retain its dot without restoring the red background.");
            grid.SelectedItem = null; Layout(retail, 1600, 1300);
        }
        cart.SelectedItem = product;
        Render(retail, "retail-clearance-rows.png", 1600, 1300);
        retail.DataContext = new HighlightContext { IsBarter = true }; Layout(retail, 1600, 1300);
        var cartRow = (DataGridRow)cart.ItemContainerGenerator.ContainerFromItem(product);
        Check(NotRed(cartRow.Background) && Descendants<System.Windows.Shapes.Ellipse>(cartRow).Count(x => x.Visibility == Visibility.Visible) == 1,
            "Barter styling must preserve the clearance dot without red backgrounds.");
        product.IsClearance = false; Layout(retail, 1600, 1300);
        foreach (var grid in new[] { cart, search })
        {
            var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(product);
            Check(row.Background is not SolidColorBrush brush || (brush.Color != background && brush.Color != selectedBackground),
                "Removing a product from clearance must immediately remove the red row background.");
        }
        var catalog = new ClearanceCatalog();
        var vm = new RetailViewModel(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            new PermissionService(), null!, clearanceCatalog: catalog);
        vm.ProductList.Add(product);
        catalog.Publish([product.ArticleNumber]);
        Check(product.IsClearance && vm.ProductList.Count == 1, "An open order screen did not receive the saved list change.");
        catalog.Publish([]);
        Check(!product.IsClearance && vm.ProductList.Count == 1, "Clearing the saved list did not remove the live highlight.");
        bool NotRed(Brush brush) => brush is not SolidColorBrush solid || (solid.Color != background && solid.Color != selectedBackground);
    }
    internal static void LoadResources()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "PresentationWpf", "App.xaml"))) folder = folder.Parent;
        var source = XDocument.Load(Path.Combine(folder!.FullName, "PresentationWpf", "App.xaml")).Root!;
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var resources = new XElement(ns + "ResourceDictionary", source.Attributes().Where(x => x.IsNamespaceDeclaration),
            source.Element(ns + "Application.Resources")!.Elements());
        foreach (var setter in resources.Descendants(ns + "EventSetter").ToArray()) setter.Remove();
        foreach (var element in resources.Descendants().Where(x => x.Name.NamespaceName.StartsWith("clr-namespace:")))
            element.Name = XName.Get(element.Name.LocalName, element.Name.NamespaceName + ";assembly=PresentationWpf");
        foreach (var attribute in resources.Attributes().Where(x => x.IsNamespaceDeclaration && x.Value.StartsWith("clr-namespace:")))
            attribute.Value += ";assembly=PresentationWpf";
        app.Resources = (ResourceDictionary)XamlReader.Parse(resources.ToString());
    }
    private static void Wait(Task task)
    {
        while (!task.IsCompleted) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        task.GetAwaiter().GetResult();
    }
    private static void Layout(FrameworkElement view, int width = 1450, int height = 850)
    {
        view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static void Render(FrameworkElement view, string name, int width, int height)
    {
        Layout(view, width, height);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var folder = Path.Combine(AppContext.BaseDirectory, "clearance-artifacts"); Directory.CreateDirectory(folder);
        using var output = File.Create(Path.Combine(folder, name)); encoder.Save(output);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject node) where T : DependencyObject
    {
        if (node is T item) yield return item;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private sealed class HighlightContext
    {
        public bool IsBarter { get; set; }
    }
    private sealed class TestPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
    private sealed class Probe(ClearanceService service, PermissionService permissions) : ClearanceViewModel(service, permissions, null!)
    {
        public ClearanceBonusReport? Printed, Exported;
        protected override bool Confirm(string text) => true;
        protected override void PrintReport(ClearanceBonusReport report) => Printed = report;
        protected override void ExportReport(ClearanceBonusReport report) => Exported = report;
    }
    private sealed class Data() : ClearanceService(null!, null!)
    {
        public int Threshold, Calls;
        public string[]? SavedProducts;
        public ClearanceBonusReport? Saved;
        public Task<IReadOnlyList<ClearanceProductRow>>? Pending;
        public ClearanceBonusReport Report = new(Guid.NewGuid(), new(2026, 11, 1), new(2026, 11, 23), 2.5m, DateTime.Now,
            [new(1, "Иван Иванов", "000123", 1, 100.01m, 2.50m), new(1, "Иван Иванов", "A-45", 2, 200, 5), new(2, "Анна Петрова", "000123", 1, 100, 2.50m)]);
        private ClearanceProductRow[] Rows => [new("000123", "Фильтр масляный", "BOSCH", 15, 0), new("A-45", "Колодки тормозные", "ATE", 6, 3)];
        public override Task<IReadOnlyList<string>> GetArticleNumbersAsync()
        { Calls++; return Task.FromResult<IReadOnlyList<string>>(["000123", "001-ADD", "A-45", "CATALOG-ONLY"]); }
        public override Task<IReadOnlyList<ClearanceProductRow>> GetProductsAsync() { Calls++; return Task.FromResult<IReadOnlyList<ClearanceProductRow>>(Rows); }
        public override Task<IReadOnlyList<ClearanceProductRow>> GenerateProductsAsync(int threshold)
        { Calls++; Threshold = threshold; return Pending ?? Task.FromResult<IReadOnlyList<ClearanceProductRow>>(Rows); }
        public override Task<ClearanceProductRow> FindProductAsync(string article)
        { Calls++; return Task.FromResult(new ClearanceProductRow(article.Trim(), "Ремень приводной", "GATES", 10, 2)); }
        public override Task SaveProductsAsync(IEnumerable<string> articles) { Calls++; SavedProducts = articles.ToArray(); return Task.CompletedTask; }
        public override Task<ClearanceBonusReport> CalculateBonusAsync(DateTime fromDate, DateTime toDate, decimal percent)
        { Calls++; return Task.FromResult(Report); }
        public override Task SaveReportAsync(ClearanceBonusReport report) { Calls++; Saved = report; return Task.CompletedTask; }
        public override Task<IReadOnlyList<ClearanceReportSummary>> GetHistoryAsync()
        { Calls++; return Task.FromResult<IReadOnlyList<ClearanceReportSummary>>([new(Report.Id, Report.FromDate, Report.ToDate, Report.Percent, Report.TotalAmount, Report.TotalBonus, Report.CreatedAt)]); }
        public override Task<ClearanceBonusReport> GetReportAsync(Guid id) { Calls++; return Task.FromResult(Report); }
    }
}
