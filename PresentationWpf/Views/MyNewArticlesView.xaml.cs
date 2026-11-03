using System.Windows.Controls;
using PresentationWpf.ViewModels;

namespace PresentationWpf.Views;

public partial class MyNewArticlesView : UserControl
{
    public MyNewArticlesView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is MyNewArticlesViewModel vm)
                await vm.RefreshDraftsCommand.ExecuteAsync(null);
        };
    }
}
