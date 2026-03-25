using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class ManagerInfoViewModel : ObservableObject
{
	private readonly ManagerService _managerService;

	public ManagerInfoViewModel(ManagerService managerService)
	{
		_managerService = managerService;
		_ = LoadManagersAsync();
	}

	[ObservableProperty]
	private ObservableCollection<SalesManagerEntity> managers = [];

	[ObservableProperty]
	private SalesManagerEntity? selectedManager;

	// Load all managers
	private async Task LoadManagersAsync()
	{
		var items = await _managerService.GetManagersAsync();
		Managers = new ObservableCollection<SalesManagerEntity>(items);
	}

	[RelayCommand]
	private void Add()
	{
        // Generate next manager ID (M1, M2, ...)
        var lastManager = Managers
            .Where(m => m.Id.StartsWith("M"))
            .OrderByDescending(m =>
            {
                var part = m.Id.Length > 1 ? m.Id.Substring(1) : "0";
                return int.TryParse(part, out int num) ? num : 0;
            })
            .FirstOrDefault();

        int nextNumber = 1;

        if (lastManager != null && lastManager.Id.Length > 1)
        {
            var numericPart = lastManager.Id.Substring(1);
            if (int.TryParse(numericPart, out int number))
                nextNumber = number + 1;
        }

        // ✅ Format new ID as "M1", "M2", "M3"
        var newId = $"M{nextNumber}";


        // Create and add new manager
        SelectedManager = new SalesManagerEntity
		{
			Id = newId,
			FullName = string.Empty,
			Address = string.Empty,
			Contacts = string.Empty
		};

		Managers.Add(SelectedManager);
	}

	[RelayCommand]
	private void Update()
	{
		if (SelectedManager == null)
		{
			MessageBox.Show("Выберите менеджера для обновления.");
			return;
		}
		MessageBox.Show("Данные менеджера можно редактировать в форме.");
	}

	[RelayCommand]
    private async Task Delete()
	{
		if (SelectedManager == null)
		{
			MessageBox.Show("Выберите менеджера для удаления.");
			return;
		}
        await _managerService.DeleteManagerAsync(SelectedManager.Id);
        Managers.Remove(SelectedManager);
		SelectedManager = null;
	}

	[RelayCommand]
	private async Task Save()
	{
		try
		{
			await _managerService.SaveManagersAsync(Managers);
			MessageBox.Show("Изменения успешно сохранены!");
		}
		catch (Exception ex)
		{
			MessageBox.Show($"Ошибка при сохранении: {ex.Message}");
		}
	}
}
