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
    /// Interaction logic for CustomerView.xaml
    /// </summary>
    public partial class CustomerView : UserControl
    {
        public CustomerView()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            CommitFocusedTextBox();
        }

        private void SaveButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CommitFocusedTextBox();
        }

        private static void CommitFocusedTextBox()
        {
            if (Keyboard.FocusedElement is TextBox textBox)
                BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty)?.UpdateSource();
        }
    }
}
