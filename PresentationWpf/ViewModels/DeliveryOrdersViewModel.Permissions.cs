using CommunityToolkit.Mvvm.ComponentModel;
using Infrastructure.Constants;
using Infrastructure.Security;
using PresentationWpf.Services;
using System.ComponentModel;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class DeliveryOrdersViewModel
{
    private OrderOperationsAccessPolicy _access = null!;
    private int _accessVersion;
    [ObservableProperty] private string message = "";

    public bool CanView => _userSession.IsLoggedIn && _access.Can(PermissionCatalog.OrdersDeliveryView);
    public bool CanUpdate => _userSession.IsLoggedIn && _access.Can(PermissionCatalog.OrdersDeliveryUpdate);
    public bool CanPrint => _userSession.IsLoggedIn && _access.Can(PermissionCatalog.OrdersDeliveryPrint);
    public bool CanReport => _userSession.IsLoggedIn && _access.Can(PermissionCatalog.OrdersDeliveryReport);
    public bool IsReadOnly => !CanUpdate;

    private void InitializePermissions(PermissionService permissions)
    {
        _access = new(permissions);
        PropertyChangedEventManager.AddHandler(permissions, OnDeliveryPermissionsChanged, nameof(PermissionService.CurrentPermissions));
        WeakEventManager<UserSessionService, EventArgs>.AddHandler(_userSession, nameof(UserSessionService.UserChanged), OnDeliveryUserChanged);
        Message = CanView ? "" : "Нет права на просмотр доставки заказов.";
    }

    private void OnDeliveryPermissionsChanged(object? sender, PropertyChangedEventArgs e) => ResetDeliveryAccess();
    private void OnDeliveryUserChanged(object? sender, EventArgs e) => ResetDeliveryAccess();

    private void ResetDeliveryAccess()
    {
        _accessVersion++; _deliveryLoadVersion++;
        SelectedCourier = null; SelectedSession = null;
        Couriers.Clear(); DeliverySessions.Clear(); OrdersForSelectedDate.Clear();
        HasUnsavedChanges = false; RecalculateTotals();
        OnPropertyChanged(nameof(CanView)); OnPropertyChanged(nameof(CanUpdate));
        OnPropertyChanged(nameof(CanPrint)); OnPropertyChanged(nameof(CanReport)); OnPropertyChanged(nameof(IsReadOnly));
        UpdateCommand.NotifyCanExecuteChanged(); PrintCommand.NotifyCanExecuteChanged(); ReportCommand.NotifyCanExecuteChanged();
        Message = CanView ? "" : "Нет права на просмотр доставки заказов.";
        if (CanView) _ = InitializeAsync();
    }
}
