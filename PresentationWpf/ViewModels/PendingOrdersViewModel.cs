using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Constants;
using Infrastructure.Dtos;
using Infrastructure.Services;
using PresentationWpf.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class PendingOrdersViewModel : ObservableObject
{
    private readonly OrderService _orderService;
    private readonly PermissionService _permissions;
    private readonly UserSessionService _session;
    private int _loadVersion;

    public PendingOrdersViewModel(OrderService orderService, PermissionService permissions, UserSessionService session)
    {
        _orderService = orderService;
        _permissions = permissions;
        _session = session;
        PropertyChangedEventManager.AddHandler(permissions, OnPermissionsChanged, nameof(PermissionService.CurrentPermissions));
        WeakEventManager<UserSessionService, EventArgs>.AddHandler(session, nameof(UserSessionService.UserChanged), OnUserChanged);
        _ = RefreshAsync();
    }

    public ObservableCollection<PendingOrderDto> PendingOrders { get; } = [];
    [ObservableProperty] private string? managerId;
    [ObservableProperty] private PendingOrderDto? selectedOrder;
    [ObservableProperty] private string message = "";

    private void OnPermissionsChanged(object? sender, PropertyChangedEventArgs e) => _ = RefreshAsync();
    private void OnUserChanged(object? sender, EventArgs e)
    {
        if (ManagerId != null) ManagerId = null;
        else _ = RefreshAsync();
    }

    partial void OnManagerIdChanged(string? value) => _ = RefreshAsync();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var version = ++_loadVersion;
        var userId = _session.UserId;
        var allManagers = _permissions.Has(PermissionCatalog.OrdersViewAllManagers);
        var selectedManagerId = allManagers ? ManagerId : null;
        PendingOrders.Clear();
        SelectedOrder = null;
        Message = "";
        if (!_session.IsLoggedIn || !_permissions.Has(PermissionCatalog.Orders)) return;

        try
        {
            var orders = await _orderService.GetUnsentOrdersAsync(selectedManagerId);
            if (version != _loadVersion || !_session.IsLoggedIn || _session.UserId != userId ||
                !_permissions.Has(PermissionCatalog.Orders) ||
                allManagers != _permissions.Has(PermissionCatalog.OrdersViewAllManagers)) return;
            foreach (var order in orders) PendingOrders.Add(order);
            Message = PendingOrders.Count == 0 ? "Нет заказов в ожидании отправки." : "";
        }
        catch (Exception ex)
        {
            if (version == _loadVersion) Message = ex is UnauthorizedAccessException ? ex.Message :
                "Не удалось загрузить заказы. Повторно откройте список.";
        }
    }
}
