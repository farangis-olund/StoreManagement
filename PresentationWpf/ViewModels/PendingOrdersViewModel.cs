using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class PendingOrdersViewModel : ObservableObject
{
    private readonly OrderService _orderService;

    public PendingOrdersViewModel(OrderService orderService)
    {
        _orderService = orderService;
        PendingOrders = [];
        _ = RefreshAsync();
    }

    [ObservableProperty]
    private ObservableCollection<PendingOrderModel> pendingOrders;

    [ObservableProperty]
    private PendingOrderModel? selectedOrder;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        PendingOrders.Clear();
        var orders = await _orderService.GetUnsentOrdersAsync();

        foreach (var o in orders)
        {
            PendingOrders.Add(new PendingOrderModel
            {
                Invoice = o.Invoice,
                InvoiceNumber = o.InvoiceNumber,
                Date = o.Date,
                CustomerId = o.CustomerId,
                IsSent = o.IsSent
            });
        }
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        // only update orders where user set IsSent = true
        var updatedOrders = PendingOrders.Where(o => o.IsSent).ToList();

        if (updatedOrders.Count == 0)
        {
            MessageBox.Show("Нет изменений для сохранения.", "Обновление");
            return;
        }

        foreach (var order in updatedOrders)
        {
            await _orderService.UpdateSentStatusAsync(order.InvoiceNumber, true);
        }

        MessageBox.Show("Изменения успешно сохранены.", "Обновление");

        // Refresh list after save (already sorted by date desc)
        await RefreshAsync();
    }

}

public class PendingOrderModel : ObservableObject
{
    public string Invoice { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string CustomerId { get; set; } = string.Empty;

    private bool _isSent;
    public bool IsSent
    {
        get => _isSent;
        set => SetProperty(ref _isSent, value);
    }
}
