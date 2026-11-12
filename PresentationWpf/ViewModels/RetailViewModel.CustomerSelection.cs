using Infrastructure.Dtos;
using System.Windows;
using System.Windows.Threading;

namespace PresentationWpf.ViewModels;

public partial class RetailViewModel
{
    private bool _isShowingCustomerChangeMessage;
    public event Action? CustomerSelectionRejected;

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (_selectedCustomer == value) return;
            if (!CanChangeCustomer(value?.Id, nameof(SelectedCustomer))) return;
            if (value != null && _retailManagerService != null &&
                (!IsRetailManagerContextReady || !AllCustomers.Any(c => c.Id == value.Id) || !CustomerFilter(value)))
            {
                RestoreCustomerSelection(nameof(SelectedCustomer));
                return;
            }

            OnPropertyChanging();
            _selectedCustomer = value;
            OnSelectedCustomerChanged(value);
            OnPropertyChanged();
        }
    }

    private bool CanChangeCustomer(string? customerId, string selectionProperty)
    {
        if (SelectedProductList.Count == 0 || customerId == SelectedCustomer?.Id) return true;

        // Reject before changing the customer or any dependent order data.
        if (!_isShowingCustomerChangeMessage)
        {
            _isShowingCustomerChangeMessage = true;
            try { ShowCustomerChangeBlockedMessage(); }
            finally { _isShowingCustomerChangeMessage = false; }
        }
        RestoreCustomerSelection(selectionProperty);
        return false;
    }

    private void RestoreCustomerSelection(string selectionProperty)
    {
        OnPropertyChanged(selectionProperty);
        // A two-way ComboBox binding can ignore a notification during its source update.
        // Repeat after that update so both the code and name display the retained customer.
        void Restore()
        {
            OnPropertyChanged(selectionProperty);
            if (selectionProperty == nameof(SelectedCustomer)) CustomerSelectionRejected?.Invoke();
        }
        if (Application.Current?.Dispatcher is { } dispatcher)
            dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(Restore));
        else Restore();
    }

    protected virtual void ShowCustomerChangeBlockedMessage() => MessageBox.Show(
        "В корзине есть товары. Оформите заказ или очистите корзину, прежде чем выбрать другого клиента.",
        "Смена клиента", MessageBoxButton.OK, MessageBoxImage.Warning);
}
