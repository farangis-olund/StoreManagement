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
    /// Interaction logic for ReturnDebtRepaymentView.xaml
    /// </summary>
    public partial class ReturnDebtRepaymentView : UserControl
    {
        public ReturnDebtRepaymentView()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                if (DataContext is ViewModels.ReturnDebtRepaymentViewModel vm)
                {
                    vm.RequestFocusArtikul += () =>
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            ArtikulComboBox.Focus();
                            // ArtikulComboBox.IsDropDownOpen = true;
                        });
                    };

                    vm.RequestFocusQuantity += () =>
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            QuantityTextBox.Focus();
                            QuantityTextBox.SelectAll();
                        });
                    };
                }
            };
        }
    }
}
