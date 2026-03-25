using PresentationWpf.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PresentationWpf.Views
{
    /// <summary>
    /// Interaction logic for BrandView.xaml
    /// </summary>
    public partial class BrandView : UserControl
    {
        public BrandView()
        {
            InitializeComponent();
        }

        private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
            {
                if (DataContext is BrandViewModel vm)
                    vm.PasteFromClipboardCommand.Execute(null);
                e.Handled = true;
            }
        }

    }
}
