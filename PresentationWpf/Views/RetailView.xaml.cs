
using PresentationWpf.Dtos;
using PresentationWpf.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.ComponentModel;
using System.Windows.Media;


namespace PresentationWpf.Views;

    /// <summary>
    /// Interaction logic for RetailView.xaml
    /// </summary>
    public partial class RetailView : UserControl
    {
    private readonly DispatcherTimer _expiryTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private DateTime _expiryDay = DateTime.Today;
    public RetailView()
    {
        InitializeComponent();

        DataContextChanged += RetailView_DataContextChanged;
        _expiryTimer.Tick += (_, _) =>
        {
            if (_expiryDay == DateTime.Today) return;
            _expiryDay = DateTime.Today;
            if (DataContext is RetailViewModel model) model.RefreshExpiryStatuses();
        };

        Loaded += (_, _) =>
        {
            _expiryTimer.Start();
            if (DataContext is RetailViewModel vm)
            {
                vm.StartMonthlyPlanRefresh();
                vm.RefreshExpiryStatuses();
                vm.OnProductAdded += () => Dispatcher.Invoke(ScrollToLastCartItem);
                vm.OnInvalidQuantityFound += product =>
                    Dispatcher.Invoke(() => ScrollToProduct(product));
            }
        };
        Unloaded += (_, _) => { _expiryTimer.Stop(); if (DataContext is RetailViewModel vm) vm.StopMonthlyPlanRefresh(); };

    }

    private void ScrollToProduct(ProductModel product)
    {
        if (CartGrid.Items.Count == 0)
            return;

        CartGrid.SelectedItem = product;
        CartGrid.UpdateLayout();
        CartGrid.ScrollIntoView(product);
    }

    private void RetailView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RetailViewModel previous)
        {
            previous.CustomerSelectionRejected -= Vm_CustomerSelectionRejected;
            previous.PropertyChanged -= Vm_CartSettingsChanged;
        }
        if (e.NewValue is RetailViewModel vm)
        {
            vm.CustomerSelectionRejected += Vm_CustomerSelectionRejected;
            vm.PropertyChanged += Vm_CartSettingsChanged;
            vm.OnProductAdded -= Vm_OnProductAdded; // remove previous (safety)
            vm.OnProductAdded += Vm_OnProductAdded;
        }
        UpdateCartColumns();
    }

    private void Vm_CartSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RetailViewModel.IsBarter)) UpdateCartColumns();
    }

    private void UpdateCartColumns()
    {
        var barter = DataContext is RetailViewModel { IsBarter: true };
        CartStockQuantityColumn.Visibility = barter ? Visibility.Collapsed : Visibility.Visible;
        CartOrderQuantityColumn.DisplayIndex = barter ? 6 : 7;
        CartPriceColumn.DisplayIndex = barter ? 7 : 6;
        CartPriceColumn.IsReadOnly = !barter;
    }

    private void CartGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not RetailViewModel { IsBarter: true } ||
            CartGrid.CurrentCell.Item is not ProductModel product) return;
        var column = CartGrid.CurrentColumn;
        if (column != CartOrderQuantityColumn && column != CartPriceColumn) return;

        e.Handled = true;
        if (FindVisualChild<TextBox>(column.GetCellContent(product)) is { } editor)
        {
            editor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (Validation.GetHasError(editor)) return;
        }
        if (column == CartPriceColumn && product.BarterPriceSom is not >= 0) return;
        if (!CartGrid.CommitEdit(DataGridEditingUnit.Cell, true) ||
            !CartGrid.CommitEdit(DataGridEditingUnit.Row, true)) return;

        if (column == CartOrderQuantityColumn)
            FocusCartEditor(product, CartPriceColumn);
        else
        {
            var nextIndex = CartGrid.Items.IndexOf(product) + 1;
            if (nextIndex < CartGrid.Items.Count && CartGrid.Items[nextIndex] is ProductModel next)
                FocusCartEditor(next, CartOrderQuantityColumn);
        }
    }

    private void FocusCartEditor(ProductModel product, DataGridColumn column)
    {
        CartGrid.SelectedItem = product;
        CartGrid.CurrentCell = new DataGridCellInfo(product, column);
        CartGrid.ScrollIntoView(product, column);
        CartGrid.UpdateLayout();
        CartGrid.Focus();
        if (!CartGrid.BeginEdit()) return;
        CartGrid.UpdateLayout();
        var editor = FindVisualChild<TextBox>(column.GetCellContent(product));
        editor?.Focus();
        editor?.SelectAll();
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is T result) return result;
        if (parent == null) return null;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            if (FindVisualChild<T>(VisualTreeHelper.GetChild(parent, index)) is { } child) return child;
        return null;
    }

    private void Vm_CustomerSelectionRejected()
    {
        if (DataContext is not RetailViewModel vm) return;
        // Editable ComboBox text can differ even when SelectedItem was restored by binding.
        CustomerCodeComboBox.Text = (vm.CustomerDisplayMemberPath == "FullName"
            ? vm.SelectedCustomer?.FullName : vm.SelectedCustomer?.Id) ?? "";
        CustomerNameComboBox.Text = vm.SelectedCustomer?.FullName ?? "";
    }

    private void Vm_OnProductAdded()
    {
        Dispatcher.InvokeAsync(ScrollToLastCartItem, DispatcherPriority.Background);
    }

    private void PART_EditableTextBox_GotFocus(object sender, RoutedEventArgs e)
	{
		// Handle GotFocus event
		// For example, you could change the appearance of the TextBox when it gains focus
	}

	private void PART_EditableTextBox_LostFocus(object sender, RoutedEventArgs e)
	{
		// Handle LostFocus event
		// For example, you could reset the appearance of the TextBox when it loses focus
	}

    public void ScrollToLastCartItem()
    {
        if (CartGrid.Items.Count == 0)
            return;

        var last = CartGrid.Items[CartGrid.Items.Count - 1];

        CartGrid.UpdateLayout();
        CartGrid.ScrollIntoView(last);
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not RetailViewModel vm)
            return;

        // Suggestion exists?
        if (!string.IsNullOrEmpty(vm.InlineSuggestion))
        {
            // Accept suggestion on TAB or RightArrow
            if (e.Key == Key.Tab || e.Key == Key.Right)
            {
                // ⬇️ Replace entire TextBox with full articul
                SearchBox.Text = vm.FullSuggestion;

                // Update ViewModel
                vm.FilterText = vm.FullSuggestion;

                // Move caret to end
                SearchBox.CaretIndex = SearchBox.Text.Length;

                // Hide inline suggestion
                vm.InlineSuggestion = "";
                vm.FullSuggestion = "";

                e.Handled = true;
            }
        }
    }

    private void CustomerPurchaseControls_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged || PurchaseSummaryPanel == null || CustomerFieldsPanel == null) return;
        var inline = e.NewSize.Width >= 1370;
        Grid.SetColumnSpan(CustomerFieldsPanel, inline ? 1 : 2);
        Grid.SetRow(PurchaseSummaryPanel, inline ? 0 : 1);
        Grid.SetColumn(PurchaseSummaryPanel, inline ? 1 : 0);
        Grid.SetColumnSpan(PurchaseSummaryPanel, inline ? 1 : 2);
        PurchaseSummaryPanel.HorizontalAlignment = inline ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    private void ProductSearchControls_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged || ProductStatusLegend == null) return;
        var inline = e.NewSize.Width >= 850;
        Grid.SetRow(ProductStatusLegend, inline ? 1 : 2);
        Grid.SetColumn(ProductStatusLegend, inline ? 1 : 0);
        Grid.SetColumnSpan(ProductStatusLegend, inline ? 1 : 2);
        ProductStatusLegend.Margin = inline ? new Thickness(16, 0, 5, 0) : new Thickness(0, 6, 5, 2);
    }



}
