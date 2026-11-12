using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Win32;
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

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProductRequestsViewModel vm || !vm.CanExportExcel) return;
        var rows = RequestsGrid.Items.OfType<Infrastructure.Dtos.ProductRequestSummary>().ToArray();
        var title = vm.Title;
        var filters = vm.FilterDescription;
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Экспорт заявок на товары",
                FileName = $"Заявки на товары — {title} — {DateTime.Now:yyyy-MM-dd}.xlsx",
                Filter = "Файл Excel (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                AddExtension = true,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            using var workbook = ProductRequestExcelExport.Create(title, filters, rows);
            workbook.SaveAs(dialog.FileName);
            MessageBox.Show("Заявки сохранены в файл Excel.", "Экспорт в Excel",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось сохранить файл Excel.\n{ex.GetBaseException().Message}",
                "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
