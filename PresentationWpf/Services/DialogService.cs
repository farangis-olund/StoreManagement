
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
		win.Show();
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
	private Window CreateWindow(object vm) =>
		vm switch
		{
			PresentationWpf.ViewModels.SummaryViewModel => new Window
			{
				Title = "Итоги по клиенту",
				Width = 820,
				Height = 520,
				ResizeMode = ResizeMode.CanResize,
				Content = new PresentationWpf.Views.SummaryWindow()
			},
			_ => throw new NotSupportedException(
				$"No window mapping for {vm.GetType().Name}")
		};
}
