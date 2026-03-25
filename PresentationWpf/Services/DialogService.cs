
using System.Windows;

namespace PresentationWpf.Services;

public class DialogService
{

	// keep one window per VM type (enough for Summary case)
	private readonly Dictionary<Type, Window> _open = new();

	// === Modeless show (so you can Hide/Show later) ===
	public void Show(object viewModel)
	{
		var win = CreateWindow(viewModel);
		var key = viewModel.GetType();

		// replace existing if already open
		if (_open.TryGetValue(key, out var old))
		{
			old.Close();
			_open.Remove(key);
		}

		win.DataContext = viewModel;
		win.Owner = Application.Current.Windows.OfType<Window>()
					  .FirstOrDefault(w => w.IsActive);
		win.ShowInTaskbar = false;
		win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		win.Closed += (_, __) => _open.Remove(key);

		_open[key] = win;
		win.ShowDialog();
	}

	public void Hide<TViewModel>()
	{
		if (_open.TryGetValue(typeof(TViewModel), out var w))
			w.Hide();
	}

	public void ShowAgain<TViewModel>()
	{
		if (_open.TryGetValue(typeof(TViewModel), out var w))
		{
			w.Show();
			w.Activate();
		}
	}

	public void Close<TViewModel>()
	{
		if (_open.TryGetValue(typeof(TViewModel), out var w))
		{
			_open.Remove(typeof(TViewModel));
			w.Close();
		}
	}

	// keep your existing ShowDialogAsync if you still use dialogs elsewhere
	public Task<bool?> ShowDialogAsync(object viewModel)
	{
		var window = CreateWindow(viewModel);
		window.DataContext = viewModel;
		window.Owner = Application.Current.Windows.OfType<Window>()
						 .FirstOrDefault(w => w.IsActive);
		bool? result = window.ShowDialog();
		return Task.FromResult(result);
	}

    // Map VM -> Window hosting the UserControl
    private Window CreateWindow(object vm) => vm switch
    {
        ViewModels.SummaryViewModel => new Window
        {
            Title = "Итоги по клиенту",
            Width = 1100,
            Height = 520,
            ResizeMode = ResizeMode.CanResize,
            Content = new Views.SummaryWindow()
        },

        ViewModels.PendingOrdersViewModel => new Window
        {
            Title = "Неотправленные заказы",
            Width = 500,
            Height = 600,
            ResizeMode = ResizeMode.CanResize,
            Content = new Views.PendingOrdersView()
        },

        ViewModels.DeliveryOrdersViewModel dovm => CreateDeliveryOrdersWindow(dovm),

        ViewModels.AssignPickersViewModel => new Window
        {
            Title = "Назначение комплектовщиков",
            Width = 600,
            Height = 500,
            ResizeMode = ResizeMode.CanResize,
            Content = new Views.AssignPickersView()
        },

        _ => throw new NotSupportedException(
            $"No window mapping for {vm.GetType().Name}")
    };

    private Window CreateDeliveryOrdersWindow(ViewModels.DeliveryOrdersViewModel vm)
    {
        var window = new Window
        {
            Title = "Доставка заказов",
            Width = 1200,
            Height = 600,
            ResizeMode = ResizeMode.CanResize,
            Content = new Views.DeliveryOrdersView { DataContext = vm }
        };

        window.Closing += (s, e) =>
        {
            if (vm.HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "Вы изменили оплату, но не нажали 'Обновить'.\n" +
                    "Хотите отменить изменения и закрыть окно?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true; 
                }
            }
        };

        return window;
    }



}
