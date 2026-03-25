
using PresentationWpf.Dtos;
using PresentationWpf.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;


namespace PresentationWpf.Views;

    /// <summary>
    /// Interaction logic for RetailView.xaml
    /// </summary>
    public partial class RetailView : UserControl
    {
    public RetailView()
    {
        InitializeComponent();

        DataContextChanged += RetailView_DataContextChanged;

        Loaded += (_, _) =>
        {
            if (DataContext is RetailViewModel vm)
            {
                vm.OnProductAdded += () => Dispatcher.Invoke(ScrollToLastCartItem);
                vm.OnInvalidQuantityFound += product =>
                    Dispatcher.Invoke(() => ScrollToProduct(product));
            }
        };

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
        if (e.NewValue is RetailViewModel vm)
        {
            vm.OnProductAdded -= Vm_OnProductAdded; // remove previous (safety)
            vm.OnProductAdded += Vm_OnProductAdded;
        }
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



}
