using CommunityToolkit.Mvvm.ComponentModel;
using PresentationWpf.Services;

namespace PresentationWpf.ViewModels;

public partial class WelcomeViewModel : ObservableObject
{
    public WelcomeViewModel(UserSessionService session)
    {
        WelcomeMessage = $"Добро пожаловать, {session.FirstName} {session.LastName}!";
    }

    [ObservableProperty] private string _welcomeMessage;
}
