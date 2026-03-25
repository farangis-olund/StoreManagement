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
using System.Windows.Threading;

namespace PresentationWpf.Views
{
    /// <summary>
    /// Interaction logic for StockLocationView.xaml
    /// </summary>
    public partial class StockLocationView : UserControl
    {
        public StockLocationView()
        {
            InitializeComponent();
        }

        private async void NewPlaceTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is StockLocationViewModel vm && vm.SelectedArticle != null)
                {
                    await vm.UpdateWarehousePlaceAsync();
                }
                ArticleComboBox.Focus();
                e.Handled = true;
            }
        }
        private void ArticleComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Move focus to the Quantity (NewPlaceTextBox)
                NewPlaceTextBox.Focus();
                e.Handled = true; 
            }
        }

     }

   

}
