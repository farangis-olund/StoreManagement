using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Entities;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using PresentationWpf.Services;
using PresentationWpf.ViewModels;
using PresentationWpf.Views;

internal static class ReturnEntryUiTests
{
    public static void Run()
    {
        var returns = new Returns();
        var vm = new EntryViewModel(returns);
        var customer = new ReturnViewModel.CustomerLite { Id = "N0001", FullName = "Клиент проверки" };
        var reason = new ReturnReasonEntity { Id = 1, Name = "Не подошёл товар" };
        vm.CustomersFiltered.Add(customer); vm.ReturnReasons.Add(reason);
        var view = new ReturnView { DataContext = vm }; Layout(view);
        var customerInput = (ComboBox)view.FindName("ReturnCustomerComboBox");
        customerInput.SelectedItem = customer; vm.SearchText = "TEST"; Layout(view);
        customerInput.ApplyTemplate();
        var customerText = (TextBox)customerInput.Template.FindName("PART_EditableTextBox", customerInput);
        Check(customerInput.VerticalContentAlignment == VerticalAlignment.Center &&
            customerText.VerticalContentAlignment == VerticalAlignment.Center,
            "Both the customer selector and its editable textbox must center their text vertically.");

        vm.UseSearchHitCommand.Execute(vm.SearchResults.Single()); Layout(view);
        var row = vm.Lines.Single();
        var grid = (DataGrid)view.FindName("ReturnLinesGrid");
        var quantity = grid.Columns.OfType<DataGridTextColumn>().Single(c => (c.Binding as Binding)?.Path.Path == "ReturnQty");
        Check(!grid.CanUserAddRows && grid.Columns.Where(c => c != quantity).All(c => c.IsReadOnly),
            "Only the return quantity may be edited; users must not insert arbitrary blank return rows.");
        foreach (var column in grid.Columns.OfType<DataGridTextColumn>().Where(c => c != quantity))
        {
            grid.SelectedItem = row; grid.CurrentCell = new DataGridCellInfo(row, column);
            Check(!grid.BeginEdit(), "A product field or price unexpectedly allowed editing.");
        }
        grid.ScrollIntoView(row, quantity); Layout(view);
        grid.CurrentCell = new DataGridCellInfo(row, quantity);
        Check(grid.BeginEdit(), "The return quantity must remain editable."); Layout(view);
        var editor = (TextBox)quantity.GetCellContent(row);
        editor.Text = "3"; editor.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true); Layout(view);
        Check(row.ReturnQty == 3 && row.Price == 15 && row.Article == "TEST" && row.Name == "Тестовый товар" && vm.Subtotal == 45,
            "Editing quantity must recalculate the total without changing product identity or price.");

        var reasonInput = Descendants<ComboBox>(view).Single(c =>
            c.GetBindingExpression(ComboBox.SelectedItemProperty)?.ParentBinding.Path.Path == nameof(vm.SelectedReturnReason));
        var methodInput = Descendants<ComboBox>(view).Single(c =>
            c.GetBindingExpression(ComboBox.SelectedItemProperty)?.ParentBinding.Path.Path == nameof(vm.RefundMethod));
        Check(reasonInput.SelectedItem == null && methodInput.SelectedItem == null && string.IsNullOrWhiteSpace(vm.RefundMethod),
            "Reason and refund method must start without a hidden or automatic selection.");
        void Reject(string[] requiredWords)
        {
            var warnings = vm.Warnings.Count;
            vm.SubmitCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            Check(returns.Calls == 0 && vm.Warnings.Count == warnings + 1 && requiredWords.All(word =>
                vm.Warnings[^1].Contains(word, StringComparison.OrdinalIgnoreCase)),
                "Missing return options must show a precise warning and stop before the persistence service.");
        }
        Reject(["причину", "способ"]);
        reasonInput.SelectedItem = reason; Layout(view); Reject(["способ"]);
        reasonInput.SelectedItem = null; methodInput.SelectedItem = RefundMethodConstants.Cash; Layout(view); Reject(["причину"]);
        reasonInput.SelectedItem = reason; vm.RefundMethod = "cash"; Layout(view); Reject(["способ"]);
        vm.SelectedReturnReason = new ReturnReasonEntity { Id = reason.Id, Name = reason.Name };
        vm.RefundMethod = RefundMethodConstants.Cash; Layout(view); Reject(["причину"]);
        reasonInput.SelectedItem = reason; methodInput.SelectedItem = RefundMethodConstants.Cash; Layout(view);
        var warningCount = vm.Warnings.Count;
        var saving = vm.SubmitCommand.ExecuteAsync(null);
        Check(returns.Calls == 1 && !saving.IsCompleted && vm.Warnings.Count == warningCount &&
            returns.Captured?.CustomerId == customer.Id && returns.Captured.ReturnReasonId == reason.Id &&
            returns.Captured.RefundMethod == RefundMethodConstants.Cash && returns.Captured.TotalAmount == 45,
            "A return with both explicit choices must reach persistence with the selected values and protected prices.");

        var bitmap = new RenderTargetBitmap(1280, 850, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, "return-entry-preview.png")); png.Save(stream);
        Console.WriteLine("PASS: vertically centered customer text, quantity-only editing, recalculated totals, precise missing-option warnings, rejection of hidden/stale options and valid submission");
    }

    private sealed class EntryViewModel(ReturnService returns) : ReturnViewModel(null!, null!, new Orders(), returns,
        null!, null!, null!, null!, new PermissionService())
    {
        public List<string> Warnings { get; } = [];
        protected override void ShowReturnOptionsRequiredMessage(string message) => Warnings.Add(message);
    }
    private sealed class Returns() : ReturnService(null!, null!, NullLogger<ReturnService>.Instance)
    {
        public int Calls;
        public ReturnEntity? Captured;
        public override Task<ReturnEntity?> AddReturnAsync(ReturnEntity entity)
        {
            Calls++; Captured = entity;
            // Pause before the completion dialog; no real database or printing is used.
            return new TaskCompletionSource<ReturnEntity?>().Task;
        }
    }
    private sealed class Orders() : OrderService(null!, null!, NullLogger<OrderService>.Instance, null!, null!, null!)
    {
        public override Task<IReadOnlyList<OrderRowDto>> GetOrdersInRangeAsync(DateTime? from, DateTime? to,
            CancellationToken ct = default, string? customerId = null) => Task.FromResult<IReadOnlyList<OrderRowDto>>(
            [new() { CustomerId = customerId!, CustomerName = "Клиент проверки", Article = "TEST", ProductName = "Тестовый товар",
                Brand = "SACHS", Marka = "MERC", Model = "W201", OrderId = "СЧ-TEST12", Date = DateTime.Today, Quantity = 10, Price = 15 }]);
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
    private static void Layout(FrameworkElement view)
    {
        view.Width = 1280; view.Height = 850;
        view.Measure(new Size(1280, 850)); view.Arrange(new Rect(0, 0, 1280, 850)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
