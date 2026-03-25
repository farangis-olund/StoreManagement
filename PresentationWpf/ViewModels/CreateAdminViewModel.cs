
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class CreateAdminViewModel : ObservableObject
{
    private readonly IDbContextFactory<DatabaseContext> _dbFactory ;
    private readonly IServiceProvider _sp;

    [ObservableProperty] private string _userName = "";
    [ObservableProperty] private string _firstName = "";
    [ObservableProperty] private string _lastName = "";
   [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _password = "";

    public CreateAdminViewModel(IDbContextFactory<DatabaseContext> dbFactory, IServiceProvider sp)
    {
        _dbFactory = dbFactory;
        _sp = sp;
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
        { Message = "Укажите логин и пароль."; return; }

        await using var db = await _dbFactory.CreateDbContextAsync();

        if (await db.Users.AnyAsync())
        { Message = "Администратор уже создан. Войдите в систему."; return; }

        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Администратор");
        if (adminRole == null)
        {
            adminRole = new RoleEntity { RoleName = "Администратор", Description = "Полные права" };
            db.Roles.Add(adminRole);
            await db.SaveChangesAsync();
        }

        var user = new UserEntity
        {
           
            UserName = UserName.Trim(),
            FirstName = FirstName?.Trim(),
            LastName = LastName?.Trim(),
            Password = Password 
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserRoles.Add(new UserRoleEntity { UserId = user.Id, RoleId = adminRole.Id });
        await db.SaveChangesAsync();

        Message = "Администратор создан. Теперь войдите.";
        MessageBox.Show(
            "Администратор создан. Теперь войдите.",
            "Успех",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        _sp.GetRequiredService<MainViewModel>().CurrentViewModel = _sp.GetRequiredService<LoginViewModel>();
    }


}
