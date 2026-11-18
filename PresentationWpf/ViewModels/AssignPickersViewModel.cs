using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Services;
using Infrastructure.Dtos;
using System.Collections.ObjectModel;
using System.Windows;
using Infrastructure.Repositories;
using Infrastructure.Constants;
using Infrastructure.Security;
using PresentationWpf.Services;
using System.ComponentModel;

namespace PresentationWpf.ViewModels;
public partial class AssignPickersViewModel : ObservableObject
{
    private readonly OrderService _orderService;
    private readonly StorekeeperRepository _storekeeperRepo;
    private readonly OrderOperationsAccessPolicy _access;
    private readonly UserSessionService _session;
    private int _loadVersion;

    public AssignPickersViewModel(OrderService orderService, StorekeeperRepository storekeeperRepo,
        PermissionService permissions, UserSessionService session)
    {
        _orderService = orderService;
        _storekeeperRepo = storekeeperRepo;
        _access = new(permissions);
        _session = session;
        PropertyChangedEventManager.AddHandler(permissions, OnPermissionsChanged, nameof(PermissionService.CurrentPermissions));
        WeakEventManager<UserSessionService, EventArgs>.AddHandler(session, nameof(UserSessionService.UserChanged), OnUserChanged);
        _ = InitializeAsync();
    }

    public bool CanAccess => _session.IsLoggedIn && _access.Can(PermissionCatalog.OrdersAssignPickers);
    public bool IsReadOnly => !CanAccess;
    [ObservableProperty] private string message = "";
    private void OnPermissionsChanged(object? sender, PropertyChangedEventArgs e) => ResetAccess();
    private void OnUserChanged(object? sender, EventArgs e) => ResetAccess();
    private void ResetAccess()
    {
        _loadVersion++;
        Orders.Clear(); OrderIds.Clear(); StorekeeperList.Clear(); FilteredOrders.Clear(); SelectedOrderId = null;
        OnPropertyChanged(nameof(CanAccess)); OnPropertyChanged(nameof(IsReadOnly));
        UpdateCommand.NotifyCanExecuteChanged(); InitializeCommand.NotifyCanExecuteChanged();
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

    [RelayCommand(CanExecute = nameof(CanAccess))]
    public async Task InitializeAsync()
    {
        var version = ++_loadVersion;
        var userId = _session.UserId;
        Message = CanAccess ? "" : "Нет права на назначение комплектовщиков.";
        if (!CanAccess) return;
        try
        {
            var today = DateTime.Today;
            var orders = await _orderService.GetOrdersInRangeForPickersAsync(today, today.AddDays(1));
            if (version != _loadVersion || userId != _session.UserId || !CanAccess) return;

            var storekeepers = await _storekeeperRepo.GetAllAsync();
            if (version != _loadVersion || userId != _session.UserId || !CanAccess) return;
            Orders = new ObservableCollection<AssignPickerDto>(orders);
            OrderIds = new ObservableCollection<string>(orders.Select(o => o.OrderId));
            StorekeeperList = new ObservableCollection<Storekeeper>(
                storekeepers.Select(e => new Storekeeper
                {
                    Id = e.Id,
                    FullName = e.FullName
                }));

            ApplyFilter();
        }
        catch (Exception ex)
        {
            if (version == _loadVersion) Message = ex is UnauthorizedAccessException ? ex.Message : "Не удалось загрузить назначения комплектовщиков.";
        }
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

    [RelayCommand(CanExecute = nameof(CanAccess))]
    private async Task UpdateAsync()
    {
        if (!CanAccess) return;
        var userId = _session.UserId;
        try
        {
            foreach (var order in Orders.ToList())
            {
                if (!CanAccess || userId != _session.UserId) return;
                if (!await _orderService.UpdateAssignPickerAsync(order.OrderId, order.PickerId))
                {
                    Message = "Не удалось сохранить назначение комплектовщика.";
                    return;
                }
            }
        }
        catch (Exception ex) { Message = ex is UnauthorizedAccessException ? ex.Message : "Не удалось сохранить назначения комплектовщиков."; return; }
        if (!CanAccess || userId != _session.UserId) return;
        MessageBox.Show("Изменения успешно сохранены.", "Обновление");
        await InitializeAsync();
    }
}
