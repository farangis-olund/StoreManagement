using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class AssignPickerInfoViewModel : ObservableObject
{
	private readonly StorekeeperService _storekeeperService;

	public AssignPickerInfoViewModel(StorekeeperService storekeeperService)
	{
		_storekeeperService = storekeeperService;
		_ = LoadAsync();
	}

	[ObservableProperty]
	private ObservableCollection<StorekeeperEntity> storekeepers = [];

	[ObservableProperty]
	private StorekeeperEntity? selectedStorekeeper;

	private async Task LoadAsync()
	{
		var list = await _storekeeperService.GetStorekeepersAsync();
		Storekeepers = new ObservableCollection<StorekeeperEntity>(list);
	}
	
	[RelayCommand]
	private void Add()
	{
		// Generate next storekeeper ID (K001, K002, ...)
		var lastStorekeeper = Storekeepers
			.Where(s => s.Id.StartsWith("K"))
			.OrderByDescending(s => s.Id)
			.FirstOrDefault();

		int nextNumber = 1;

		if (lastStorekeeper != null && lastStorekeeper.Id.Length > 1)
		{
			var numericPart = lastStorekeeper.Id.Substring(1);
			if (int.TryParse(numericPart, out int number))
				nextNumber = number + 1;
		}

		var newId = $"K{nextNumber:D3}";

		// Create and add new storekeeper
		SelectedStorekeeper = new StorekeeperEntity
		{
			Id = newId,
			FullName = string.Empty,
			Phone = string.Empty,
			Active = true
		};

		Storekeepers.Add(SelectedStorekeeper);
	}

	[RelayCommand]
	private void Update()
	{
		if (SelectedStorekeeper == null)
		{
			MessageBox.Show("Выберите кладовщика для редактирования.");
			return;
		}
		MessageBox.Show("Данные можно изменить в форме выше.");
	}

	[RelayCommand]
	private void Delete()
	{
		if (SelectedStorekeeper == null)
		{
			MessageBox.Show("Выберите кладовщика для удаления.");
			return;
		}
		Storekeepers.Remove(SelectedStorekeeper);
		SelectedStorekeeper = null;
	}

	[RelayCommand]
	private async Task Save()
	{
		try
		{
			await _storekeeperService.SaveStorekeepersAsync(Storekeepers);
			MessageBox.Show("Изменения успешно сохранены!");
		}
		catch (Exception ex)
		{
			MessageBox.Show($"Ошибка: {ex.Message}");
		}
	}
}
