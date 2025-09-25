using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Services;
using Infrastructure.Dtos;
using System.Collections.ObjectModel;
using System.Windows;
using Infrastructure.Repositories;

namespace PresentationWpf.ViewModels;
public partial class AssignPickersViewModel : ObservableObject
{
    private readonly OrderService _orderService;
    private readonly StorekeeperRepository _storekeeperRepo;

    public AssignPickersViewModel(OrderService orderService, StorekeeperRepository storekeeperRepo)
    {
        _orderService = orderService;
        _storekeeperRepo = storekeeperRepo;
        _ = InitializeAsync();
    }

    [ObservableProperty]
    private ObservableCollection<AssignPickerDto> _orders = [];

    [ObservableProperty]
    private ObservableCollection<string> _orderIds = [];

    [ObservableProperty]
    private string? _selectedOrderId;

    [ObservableProperty]
    private ObservableCollection<Storekeeper> _storekeeperList = [];

    [ObservableProperty]
    private ObservableCollection<AssignPickerDto> _filteredOrders = [];

    [RelayCommand]
    public async Task InitializeAsync()
    {
        var today = DateTime.Today;
        var orders = await _orderService.GetOrdersInRangeForPickersAsync(today, today.AddDays(1));

        Orders = new ObservableCollection<AssignPickerDto>(orders);
        OrderIds = new ObservableCollection<string>(orders.Select(o => o.OrderId));

        var storekeepers = await _storekeeperRepo.GetAllAsync();
        StorekeeperList = new ObservableCollection<Storekeeper>(
            storekeepers.Select(e => new Storekeeper
            {
                Id = e.Id,
                FullName = e.FullName
            }));

        ApplyFilter();
    }

    // whenever SelectedOrderId changes → filter
    partial void OnSelectedOrderIdChanged(string? value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrEmpty(SelectedOrderId))
        {
            FilteredOrders = new ObservableCollection<AssignPickerDto>(Orders);
        }
        else
        {
            FilteredOrders = new ObservableCollection<AssignPickerDto>(
                Orders.Where(o => o.OrderId == SelectedOrderId));
        }
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        foreach (var order in Orders)
        {
            await _orderService.UpdateAssignPickerAsync(order.OrderId, order.PickerId);
        }

        MessageBox.Show("Изменения успешно сохранены.", "Обновление");
        await InitializeAsync();
    }
}
