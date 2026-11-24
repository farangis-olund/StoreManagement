using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Services;
using PresentationWpf.Dtos;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class ProductExpiryUiTests
{
    public static int Run()
    {
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            ClearanceUiTests.LoadResources();
            CheckEditor(); CheckRetail(); CheckHome(); CheckInvoicePlaces();
            Console.WriteLine("PASS: expiry editor, article/brand filters, save/delete, read-only seller view, revoked/late results, row colors, combined dots, live changes, legend/notification layout and home link");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void CheckEditor()
    {
        var permissions = new PermissionService(); permissions.SetPermissions([PermissionCatalog.Inventory]);
        var service = new Data(); var vm = new Probe(service, permissions);
        Wait(vm.InitializeAsync());
        var view = new ProductExpiryView { DataContext = vm }; Layout(view);
        Check(vm.BatchesView.Cast<ExpiryBatchRow>().Count() == 3, "Active batch list failed");
        vm.SelectedBrand = vm.Brands.Single(x => x.Id == 2);
        Check(vm.BatchesView.Cast<ExpiryBatchRow>().Count() == 1, "Brand filter failed");
        vm.SelectedBrand = vm.Brands[0]; vm.ArticleFilter = "001";
        Check(vm.BatchesView.Cast<ExpiryBatchRow>().Count() == 2, "Article filter must keep separate batches");
        vm.ArticleFilter = "";
        var articleBox = (ComboBox)view.FindName("BatchArticleBox"); articleBox.SelectedItem = vm.Products[0]; Layout(view);
        Check(vm.ArticleText == "0001", "Selecting product must enter only its article, not display description");
        vm.SelectedBatch = vm.Batches[0];
        Check(vm.WarehousePlace == "А-01" && vm.ManufacturedOn == vm.Batches[0].ManufacturedOn, "Selection did not fill editor");
        vm.WarehousePlace = "Б-02"; vm.QuantityText = "0"; Wait(vm.SaveCommand.ExecuteAsync(null));
        Check(service.Saved is { WarehousePlace: "Б-02", Quantity: 0 } && service.Saved.Id != Guid.Empty && service.Saved.Version!.Length == 1,
            "Saving lost batch identity, availability or concurrency token");
        vm.NewBatchCommand.Execute(null); vm.ArticleText = "0001"; vm.ManufacturedOn = null;
        Wait(vm.SaveCommand.ExecuteAsync(null)); Check(vm.Message.Contains("обе даты"), "Missing date was accepted");
        vm.SelectedBatch = vm.Batches[0]; Wait(vm.DeleteCommand.ExecuteAsync(null)); Check(service.Deleted == vm.Batches[0].Id, "Delete targeted wrong batch");
        Render(view, "expiry-inventory.png", 1450, 900); Render(view, "expiry-inventory-compact.png", 1000, 760);
        vm.SetPriorityMode(true); permissions.SetPermissions([PermissionCatalog.Orders]); Wait(vm.InitializeAsync()); Layout(view);
        Check(!vm.CanManage && !vm.CanEdit && !vm.SaveCommand.CanExecute(null) && !vm.ImportCommand.CanExecute(null), "Seller can edit inventory batches");
        service.Saved = null; Wait(vm.SaveCommand.ExecuteAsync(null)); Check(service.Saved == null, "A direct command bypassed read-only seller mode");
        Check(Descendants<Button>(view).Where(b => Equals(b.Content, "Сохранить партию")).Single().IsVisible == false, "Seller editor is still visible");
        Render(view, "expiry-seller.png", 1450, 800);
        var pending = new TaskCompletionSource<IReadOnlyList<ExpiryBatchRow>>(); service.Pending = pending.Task;
        var task = vm.RefreshCommand.ExecuteAsync(null); permissions.SetPermissions([]); pending.SetResult(service.Rows); Wait(task);
        Check(vm.Batches.Count == 0 && vm.Products.Count == 0 && !vm.IsBusy && !vm.CanAccess, "Late query restored revoked data");
    }
    private static void CheckRetail()
    {
        ProductModel Product(string article, DateTime? expires, bool clearance = false) => (ProductModel)new Product
        {
            ArticleNumber = article, ProductName = "Масло моторное", BrandName = "BOSCH", Quentity = 10,
            PriceLevel1 = 10, NearestExpiryDate = expires, ExpiryHint = "Годен до " + expires?.ToString("dd.MM.yyyy") + "; место А-01", IsClearance = clearance
        };
        var near = Product("0001", DateTime.Today.AddYears(1)); var expired = Product("0002", DateTime.Today.AddDays(-1));
        var both = Product("0003", DateTime.Today.AddMonths(1), true); var far = Product("0004", DateTime.Today.AddYears(1).AddDays(1));
        Check(near.IsNearExpiry && !near.IsExpired && expired.IsExpired && !far.HasExpiryWarning, "Product date mapping/boundary failed");
        var view = new RetailView { DataContext = new RetailContext() };
        var cart = (DataGrid)view.FindName("CartGrid"); var search = (DataGrid)view.FindName("SearchResultsGrid");
        cart.ItemsSource = new[] { near, expired, both, far }; search.ItemsSource = new[] { near, expired, both, far };
        Layout(view, 1750, 1250);
        foreach (var grid in new[] { cart, search })
        {
            foreach (var product in new[] { near, expired, both })
            {
                var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(product);
                var cells = Descendants<DataGridCell>(row).ToArray();
                Check(cells.Length > 5 && NotYellow(row.Background) && cells.All(x => NotYellow(x.Background)),
                    "Expiry markers must not color the row background: " + grid.Name + " / " + product.ArticleNumber);
            }
            var bothRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(both);
            Check(Descendants<Ellipse>(bothRow).Count(x => x.Visibility == Visibility.Visible) == 2, "Combined clearance/expiry marks must both be visible");
            var expiredRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(expired);
            var expiredDot = Descendants<Ellipse>(expiredRow).Single(x => x.Visibility == Visibility.Visible);
            Check(((SolidColorBrush)expiredDot.Fill).Color == (Color)ColorConverter.ConvertFromString("#D6A400"),
                "Near and expired articles must use the same expiry marker");
            grid.SelectedItem = near; Layout(view, 1750, 1250);
            Check(Descendants<DataGridCell>((DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(near)).All(x =>
                NotYellow(x.Background)), "Selection must not restore a yellow background");
            grid.SelectedItem = null;
        }
        near.NearestExpiryDate = null; Layout(view, 1750, 1250);
        Check(!near.HasExpiryWarning && !near.IsNearExpiry, "Removed expiry did not clear highlight");
        near.NearestExpiryDate = DateTime.Today.AddMonths(2);
        Render(view, "expiry-retail.png", 1750, 1250);
        var legend = (StackPanel)view.FindName("ProductStatusLegend"); var notices = (StackPanel)view.FindName("ProductRequestPanel");
        var controls = (Grid)view.FindName("ProductSearchControls");
        Check(legend.Orientation == Orientation.Vertical && legend.Children.Count == 2, "Legend must contain two vertically arranged labels");
        var input = (TextBox)view.FindName("SearchBox");
        foreach (var width in new[] { 1750, 1250, 1000 })
        {
            Layout(view, width, 1250);
            Rect Bounds(FrameworkElement element) => new(element.TranslatePoint(new Point(), view), element.RenderSize);
            Check(!Bounds(legend).IntersectsWith(Bounds(input)) && !Bounds(legend).IntersectsWith(Bounds(notices)), "Legend overlaps input or notifications");
            Check(Grid.GetRow(notices) == 3 && Grid.GetRow(legend) == (width == 1000 ? 2 : 1), "Legend does not adapt to available width");
            Check(Math.Abs(Bounds(controls).Right - Bounds(legend).Right - 5) < 1, "Legend must remain close to the right border");
        }
        Render(view, "expiry-retail-compact.png", 1250, 1250);
        var catalog = new ProductExpiryCatalog(); var vm = new RetailViewModel(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            new PermissionService(), null!, expiryCatalog: catalog);
        vm.ProductList.Add(far); vm.ProductList.Add(near); vm.ProductList.Add(both); vm.SelectedProductList.Add(both);
        vm.RefreshExpiryStatuses();
        Check(vm.ProductListView.Cast<ProductModel>().Select(x => x.ArticleNumber).SequenceEqual(["0003", "0001", "0004"]),
            "Search must prioritize usable batches by nearest expiry");
        catalog.Publish([new("0001", DateTime.Today.AddDays(-1), "Expired A1"), new("0003", DateTime.Today.AddDays(2), "Near A2")]);
        Check(near.IsExpired && both.IsNearExpiry && near.ExpiryHint == "Expired A1", "Live expiry update missed an open product/cart");
        catalog.Publish([]); Check(!near.HasExpiryWarning && !both.HasExpiryWarning && both.IsClearance, "Live removal changed clearance or retained expiry");
        static bool NotYellow(Brush? brush) => brush is not SolidColorBrush solid ||
            (solid.Color != (Color)ColorConverter.ConvertFromString("#FFF3BF") && solid.Color != (Color)ColorConverter.ConvertFromString("#FFE082"));
    }
    private static void CheckHome()
    {
        var called = 0; var window = new Window { DataContext = new HomeContext(new RelayCommand(() => called++)) };
        var view = new Welcome(); window.Content = view; Layout(view);
        var link = (Hyperlink)view.FindName("ExpiryProductsLink");
        Check(link.Command?.CanExecute(null) == true, "Home expiry link has no command"); link.Command!.Execute(null);
        Check(called == 1, "Home expiry link did not navigate");
    }
    private static void CheckInvoicePlaces()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var vm = new OrderInvoiceViewModel(null!, null!, null!)
        {
            InvoiceNumber = "СЧ-LOT123", InvoiceDate = DateTime.Today, ShopName = "Auto Spaire", CustomerName = "Клиент",
            TotalSum = 28, TotalSumInWords = "Двадцать восемь", RowCount = 1,
            OrderDetails = [new() { ArticleNumber = "LOT-01", ProductName = "Масло моторное", BrandName = "BOSCH", Model = "", Marka = "",
                Quentity = 4, Price = 7, Total = 28, WarehousePlace = "А1, А2" }]
        };
        var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "expiry-artifacts"); Directory.CreateDirectory(dir);
        foreach (var barter in new[] { false, true })
        {
            var path = System.IO.Path.Combine(dir, barter ? "expiry-barter-invoice.pdf" : "expiry-invoice.pdf");
            QuestPDF.Infrastructure.IDocument document = barter ? new PresentationWpf.Documents.BarterInvoiceDocument(vm) : new PresentationWpf.Documents.InvoiceDocument(vm);
            QuestPDF.Fluent.GenerateExtensions.GeneratePdf(document, path);
            using var pdf = PdfiumViewer.PdfDocument.Load(path);
            var text = pdf.GetPdfText(0).Replace("\r", "").Replace("\n", " ");
            Check(text.Contains("А1, А2"), "Printed invoice truncated or omitted the two batch places");
            using var rendered = pdf.Render(0, 1240, 1754, 150, 150, PdfiumViewer.PdfRenderFlags.Annotations);
            rendered.Save(System.IO.Path.ChangeExtension(path, ".png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }
    private static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
    private static void Wait(Task task)
    {
        while (!task.IsCompleted) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        task.GetAwaiter().GetResult();
    }
    private static void Layout(FrameworkElement view, int width = 1450, int height = 900)
    {
        view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static void Render(FrameworkElement view, string name, int width, int height)
    {
        Layout(view, width, height); var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "expiry-artifacts"); Directory.CreateDirectory(dir);
        using var stream = File.Create(System.IO.Path.Combine(dir, name)); encoder.Save(stream);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject node) where T : DependencyObject
    {
        if (node is T found) yield return found;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    private sealed record HomeContext(RelayCommand NavigateToExpiryCommand) { public bool CanViewExpiry => true; }
    private sealed class RetailContext
    {
        public bool IsBarter { get; set; } = true;
        public string RequestMessage => "Товар отсутствует. Создайте заявку на пополнение склада.";
        public string RequestStatus => "Уведомление продавцу";
        public bool IsRequestEntryVisible => true;
    }
    private sealed class Probe(ProductExpiryService service, PermissionService permissions) : ProductExpiryViewModel(service, permissions)
    {
        protected override bool ConfirmDelete(ExpiryBatchRow row) => true;
    }
    private sealed class Data() : ProductExpiryService(null!, null!)
    {
        public ExpiryBatchInput? Saved; public Guid Deleted;
        public Task<IReadOnlyList<ExpiryBatchRow>>? Pending;
        public IReadOnlyList<ExpiryBatchRow> Rows { get; } = [
            new(Guid.NewGuid(), "0001", "Масло моторное", 1, "BOSCH", 10, DateTime.Today.AddYears(-1), DateTime.Today.AddMonths(1).AddDays(-1), "А-01", 3, [1], DateTime.Today),
            new(Guid.NewGuid(), "0001", "Масло моторное", 1, "BOSCH", 10, DateTime.Today.AddMonths(-6), DateTime.Today.AddMonths(8), "А-02", 7, [1], DateTime.Today),
            new(Guid.NewGuid(), "0002", "Масло трансмиссионное", 2, "MOTUL", 4, DateTime.Today.AddYears(-2), DateTime.Today.AddDays(-2), "Б-01", 4, [1], DateTime.Today)
        ];
        public override Task<IReadOnlyList<ExpiryBatchRow>> GetBatchesAsync(bool priorityOnly, DateTime? asOf = null) => Pending ?? Task.FromResult(Rows);
        public override Task<IReadOnlyList<ExpiryProductOption>> GetProductsAsync() => Task.FromResult<IReadOnlyList<ExpiryProductOption>>([new("0001", "Масло моторное", "BOSCH", "А-01")]);
        public override Task SaveAsync(ExpiryBatchInput input) { Saved = input; return Task.CompletedTask; }
        public override Task DeleteAsync(Guid id, byte[] version) { Deleted = id; return Task.CompletedTask; }
    }
}
