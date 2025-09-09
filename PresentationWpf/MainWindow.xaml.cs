using PresentationWpf.ViewModels;
using System.Windows;


namespace PresentationWpf;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
	private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
	{
		if (SidebarColumn.Width.Value > 0)
			SidebarColumn.Width = new GridLength(0); 
		else
			SidebarColumn.Width = new GridLength(200); 
	}

}