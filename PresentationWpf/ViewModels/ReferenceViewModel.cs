using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Services;
using PresentationWpf.Views;

namespace PresentationWpf.ViewModels;

public partial class ReferenceViewModel : ObservableObject
{
    private readonly OrganizationInfoService _orgService;

    [ObservableProperty] private object? selectedReferenceView;

    public ReferenceViewModel(OrganizationInfoService orgService)
    {
        _orgService = orgService;
    }

    [RelayCommand]
    private void OpenOrganization()
    {
        SelectedReferenceView = new OrganizationInfoView
        {
            DataContext = new OrganizationInfoViewModel(_orgService)
        };
    }
}
