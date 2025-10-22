using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Services;
using PresentationWpf.Views;

namespace PresentationWpf.ViewModels;

public partial class ReferenceViewModel : ObservableObject
{
    private readonly OrganizationInfoService _orgService;
	private readonly ManagerService _managerService;
	private readonly StorekeeperService _storekeeperService;

	[ObservableProperty] private object? selectedReferenceView;

    public ReferenceViewModel(OrganizationInfoService orgService, ManagerService managerService, StorekeeperService storekeeperService)
    {
        _orgService = orgService;
		_managerService = managerService;
		_storekeeperService = storekeeperService;
    }

    [RelayCommand]
    private void OpenOrganization()
    {
        SelectedReferenceView = new OrganizationInfoView
        {
            DataContext = new OrganizationInfoViewModel(_orgService)
        };
    }

	[RelayCommand]
	private void OpenManager()
	{
		SelectedReferenceView = new ManagerInfoView
		{
			DataContext = new ManagerInfoViewModel(_managerService)
		};
	}


	[RelayCommand]
	private void OpenAssignPicker()
	{
		SelectedReferenceView = new AssignPickerInfoView
		{
			DataContext = new AssignPickerInfoViewModel(_storekeeperService)
		};
	}
}
