using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class BarterHistoryUiTests
{
    public static void Run()
    {
        var orders = new SavedOrders();
        var vm = new CapturedSummary(orders) { CustomerId = "N0001", PaymentAmountText = "123.45" };
        var barter = new SummaryViewModel.OrderRow { InvoiceNo = orders.Order.Id, IsBarter = true, Amount = 86.25m };
        var ordinary = new SummaryViewModel.OrderRow { InvoiceNo = "СЧ-ABC123", Amount = 10 };
        vm.Orders.Add(barter);
        vm.Orders.Add(ordinary);
        vm.SelectedOrder = barter;
        Check(vm.PaymentAmountText == "0" && vm.PaymentOrderId == orders.Order.Id && orders.Queries.Count == 0,
            "Selecting a historical barter must not calculate or suggest another payment.");

        var view = new SummaryWindow { DataContext = vm };
        Layout(view);
        var grid = Descendants<DataGrid>(view).Single(g => ReferenceEquals(g.ItemsSource, vm.Orders));
        var column = grid.Columns.Single(c => Equals(c.Header, "№ Накладной"));
        Check(((TextBlock)column.GetCellContent(barter)).Text == orders.Order.Id + " — бартер" &&
            ((TextBlock)column.GetCellContent(ordinary)).Text == ordinary.InvoiceNo,
            "The actual invoice grid must distinguish barter while retaining ordinary invoice numbers.");

        for (var i = 0; i < 2; i++) vm.ViewInvoiceCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Check(vm.Previews.Count == 2 && orders.Queries.SequenceEqual([orders.Order.Id, orders.Order.Id]),
            "Repeated preview must look up the original invoice number, without its display marker.");
        Check(vm.Previews.All(o => o.IsBarter && o.Id == orders.Order.Id && o.CustomerCity == "Душанбе" &&
            o.UserFullName == "Тест Продавец" && o.OrderDetails.Single().Price == 17.25m &&
            o.OrderDetails.Single().Quentity == 5 && o.OrderDetails.Single().Total == 86.25m && o.OrderDetails.Single().WarehousePlace == "А1, А2") && !orders.Order.IsSent,
            "Barter reprints must retain the document type, seller, customer and persisted manual prices without changing the saved order.");
        view.DataContext = null;
        Console.WriteLine("PASS: customer invoice grid labels barter; repeated preview preserves the original ID, document type and manual prices without suggesting a duplicate payment");
    }

    private sealed class SavedOrders() : OrderService(null!, null!, NullLogger<OrderService>.Instance, null!, null!, null!)
    {
        public List<string> Queries { get; } = [];
        public OrderEntity Order { get; } = new()
        {
            Id = "БТ-ABC123", IsBarter = true, IsPaid = true, Date = DateTime.Today.AddDays(-5), Rate = 11,
            Customer = new() { Id = "N0001", FullName = "Клиент", City = "Душанбе" },
            User = new() { FirstName = "Тест", LastName = "Продавец" },
            OrderDetails = [new() { ArticleNumber = "PART", Price = 17.25m, Quentity = 5,
                WarehousePlaceSnapshot = "А1, А2",
                Product = new() { ProductName = "Товар", Brand = new() { BrandName = "Бренд" } } }]
        };
        public override Task<OrderEntity?> GetOrderAsync(string id)
        {
            Queries.Add(id);
            return Task.FromResult<OrderEntity?>(id == Order.Id ? Order : null);
        }
    }

    private sealed class CapturedSummary(OrderService orders) : SummaryViewModel(orders, null!, null!, null!, null!, null!)
    {
        public List<CustomerOrder> Previews { get; } = [];
        protected override Task PreviewInvoiceAsync(CustomerOrder order, string? territory, string? courierId, string? storekeeperId)
        {
            Previews.Add(order);
            return Task.CompletedTask;
        }
    }

    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(1400, 650));
        view.Arrange(new Rect(0, 0, 1400, 650));
        view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
