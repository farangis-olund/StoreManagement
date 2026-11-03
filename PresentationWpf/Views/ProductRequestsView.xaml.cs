using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using PresentationWpf.ViewModels;
using PresentationWpf.Services;

namespace PresentationWpf.Views;

public partial class ProductRequestsView : UserControl
{
    public ProductRequestsView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ProductRequestsViewModel vm) await vm.RefreshCommand.ExecuteAsync(null);
        };
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProductRequestsViewModel vm || vm.Rows.Count == 0) return;
        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return;
            // A paginated document prints all rows, including those outside the on-screen viewport.
            var document = ProductRequestPrintDocument.Create(vm.Title, vm.FilterDescription, vm.Rows.ToArray(),
                new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight));
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Заявки на товары");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "Ошибка печати", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
