using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace PresentationWpf.Services;

public class NavigationService
{
    private readonly IServiceProvider _provider;

    public NavigationService(IServiceProvider provider)
    {
        _provider = provider;
    }

    public TView Open<TView>() where TView : FrameworkElement
    {
        // 1️⃣ Resolve the view from DI
        var view = _provider.GetRequiredService<TView>();

        // 2️⃣ Try resolving ViewModel using naming convention
        // PriceLevelView → PriceLevelViewModel
        var viewType = typeof(TView);
        var viewModelName = viewType.FullName!.Replace("View", "ViewModel");
        var viewModelType = Type.GetType(viewModelName);

        if (viewModelType != null)
        {
            var viewModel = _provider.GetRequiredService(viewModelType);
            view.DataContext = viewModel;
        }

        return view;
    }
}
