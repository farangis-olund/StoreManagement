using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Infrastructure.Dtos;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class ReturnSearchUiTests
{
    public static void Run()
    {
        var orders = new Orders();
        var vm = new ReturnViewModel(null!, null!, orders, null!, null!, null!, null!, null!, new PermissionService());
        var first = new ReturnViewModel.CustomerLite { Id = "N0001", FullName = "Первый клиент" };
        var second = new ReturnViewModel.CustomerLite { Id = "N0002", FullName = "Второй клиент", PriceLevelCode = 2 };
        vm.CustomersFiltered.Add(first); vm.CustomersFiltered.Add(second);
        var view = new ReturnView { DataContext = vm }; Layout(view);
        var customers = (ComboBox)view.FindName("ReturnCustomerComboBox");
        var search = (TextBox)view.FindName("ReturnSearchBox");
        var results = (DataGrid)view.FindName("ReturnSearchResultsGrid");
        var lines = (DataGrid)view.FindName("ReturnLinesGrid");
        Check(!search.IsEnabled && !vm.CanSearchOrders && orders.Calls == 0 && vm.SearchResultsView.IsEmpty,
            "Returns must not allow search or load any orders before a customer is selected.");
        vm.LoadOrdersCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(orders.Calls == 0, "An explicit reload without a customer must not query orders.");
        customers.SelectedItem = first;
        search.Text = "170 451"; Layout(view);
        Check(search.IsEnabled && orders.CustomerId == first.Id && vm.SearchResults.All(r => r.CustomerId == first.Id),
            "Return search must query and display only the selected customer's purchases.");
        var hit = vm.SearchResultsView.Cast<ReturnViewModel.SearchHitRow>().Single();
        Check(hit.Article == "170 451" && hit.Quantity == 4,
            "An exact spaced article must take priority over separate keyword matches and respect already-returned quantities.");
        Check(vm.SearchResults.All(r => r.Article != "RETURNED"), "A fully returned purchase must not be offered again.");
        search.Text = "амортизатор SACHS"; Layout(view);
        Check(vm.SearchResultsView.Cast<ReturnViewModel.SearchHitRow>().Single() == hit,
            "Multiword name/brand search must remain available alongside article search.");
        vm.UseSearchHitCommand.Execute(hit); Layout(view);
        Check(vm.Lines.Single().CustomerId == first.Id && !customers.IsEnabled && !vm.CanSelectCustomer,
            "Selecting a result must add that customer's purchase and lock the customer while return rows exist.");
        search.Text = "Фильтр"; Layout(view);
        Check(results.Items.Count == 200 && Math.Abs(results.ActualHeight - 220) < 1,
            "A large search result must keep its fixed height and use scrolling.");
        var linesTop = lines.TransformToAncestor(view).Transform(new Point()).Y;
        Check(lines.ActualHeight >= 120 && linesTop >= results.TransformToAncestor(view).Transform(new Point()).Y + results.ActualHeight &&
            linesTop + lines.ActualHeight <= view.ActualHeight + 1,
            "The selected return rows must stay visible below the bounded result grid.");
        vm.SelectedCustomer = second;
        Check(vm.SelectedCustomer == first && vm.Lines.Count == 1, "A customer switch must not mix or silently discard pending returns.");
        vm.UseSearchHitCommand.Execute(new ReturnViewModel.SearchHitRow { CustomerId = second.Id, Article = "FOREIGN" });
        Check(vm.Lines.Count == 1, "A stale or foreign-customer result must not be added to the return.");
        vm.RemoveLineCommand.Execute(vm.Lines.Single()); Layout(view);
        Check(customers.IsEnabled, "Removing the last return row must unlock the customer picker.");
        customers.SelectedItem = second; search.Text = "170 451"; Layout(view);
        Check(vm.SearchResultsView.Cast<ReturnViewModel.SearchHitRow>().Single().CustomerId == second.Id && orders.CustomerId == second.Id,
            "Changing customer must reload the new customer's purchases using the same article search.");
        vm.SelectedCustomer = null; Layout(view);
        Check(!search.IsEnabled && vm.SearchResults.Count == 0 && !vm.SearchExpanded, "Clearing customer must clear results and disable search.");

        var delayed = new TaskCompletionSource<IReadOnlyList<OrderRowDto>>();
        orders.Next = delayed;
        vm.SelectedCustomer = first;
        vm.SelectedCustomer = second;
        delayed.SetResult(orders.Rows.Where(r => r.CustomerId == first.Id).ToArray()); Layout(view);
        Check(vm.SearchResults.Count > 0 && vm.SearchResults.All(r => r.CustomerId == second.Id),
            "A delayed previous-customer response must not replace the current customer's results.");
        vm.SearchFrom = new DateTime(2026, 11, 1); vm.SearchTo = new DateTime(2026, 11, 12);
        Check(orders.CustomerId == second.Id && orders.From == vm.SearchFrom && orders.To == vm.SearchTo,
            "Date filters must retain the selected customer in the server query.");
        vm.ManualMode = true;
        vm.SelectedManualProduct = new ProductPickItem { Article = "MANUAL", ProductName = "Ручной возврат", Quantity = 10, PriceLevel1 = 5, PriceLevel2 = 7 };
        vm.AddManualLineCommand.Execute(null);
        Check(vm.Lines.Single().CustomerId == second.Id && vm.Subtotal == 7,
            "Manual returns must continue to use the shared customer selector and customer-level price.");
        Console.WriteLine("PASS: customer-gated return search, server customer parameter, exact spaced article and keywords, stale result rejection, fixed-height scrolling, visible return rows and manual-mode compatibility");
    }

    private sealed class Orders() : OrderService(null!, null!, NullLogger<OrderService>.Instance, null!, null!, null!)
    {
        public int Calls;
        public string? CustomerId;
        public DateTime? From, To;
        public TaskCompletionSource<IReadOnlyList<OrderRowDto>>? Next;
        public List<OrderRowDto> Rows = Enumerable.Range(1, 200).Select(i => Row("N0001", $"FILTER-{i}", "Фильтр масляный", "BOSCH")).Concat(
        [
            Row("N0001", "170 451", "Амортизатор задний", "SACHS", 1),
            Row("N0001", "170", "Шайба 451", "Other"),
            Row("N0002", "170 451", "Амортизатор задний", "SACHS"),
            Row("N0001", "RETURNED", "Полностью возвращён", "Other", 5)
        ]).ToList();
        private static OrderRowDto Row(string customer, string article, string name, string brand, int returned = 0) => new()
        {
            CustomerId = customer, CustomerName = customer, Article = article, ProductName = name, Brand = brand,
            OrderId = "СЧ-TEST12", Date = new(2026, 11, 10), Quantity = 5, ReturnedQty = returned, Price = 10
        };
        public override Task<IReadOnlyList<OrderRowDto>> GetOrdersInRangeAsync(DateTime? from, DateTime? to,
            CancellationToken ct = default, string? customerId = null)
        {
            Calls++; CustomerId = customerId; From = from; To = to;
            var delay = Next; Next = null;
            return delay?.Task ?? Task.FromResult<IReadOnlyList<OrderRowDto>>(Rows);
        }
    }
    private static void Layout(FrameworkElement view)
    {
        view.Width = 1280; view.Height = 850;
        view.Measure(new Size(1280, 850)); view.Arrange(new Rect(0, 0, 1280, 850)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
