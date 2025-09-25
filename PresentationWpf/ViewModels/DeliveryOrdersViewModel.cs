using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dtos;
using Infrastructure.Repositories;
using Infrastructure.Services;
using PresentationWpf.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class DeliveryOrdersViewModel : ObservableObject
{
    private readonly OrderService _orderService;
    private readonly CourierRepository _courierRepo;
    private readonly UserSessionService _userSession;

    public DeliveryOrdersViewModel(
        OrderService orderService,
        CourierRepository courierRepo,
        UserSessionService userSession)
    {
        _orderService = orderService;
        _courierRepo = courierRepo;
        _userSession = userSession;

        DeliverySessions = [];
        OrdersForSelectedDate = new ObservableCollection<DeliveryOrderModel>();
        Couriers = new ObservableCollection<Courier>();
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
                HasUnsavedChanges = true;   // 🚨 Mark dirty
                RecalculateTotals();
            }
        };
    }

    public async Task InitializeAsync()
    {
        // Load couriers
        var courierEntities = await _courierRepo.GetAllAsync();
        Couriers = new ObservableCollection<Courier>(
            courierEntities.Select(e => (Courier)e));

        // Default exchange rate
        var rate = _userSession.ExchangeRate;
        ExchangeRate = rate > 0 ? rate : 1;
    }

    // 🔹 When courier changes, reload sessions
    partial void OnSelectedCourierChanged(Courier? value)
    {
        if (value != null)
            _ = LoadSessionsForCourierAsync(value.Id);
    }

    // 🔹 When session changes, show orders
    partial void OnSelectedSessionChanged(DeliverySessionModel? value)
    {
        OrdersForSelectedDate.Clear();
        if (value != null)
        {
            foreach (var o in value.Orders)
                OrdersForSelectedDate.Add(o);
        }
    }

    private async Task LoadSessionsForCourierAsync(string courierId)
    {
        DeliverySessions.Clear();

        // Get orders by courier
        var allOrders = await _orderService.GetOrdersByCourierAsync(courierId);

        // Only unpaid (optional)
        var unpaid = allOrders.Where(o => !o.IsPaid).ToList();

        foreach (var group in unpaid.GroupBy(o => o.Date.Date))
        {
            var session = new DeliverySessionModel
            {
                Date = group.Key,
                Orders = new ObservableCollection<DeliveryOrderModel>(
                    group.Select(e =>
                    {
                        var order = new DeliveryOrderModel
                        {
                            Date = e.Date,
                            Number = e.OrderId,
                            CustomerId = e.CustomerId,
                            FullName = e.FullName,
                            Address = e.Address,
                            Invoice = e.OrderId,

                            // 🚩 Sale in base currency (EUR)
                            Sale = e.SaleAmount,

                            // 🚩 Converted to local currency (сом)
                            Amount = e.SaleAmount * (decimal)ExchangeRate,

                            Payment = e.PaymentAmount,
                            IsPaid = e.IsPaid,
                            ExchangeRate = (decimal)ExchangeRate,

                            // ✅ Save originals for rollback
                            OriginalPayment = e.PaymentAmount,
                            OriginalIsPaid = e.IsPaid
                        };

                        AttachOrderEvents(order); // ✅ hook property change tracking
                        return order;
                    }))
            };

            DeliverySessions.Add(session);
        }

        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        TotalAmount = DeliverySessions.Sum(s => s.TotalAmount);
    }
        
    [RelayCommand]
    private async Task UpdateAsync()
    {
        foreach (var session in DeliverySessions)
        {
            foreach (var order in session.Orders.Where(o => o.IsPaid))
            {
                await _orderService.UpdatePaymentStatusAsync(order.Number, order.IsPaid);
            }
        }

        HasUnsavedChanges = false; // ✅ now safe to close
        MessageBox.Show("Изменения успешно сохранены.", "Обновление");
        if (SelectedCourier == null)
            return;
        await LoadSessionsForCourierAsync(SelectedCourier.Id);
    }

    [RelayCommand]
    private void Print()
    {
        // TODO: Hook your reporting/printing service
        MessageBox.Show("Печать отчета по заказам доставщика...");
    }
}

public class DeliverySessionModel
{
    public DateTime Date { get; set; }
    public ObservableCollection<DeliveryOrderModel> Orders { get; set; } = new();

    public int OrdersCount => Orders.Count;
    public decimal TotalAmount => Orders.Sum(o => o.PaymentInSom);
}

public partial class DeliveryOrderModel : ObservableObject
{
    public DateTime Date { get; set; }
    public string Number { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Address { get; set; } = "";
    public string Invoice { get; set; } = "";

    public decimal Sale { get; set; }
    public decimal Amount { get; set; }
    public decimal Payment { get; set; }

    [ObservableProperty] private bool isPaid;
    public decimal OriginalPayment { get; set; }
    public bool OriginalIsPaid { get; set; }


    public decimal ExchangeRate { get; set; }   

    public decimal PaymentInSom => IsPaid ? Payment * ExchangeRate : 0;
}
