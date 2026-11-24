using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PresentationWpf.ViewModels;

namespace PresentationWpf.Views;

public partial class ProductExpiryView : UserControl
{
    private DateTime _day = DateTime.Today;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };
    public ProductExpiryView()
    {
        InitializeComponent();
        _timer.Tick += async (_, _) =>
        {
            if (_day == DateTime.Today) return;
            if (DataContext is not ProductExpiryViewModel { CanWork: true } model) return;
            _day = DateTime.Today; await model.InitializeAsync();
        };
        Unloaded += (_, _) => _timer.Stop();
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _timer.Start();
        if (DataContext is ProductExpiryViewModel model) await model.InitializeAsync();
    }
}
