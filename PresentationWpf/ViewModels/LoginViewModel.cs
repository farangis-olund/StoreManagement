
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PresentationWpf.Services;
using System.Windows.Controls;

namespace PresentationWpf.ViewModels;

public partial class LoginViewModel(IServiceProvider serviceProvider, DatabaseContext context, UserSessionService userSessionService) : ObservableObject
{
	private readonly IServiceProvider _serviceProvider = serviceProvider;
	private readonly UserSessionService _userSessionService = userSessionService;
	private readonly DatabaseContext _context = context;	
	[ObservableProperty] private string _username = null!;
	[ObservableProperty] private string _message = null!;

	[RelayCommand]
	private async Task Login(object parameter)
	{
		

		var passwordBox = parameter as PasswordBox;
		var password = passwordBox!.Password;

		if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
		{
			Message = "Вы не указали логин или пароль!";
			return;
		}
				
		var userModel = await _context.Users.Where(u => u.UserName == Username).SingleOrDefaultAsync();

		var rate = await _context.ExchangeRates
							 .Where(er => er.Code == "USD")
							 .OrderByDescending(er => er.Date)
							 .FirstOrDefaultAsync();
		if (rate != null)
		{
			_userSessionService.ExchangeRate = rate.Rate;
		}

		if (userModel !=null && Username == userModel.UserName && password == userModel.Password)
		{
			_userSessionService.FirstName = userModel.FirstName;
			_userSessionService.LastName = userModel.LastName;
			_userSessionService.UserId = userModel.Id;
			_serviceProvider.GetRequiredService<MainViewModel>().IsLoggedIn = true;
			_serviceProvider.GetRequiredService<MainViewModel>().CurrentViewModel = _serviceProvider.GetRequiredService<WelcomeViewModel>();
		}
		else
		{
			Message = "Не правельно задан Логин или Пароль!";
		}
	}

}
