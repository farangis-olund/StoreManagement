
using PresentationWpf.Converters;
using PresentationWpf.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;


namespace PresentationWpf.Views;

/// <summary>
/// Interaction logic for SaleTotalByGroupReportView.xaml
/// </summary>
public partial class SaleTotalByGroupReportView : UserControl
{
    public SaleTotalByGroupReportView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is SaleTotalByGroupReportViewModel vm)
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
      
    private void GenerateColumns(SaleTotalByGroupReportViewModel vm)
    {
        PivotGrid.Columns.Clear();

        // FIRST COLUMN
        var rowColumn = new DataGridTextColumn
        {
            Header = "Область",
            Binding = new Binding("RowKey"),
            Width = 90
        };

        PivotGrid.Columns.Add(rowColumn);

      
        // BRAND COLUMNS
       for (int i = 0; i < vm.BrandColumns.Count; i++)
        {
            int columnIndex = i;

            var brandColumn = new DataGridTextColumn
            {
                Header = vm.BrandColumns[i],
                Binding = new Binding($"Cells[{columnIndex}].Value")
                {
                    StringFormat = "N2"
                },
                Width = 100
            };

            brandColumn.ElementStyle = new Style(typeof(TextBlock))
            {
                Setters =
        {
            new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right)
        }
            };

            PivotGrid.Columns.Add(brandColumn);
        }

        // TOTAL COLUMN
        var totalColumn = new DataGridTextColumn
        {
            Header = "ИТОГО",
            Binding = new Binding("Total")
            {
                StringFormat = "N2"
            },
            Width = 110
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

        // 🔥 ADD ROW STYLE HERE
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
                Binding = new Binding("IsTotalRow"),
                Value = true,
                Setters =
                {
                    new Setter(DataGridRow.FontWeightProperty, FontWeights.Bold),
                    new Setter(DataGridRow.BackgroundProperty,
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EDEDED")))
                }
            },

            new DataTrigger
            {
                Binding = new Binding("IsPercentRow"),
                Value = true,
                Setters =
                {
                    new Setter(DataGridRow.FontWeightProperty, FontWeights.Bold),
                    new Setter(DataGridRow.BackgroundProperty,
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5")))
                }
            }
        }
        };
    }
}