
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Services;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class OrganizationInfoViewModel : ObservableObject
{
    private readonly OrganizationInfoService _service;

    [ObservableProperty] private string organizationCode = "";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string address = "";
    [ObservableProperty] private string city = "";
    [ObservableProperty] private string region = "";
    [ObservableProperty] private string phoneNumber = "";
    [ObservableProperty] private string exportPath = "";
    [ObservableProperty] private string importPath = "";

    public OrganizationInfoViewModel(OrganizationInfoService service)
    {
        _service = service;
        Load();
    }

    private async void Load()
    {
        var entity = await _service.GetAsync();
        if (entity != null)
        {
            OrganizationCode = entity.OrganizationCode ?? "";
            Name = entity.Name ?? "";
            Address = entity.Address ?? "";
            City = entity.City ?? "";
            Region = entity.Region ?? "";
            PhoneNumber = entity.PhoneNumber ?? "";
            ExportPath = entity.ExportPath ?? "";
            ImportPath = entity.ImportPath ?? "";
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        var entity = new OrganizationInfoEntity
        {
            OrganizationCode = OrganizationCode,
            Name = Name,
            Address = Address,
            City = City,
            Region = Region,
            PhoneNumber = PhoneNumber,
            ExportPath = ExportPath,
            ImportPath = ImportPath
        };

        await _service.UpdateAsync(entity);
        MessageBox.Show("Изменения сохранены", "Организация", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
