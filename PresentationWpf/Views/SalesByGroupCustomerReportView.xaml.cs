using PresentationWpf.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace PresentationWpf.Views;

public partial class SalesByGroupCustomerReportView : UserControl
{
    public SalesByGroupCustomerReportView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is SalesByGroupCustomerReportViewModel vm)
        {
            vm.PivotTableUpdated += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    GenerateColumns(vm);
                });
            };
        }
    }

    private void GenerateColumns(SalesByGroupCustomerReportViewModel vm)
    {
        PivotGrid.Columns.Clear();
        
        string numberFormat = vm.TotalsByQuantity ? "N0" : "N2";

        // FIRST COLUMN (Product Group)
        var rowColumn = new DataGridTextColumn
        {
            Header = "Клиент",
            Binding = new Binding("RowKey"),
            Width = 180
        };

        PivotGrid.Columns.Add(rowColumn);

        // CUSTOMER COLUMNS
        for (int i = 0; i < vm.CustomerColumns.Count; i++)
        {
            int index = i;

            var column = new DataGridTextColumn
            {
                Header = vm.CustomerColumns[i],
                Binding = new Binding($"Cells[{index}].Value")
                {
                    StringFormat = numberFormat
                },
                Width = 70
            };

            column.ElementStyle = new Style(typeof(TextBlock))
            {
                Setters =
                {
                    new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right)
                }
            };

            PivotGrid.Columns.Add(column);
        }

        // TOTAL COLUMN
        var totalColumn = new DataGridTextColumn
        {
            Header = "ИТОГО",
            Binding = new Binding("Total")
            {
                StringFormat = numberFormat
            },
            Width = 90
        };

        totalColumn.ElementStyle = new Style(typeof(TextBlock))
        {
            Setters =
            {
                new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right),
                new Setter(TextBlock.FontWeightProperty, FontWeights.Bold)
            }
        };

        PivotGrid.Columns.Add(totalColumn);

        // STYLE TOTAL ROW
        PivotGrid.RowStyle = new Style(typeof(DataGridRow))
        {
            Setters =
            {
                new Setter(DataGridRow.BackgroundProperty, Brushes.White)
            },
            Triggers =
            {
                new DataTrigger
                {
                    Binding = new Binding("RowKey"),
                    Value = "ВСЕГО",
                    Setters =
                    {
                        new Setter(DataGridRow.FontWeightProperty, FontWeights.Bold),
                        new Setter(DataGridRow.BackgroundProperty,
                            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDEDED")))
                    }
                }
            }
        };
    }
}