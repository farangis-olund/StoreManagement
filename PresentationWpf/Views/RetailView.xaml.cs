
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace PresentationWpf.Views;

    /// <summary>
    /// Interaction logic for RetailView.xaml
    /// </summary>
    public partial class RetailView : UserControl
    {
        public RetailView()
        {
            InitializeComponent();
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

	

}
