using PresentationWpf.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;


namespace PresentationWpf.Views
{
    /// <summary>
    /// Interaction logic for MovementView.xaml
    /// </summary>
    public partial class MovementView : UserControl
    {
        public MovementView()
        {
            InitializeComponent();
        }

        private async void QuantityTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is MovementViewModel vm)
                {
                    if (vm.IsIncoming)
                        await vm.AddIncomingAsync();
                    else if (vm.IsOutgoing)
                        await vm.AddOutgoingAsync();
                }

                ArticleComboBox.Focus();
                e.Handled = true;
            }
        }

        private void ArticleComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                QuantityTextBox.Focus();
                e.Handled = true;
            }
        }

    }
}
