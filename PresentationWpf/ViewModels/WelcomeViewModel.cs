
using CommunityToolkit.Mvvm.ComponentModel;
using PresentationWpf.Services;

namespace PresentationWpf.ViewModels;

public partial class WelcomeViewModel : ObservableObject
{
	private readonly IServiceProvider _serviceProvider;
	private readonly UserSessionService _userSessionService;
	

	public WelcomeViewModel(IServiceProvider serviceProvider, UserSessionService userSessionService)
	{
		_serviceProvider = serviceProvider;
		_userSessionService = userSessionService;
		WelcomeMessage = $"Добро пожаловать, {_userSessionService.FirstName + " " + _userSessionService.LastName}!";

	}

	[ObservableProperty]
	string _welcomeMessage = null!;
}
