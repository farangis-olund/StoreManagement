using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Services;
using PresentationWpf.Documents;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;
using QuestPDF.Fluent;

internal static class ReturnDebtRepaymentReportTests
{
    public static int Run()
    {
        try
        {
            if (Application.Current == null) ClearanceUiTests.LoadResources();
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            var data = new ExchangeData(); var vm = new Probe(data);
            var view = new ReturnDebtRepaymentView { DataContext = vm };
            Layout(view);
            var combo = (ComboBox)view.FindName("StoreFilterComboBox");
            Check(vm.Stores.Select(x => x.StoreCode).SequenceEqual(["Все", "SHOP-A", "SHOP-B"]), "All stores must be the first combo option");
            Check(!vm.ShowCommand.CanExecute(null), "Preview without a selected scope must be disabled");
            foreach (var repayment in new[] { false, true })
            {
                if (repayment) vm.SelectRepaymentCommand.Execute(null); else vm.SelectReturnCommand.Execute(null);
                combo.SelectedItem = vm.Stores[0]; Layout(view);
                Check(vm.IsAllStores && vm.ShowCommand.CanExecute(null) && !vm.SubmitCommand.CanExecute(null) && !vm.IsInputVisible,
                    "All stores must allow preview without allowing a multi-store transaction");
                Wait(vm.SubmitCommand.ExecuteAsync(null));
                Check(vm.Message.Contains("конкретный магазин"), "Direct execution must also reject submitting All");
                Wait(vm.ShowCommand.ExecuteAsync(null));
                var expectedType = repayment ? "получение_товара" : "передача_товара";
                Check(data.LastStore == null && data.LastType == expectedType, "All preview queried an actual store or the wrong debt direction");
                var all = Text(vm.LastPdf);
                Check(all.Contains("Магазины: Все") && all.Contains("SHOP-A") && all.Contains("SHOP-B"), "The combined PDF is missing a store");
                var first = all.IndexOf("Магазин: SHOP-A", StringComparison.Ordinal);
                var second = all.IndexOf("Магазин: SHOP-B", StringComparison.Ordinal);
                Check(first >= 0 && second > first, "Store groups are not sorted");
                var firstGroup = all[first..second]; var secondGroup = all[second..];
                Check(firstGroup.IndexOf("0001", StringComparison.Ordinal) < firstGroup.IndexOf("0002", StringComparison.Ordinal) &&
                    secondGroup.IndexOf("0001", StringComparison.Ordinal) < secondGroup.IndexOf("Z-300", StringComparison.Ordinal),
                    "Articles must be sorted separately within each store, keeping the same article in both groups");
                RenderPdf(vm.LastPdf, Path.Combine(Output, repayment ? "repayment-all.png" : "return-all.png"));

                combo.SelectedItem = vm.Stores[1]; Layout(view);
                Check(vm.CanSubmit && !vm.IsAllStores, "Selecting a real store must restore the transaction command");
                if (!repayment)
                {
                    vm.SelectedArtikul = vm.ArtikulList.First(); vm.ConfirmSelectedArtikulCommand.Execute(null);
                    vm.OrderQuantity = 2; vm.ApplyQuantityCommand.Execute(null);
                }
                var before = vm.Products.ToArray(); var quantities = before.Select(x => x.Quantity).ToArray();
                Wait(vm.ShowCommand.ExecuteAsync(null));
                var single = Text(vm.LastPdf);
                Check(data.LastStore == "SHOP-A" && data.LastType == expectedType && single.Contains("Операция для магазина: SHOP-A") &&
                    !single.Contains("SHOP-B") && !single.Contains("Магазины: Все"), "Single-store preview included another store");
                Check(vm.Products.SequenceEqual(before) && vm.Products.Select(x => x.Quantity).SequenceEqual(quantities),
                    "Preview must not replace or clear prepared transaction quantities");
                RenderPdf(vm.LastPdf, Path.Combine(Output, repayment ? "repayment-single.png" : "return-single.png"));
            }
            // A delayed All response must not open after switching back to a specific store.
            vm.SelectedStore = vm.Stores[0];
            var delayed = new TaskCompletionSource<IEnumerable<ExchangeProductDto>>(); data.Next = delayed.Task;
            var showing = vm.ShowCommand.ExecuteAsync(null); var previews = vm.Previews;
            vm.SelectedStore = vm.Stores[1]; delayed.SetResult(ExchangeData.Rows);
            Wait(showing); Layout(view);
            Check(vm.Previews == previews && vm.SelectedStore?.StoreCode == "SHOP-A", "A stale All response opened after changing the store");

            var many = Enumerable.Range(0, 190).Select(i => new TransferReportLine
            {
                StoreCode = i < 110 ? "SHOP-A" : "SHOP-B", StoreName = i < 110 ? "Альфа" : "Бета",
                Article = $"PART-{i:0000}", ProductName = "Масло моторное", Brand = "BOSCH", Marka = "Универсальный",
                Model = "MODEL", Quantity = 1, WarehousePlace = "А1/2"
            }).Reverse().ToList();
            var path = Path.Combine(Output, "return-all-multipage.pdf");
            new TransferReportDocument("Мой магазин", DateTime.Today, "получение_возврата", many, "", "view", "Все", true).GeneratePdf(path);
            using (var pdf = PdfiumViewer.PdfDocument.Load(path))
            {
                Check(pdf.PageCount > 2, "Multipage fixture did not span multiple pages");
                for (var page = 0; page < pdf.PageCount; page++)
                {
                    var text = pdf.GetPdfText(page);
                    if (text.Contains("PART-")) Check(text.Contains("Магазин: SHOP-") && text.Contains("Артикул"),
                        "A continued store table lost its store or column headers");
                }
            }
            Console.WriteLine("PASS: return/repayment All and single-store previews, store/article grouping, duplicate articles across stores, transaction guard, preserved edits, stale selection and repeated multipage store headers");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static string Output { get { var path = Path.Combine(AppContext.BaseDirectory, "return-debt-artifacts"); Directory.CreateDirectory(path); return path; } }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Wait(Task task)
    {
        while (!task.IsCompleted) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        task.GetAwaiter().GetResult();
    }
    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(1450, 850)); view.Arrange(new Rect(0, 0, 1450, 850)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static string Text(string path)
    {
        using var pdf = PdfiumViewer.PdfDocument.Load(path);
        return string.Join(" ", Enumerable.Range(0, pdf.PageCount).Select(pdf.GetPdfText)).Replace("\r", "").Replace("\n", " ");
    }
    private static void RenderPdf(string path, string output)
    {
        using var pdf = PdfiumViewer.PdfDocument.Load(path);
        using var image = pdf.Render(0, 1240, 1754, 150, 150, PdfiumViewer.PdfRenderFlags.Annotations);
        image.Save(output, System.Drawing.Imaging.ImageFormat.Png);
    }
    private sealed class Stores() : StoreService(null!)
    {
        public override Task<List<StoreEntity>> GetAllAsync() => Task.FromResult<List<StoreEntity>>([
            new() { StoreCode = "SHOP-B", Name = "Бета" }, new() { StoreCode = "SHOP-A", Name = "Альфа" }]);
    }
    private sealed class ExchangeData() : StoreExchangeService(null!, null!)
    {
        public string? LastStore; public string? LastType; public Task<IEnumerable<ExchangeProductDto>>? Next;
        public static ExchangeProductDto[] Rows => [Row("SHOP-B", "Z-300", 9), Row("SHOP-A", "0002", 7),
            Row("SHOP-B", "0001", 4), Row("SHOP-A", "0001", 3)];
        private static ExchangeProductDto Row(string store, string article, int debt) => new()
        {
            StoreCode = store, ArticleNumber = article, ProductName = "Масло моторное", BrandName = "BOSCH",
            Marka = "Универсальный", Model = "MODEL", Debt = debt, Quantity = store == "SHOP-B" ? 0 : 20, WarehousePlace = "А1/2"
        };
        public override Task<IEnumerable<ExchangeProductDto>> GetExchangeProductsAsync(string? store, string type)
        {
            LastStore = store; LastType = type;
            var next = Next; Next = null;
            return next ?? Task.FromResult(Rows.Where(x => store == null || x.StoreCode == store));
        }
    }
    private sealed class Probe(ExchangeData data) : ReturnDebtRepaymentViewModel(data, new Stores(), null!, null!, null!)
    {
        public string LastPdf = "", Message = ""; public int Previews;
        protected override Task<string?> GetReportShopDisplayAsync() => Task.FromResult<string?>("Мой магазин");
        protected override void ShowReportMessage(string message) => Message = message;
        protected override void OpenReportPreview(TransferReportDocument document)
        {
            LastPdf = Path.Combine(Output, $"preview-{++Previews}.pdf"); document.GeneratePdf(LastPdf);
        }
    }
}
