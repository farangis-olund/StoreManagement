using System.Windows.Controls;
using PresentationWpf.ViewModels;

namespace PresentationWpf.Views;

public partial class OfficialMonthlyPlansView : UserControl
{
    public OfficialMonthlyPlansView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (!IsLoaded) return;
            if (e.OldValue is OfficialMonthlyPlansViewModel old) old.Deactivate();
            if (e.NewValue is OfficialMonthlyPlansViewModel current) current.Activate();
        };
        Loaded += (_, _) => { if (DataContext is OfficialMonthlyPlansViewModel vm) vm.Activate(); };
        Unloaded += (_, _) => { if (DataContext is OfficialMonthlyPlansViewModel vm) vm.Deactivate(); };
    }
}
