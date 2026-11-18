using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Repositories;
using Infrastructure.Services;
using PresentationWpf.Documents;
using PresentationWpf.Services;
using PresentationWpf.Views;
using QuestPDF.Fluent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class DeliveryOrdersViewModel : ObservableObject
{
    private readonly OrderService _orderService;
    private readonly CourierRepository _courierRepo;
    private readonly UserSessionService _userSession;
    private int _deliveryLoadVersion;

    public DeliveryOrdersViewModel(
        OrderService orderService,
        CourierRepository courierRepo,
        UserSessionService userSession,
        PermissionService permissions)
    {
        _orderService = orderService;
        _courierRepo = courierRepo;
        _userSession = userSession;

        DeliverySessions = [];
        OrdersForSelectedDate = new ObservableCollection<DeliveryOrderModel>();
        Couriers = new ObservableCollection<Courier>();
        InitializePermissions(permissions);
        _ = InitializeAsync();
    }

    // 🔹 Courier list + selected
    [ObservableProperty] private ObservableCollection<Courier> couriers;
    [ObservableProperty] private Courier? selectedCourier;

    // 🔹 Sessions (dates grouped)
    [ObservableProperty] private ObservableCollection<DeliverySessionModel> deliverySessions;

    [ObservableProperty] private DeliverySessionModel? selectedSession;

    // 🔹 Orders for one date
    [ObservableProperty] private ObservableCollection<DeliveryOrderModel> ordersForSelectedDate;

    // 🔹 Exchange rate + total sum
    [ObservableProperty] private double exchangeRate = 1;

    [ObservableProperty] private double totalSum;

    [ObservableProperty] private double totalSale;
    [ObservableProperty] private double totalPayment;

    private decimal _totalAmount;
    public decimal TotalAmount
    {
        get => _totalAmount;
        set => SetProperty(ref _totalAmount, value);
    }

    private bool _hasUnsavedChanges;

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set => SetProperty(ref _hasUnsavedChanges, value);
    }

    private void AttachOrderEvents(DeliveryOrderModel order)
    {
        order.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(order.IsPaid) or nameof(order.Payment))
            {
                if (!CanUpdate)
                {
                    order.IsPaid = order.OriginalIsPaid;
                    return;
                }
                HasUnsavedChanges = true;
                RecalculateTotals();
            }
        };
    }


    public async Task InitializeAsync()
    {
        if (!CanView) return;
        var version = _accessVersion;
        var userId = _userSession.UserId;
        try
        {
            // Load couriers
            var courierEntities = await _courierRepo.GetAllAsync();
            if (version != _accessVersion || userId != _userSession.UserId || !CanView) return;
            Couriers = new ObservableCollection<Courier>(
                courierEntities.Select(e => (Courier)e));

            // Default exchange rate
            var rate = _userSession.ExchangeRate;
            ExchangeRate = rate > 0 ? rate : 1;
        }
        catch (Exception)
        {
            if (version == _accessVersion) Message = "Не удалось загрузить доставщиков. Повторно откройте доставку заказов.";
        }
    }

    // 🔹 When courier changes, reload sessions
    partial void OnSelectedCourierChanged(Courier? value)
    {
        if (CanView && value != null)
            _ = LoadSessionsForCourierAsync(value.Id);
    }

    // 🔹 When session changes, show orders
    partial void OnSelectedSessionChanged(DeliverySessionModel? value)
    {
        OrdersForSelectedDate.Clear();

        if (value is null || !CanView)
            return;

        foreach (var o in value.Orders)
        {
            OrdersForSelectedDate.Add(o);
        }

        // set exchange rate
        if (value.Orders.Any())
            ExchangeRate = (double)value.Orders.First().ExchangeRate;
        else
            ExchangeRate = 1;

        // initial recalculation
        RecalculateTotals();
    }



    private async Task LoadSessionsForCourierAsync(string courierId)
    {
        var version = ++_deliveryLoadVersion;
        DeliverySessions.Clear();
        SelectedSession = null;
        OrdersForSelectedDate.Clear();
        if (!CanView) return;
        var userId = _userSession.UserId;

        try
        {
            // 🔹 Load all orders for this courier
            var allOrders = await _orderService.GetOrdersByCourierAsync(courierId);
            if (version != _deliveryLoadVersion || SelectedCourier?.Id != courierId || userId != _userSession.UserId || !CanView) return;

            // 🔹 Only unpaid orders
            var unpaid = allOrders.Where(o => !o.IsPaid).ToList();

            // 🔹 Group by order date
            foreach (var group in unpaid.GroupBy(o => o.Date.Date))
            {
                var sessionDate = group.Key;

                // 📦 Create the delivery session
                var session = new DeliverySessionModel
                {
                    Date = sessionDate,
                    Orders = new ObservableCollection<DeliveryOrderModel>()
                };

                // 🔹 Loop through each order in this date group
                foreach (var e in group)
                {
                    // 💱 Get the exchange rate stored in Orders table for this order
                    var dailyRate = e.Rate;

                    var order = new DeliveryOrderModel
                    {
                        Date = e.Date,
                        Number = e.OrderId,
                        PaymentId = e.PaymentId,
                        ReceiptNumber = e.ReceiptNumber,
                        CustomerId = e.CustomerId,
                        FullName = e.FullName,
                        Address = e.Address,
                        City = e.City,
                        Invoice = e.OrderId,
                        Sale = e.SaleAmount,
                        Phone = e.Phone,

                        // ✅ Use the actual rate stored in Orders table
                        Amount = e.PaymentAmount * dailyRate,
                        ExchangeRate = dailyRate,

                        Payment = e.PaymentAmount,
                        IsPaid = e.IsPaid,
                        OriginalPayment = e.PaymentAmount,
                        OriginalIsPaid = e.IsPaid
                    };

                    AttachOrderEvents(order);
                    session.Orders.Add(order);
                }

                DeliverySessions.Add(session);
            }

            SelectedSession = DeliverySessions.FirstOrDefault();
            HasUnsavedChanges = false;
            RecalculateTotals();
        }
        catch (Exception)
        {
            if (version == _deliveryLoadVersion) Message = "Не удалось загрузить доставку. Повторно откройте список.";
        }
    }
    private void RecalculateTotals()
    {
        // bottom totals (across all sessions)
        TotalSale = (double)DeliverySessions.Sum(s => s.TotalSales);
        TotalPayment = (double)DeliverySessions.Sum(s => s.TotalPayments);

        // top total (only for visible orders in current session)
        if (OrdersForSelectedDate != null && OrdersForSelectedDate.Any())
        {
            var sum = OrdersForSelectedDate
                .Where(o => o.IsPaid)
                .Sum(o => (double)(o.Payment * o.ExchangeRate));

            TotalSum = Math.Round(sum, 2);
        }
        else
        {
            TotalSum = 0;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task UpdateAsync()
    {
        if (!CanUpdate || SelectedCourier == null)
            return;

        var courierId = SelectedCourier.Id;
        var userId = _userSession.UserId;
        try
        {
            foreach (var order in DeliverySessions.SelectMany(s => s.Orders).Where(o => o.IsPaid).ToList())
            {
                if (!CanUpdate || userId != _userSession.UserId || SelectedCourier?.Id != courierId) return;
                if (!await _orderService.ConfirmDeliveryAsync(courierId, order.Number, order.PaymentId))
                {
                    MessageBox.Show("Не удалось подтвердить оплату. Данные могли измениться; откройте список доставки снова.",
                        "Доставка заказов", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
        }
        catch (Exception ex) { Message = ex is UnauthorizedAccessException ? ex.Message : "Не удалось обновить доставку заказов."; return; }

        if (!CanUpdate || userId != _userSession.UserId || SelectedCourier?.Id != courierId) return;
        HasUnsavedChanges = false; // ✅ now safe to close
        MessageBox.Show("Изменения успешно сохранены.", "Обновление");
        if (SelectedCourier == null)
            return;
        await LoadSessionsForCourierAsync(SelectedCourier.Id);
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private void Print()
    {
        if (!CanPrint) return;
        if (SelectedCourier == null)
        {
            MessageBox.Show("Выберите доставщика!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SelectedSession == null || SelectedSession.Orders.Count == 0)
        {
            MessageBox.Show("Нет данных для печати!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var orgName = _userSession.OrganizationDisplayName;
        var courierName = SelectedCourier.FullName;
        var date = SelectedSession.Date;
        var rate = ExchangeRate;
        var invoiceNumber = SelectedSession.Orders.First().Invoice;

        var lines = SelectedSession.Orders.Select(o => new DeliveryNoteLine
        {
            CustomerId = o.CustomerId,
            City = o.City ?? "",
            Address = o.Address,
            CustomerName = o.FullName,
            Phone = o.Phone, 
            InvoiceNumber = o.Invoice,
            ReceiptNumber = o.ReceiptNumber
        }).ToList();

        var doc = new DeliveryNoteDocument(orgName, courierName, date, rate, invoiceNumber, lines);

        string folder = Path.Combine(Path.GetTempPath(), "Reports");
        Directory.CreateDirectory(folder);
        string filePath = Path.Combine(folder, $"DeliveryNote_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        doc.GeneratePdf(filePath);

        var preview = new DocumentPreviewView(filePath);
        var window = new Window
        {
            Title = "Транспортная накладная",
            Content = preview,
            Width = 900,
            Height = 1000,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        window.ShowDialog();
    }


    [RelayCommand(CanExecute = nameof(CanReport))]
    private void Report()
    {
        if (!CanReport) return;
        if (SelectedCourier == null)
        {
            MessageBox.Show("Выберите курьера!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var orgInfo = _userSession.OrganizationDisplayName;
        
        var courierName = SelectedCourier.FullName;

        // Combine all unpaid orders for this courier
        var allOrders = DeliverySessions
            .SelectMany(s => s.Orders)
            .Where(o => !o.IsPaid)
            .ToList();


        if (!allOrders.Any())
        {
            MessageBox.Show("У данного курьера нет неоплаченных заказов!", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var lines = allOrders.Select(o => new CourierDeliveryLine
        {
            CustomerId = o.CustomerId,
            FullName = o.FullName,
            Address = o.Address,
            City = o.City,
            Invoice = o.Invoice,
            ReceiptNumber = o.ReceiptNumber,
            Sale = o.Sale,
            Phone = o.Phone,
            Amount = o.Amount,
            Payment = o.Payment,
            Date = o.Date,
        }).ToList();

        var document = new CourierDeliveryReportDocument(
            orgInfo,
            courierName,
            lines,
            "Список неоплаченных доставок");

        string folder = Path.Combine(Path.GetTempPath(), "Reports");
        Directory.CreateDirectory(folder);
        string filePath = Path.Combine(folder, $"CourierUnpaid_{courierName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        document.GeneratePdf(filePath);

        var preview = new DocumentPreviewView(filePath);
        var window = new Window
        {
            Title = "Отчёт о неоплаченных доставках",
            Content = preview,
            Width = 900,
            Height = 1000,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        window.ShowDialog();
    }

}

public class DeliverySessionModel
{
    public DateTime Date { get; set; }
    public ObservableCollection<DeliveryOrderModel> Orders { get; set; } = new();

    public int OrdersCount => Orders.Count;
    public decimal TotalAmount => Orders.Sum(o => o.PaymentInSom);

    public decimal TotalSales => Orders?.Sum(o => o.Sale) ?? 0;
    public decimal TotalPayments => Orders?.Sum(o => o.Payment) ?? 0;
}

public partial class DeliveryOrderModel : ObservableObject
{
    public DateTime Date { get; set; }
    public string Number { get; set; } = "";
    public int? PaymentId { get; set; }
    public string ReceiptNumber { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string Invoice { get; set; } = "";

    public decimal Sale { get; set; }
    public decimal Amount { get; set; }
    public decimal Payment { get; set; }

    [ObservableProperty]
    private bool isPaid;

    public decimal OriginalPayment { get; set; }
    public bool OriginalIsPaid { get; set; }

    public decimal ExchangeRate { get; set; }

    public decimal PaymentInSom => IsPaid ? Payment * ExchangeRate : 0;

    // 🔹 When IsPaid changes, tell WPF that PaymentInSom changed AND trigger global event
    partial void OnIsPaidChanged(bool value)
    {
        OnPropertyChanged(nameof(PaymentInSom));
    }
}
