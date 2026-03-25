using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using PresentationWpf.ViewModels;

namespace PresentationWpf.Views;

public partial class SalesManagerReportView : UserControl
{
    private SalesManagerReportViewModel? _viewModel;

    public SalesManagerReportView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Unsubscribe old VM
        if (_viewModel != null)
            _viewModel.PivotTableUpdated -= OnPivotTableUpdated;

        if (e.NewValue is SalesManagerReportViewModel vm)
        {
            _viewModel = vm;
            vm.PivotTableUpdated += OnPivotTableUpdated;
        }
    }

    private void OnPivotTableUpdated()
    {
        Dispatcher.Invoke(() =>
        {
            if (_viewModel != null)
                GenerateColumns(_viewModel);
        });
    }

    private void GenerateColumns(SalesManagerReportViewModel vm)
    {
        ReportGrid.Columns.Clear();

        ReportGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Менеджер",
            Binding = new Binding("[Manager]"),
            Width = 120
        });

        foreach (var company in vm.CompanyColumns)
        {
            ReportGrid.Columns.Add(new DataGridTextColumn
            {
                Header = $"{company}",
                Binding = new Binding($"[{company}_Sales]")
                {
                    StringFormat = "N2"
                },
                Width = 95,
                ElementStyle = CreateRightAlignedStyle()
            });
        }

        ReportGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Продажа",
            Binding = new Binding("[SalesTotal]")
            {
                StringFormat = "N2"
            },
            Width = 100,
            ElementStyle = CreateRightAlignedStyle()
        });

        ReportGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Возврат",
            Binding = new Binding("[ReturnTotal]")
            {
                StringFormat = "N2"
            },
            Width = 90,
            ElementStyle = CreateRightAlignedStyle()
        });

        ReportGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Итого %",
            Binding = new Binding("[CommissionTotal]")
            {
                StringFormat = "N2"
            },
            Width = 90,
            ElementStyle = new Style(typeof(TextBlock))
            {
                Setters =
            {
                new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right),
                new Setter(TextBlock.FontWeightProperty, FontWeights.Bold)
            }
            }
        });

        ReportGrid.RowStyle = new Style(typeof(DataGridRow))
        {
            Triggers =
        {
            new DataTrigger
            {
                Binding = new Binding("[Manager]"),
                Value = "ИТОГО",
                Setters =
                {
                    new Setter(DataGridRow.FontWeightProperty, FontWeights.Bold),
                    new Setter(DataGridRow.BackgroundProperty,
                        new SolidColorBrush(Color.FromRgb(235, 235, 235)))
                }
            }
        }
        };
    }
    private Style CreateRightAlignedStyle()
    {
        return new Style(typeof(TextBlock))
        {
            Setters =
            {
                new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right)
            }
        };
    }

    
}