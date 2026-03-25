using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Repositories;
using System.Collections.ObjectModel;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class PriceLevelViewModel : ObservableObject
{
    private readonly PriceLevelRepository _priceRepo;

    // COLLECTION
    [ObservableProperty]
    private ObservableCollection<PriceLevelEntity> priceLevels = new();

    // SELECTED ITEM
    [ObservableProperty]
    private PriceLevelEntity selectedPriceLevel = new();

    public PriceLevelViewModel(PriceLevelRepository priceRepo)
    {
        _priceRepo = priceRepo;
        _ = LoadDataAsync();
    }


    // ======================================================
    // LOAD DATA
    // ======================================================
    private async Task LoadDataAsync()
    {
        var list = await _priceRepo.GetAllAsync();
        PriceLevels = new ObservableCollection<PriceLevelEntity>(list);

        OnPropertyChanged(nameof(IsPriceLevelValid));
    }


    // ======================================================
    // ADD
    // ======================================================
    [RelayCommand]
    private void Add()
    {
        if (PriceLevels == null || PriceLevels.Count == 0)
        {
            // First entry
            SelectedPriceLevel = new PriceLevelEntity
            {
                Level = "Level1",
                Code = 1,
                PriceType = "Уровень1"
            };
            return;
        }

        // Extract existing numeric parts from Level (e.g., "Level10" → 10)
        var numbers = PriceLevels
            .Select(pl =>
            {
                var digits = new string(pl.Level.Where(char.IsDigit).ToArray());
                return int.TryParse(digits, out int num) ? num : 0;
            })
            .ToList();

        // Find the NEXT free number
        int nextNumber = numbers.Count == 0 ? 1 : numbers.Max() + 1;

        // Create the new default entry
        SelectedPriceLevel = new PriceLevelEntity
        {
            Level = $"Level{nextNumber}",
            Code = nextNumber,
            PriceType = $"Уровень{nextNumber}"
        };
    }



    // ======================================================
    // UPDATE (Just selects item for editing)
    // ======================================================
    [RelayCommand]
    private void Update()
    {
        if (SelectedPriceLevel == null)
        {
            MessageBox.Show("Выберите уровень для редактирования.",
                "Редактирование", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }


    // ======================================================
    // SAVE (Insert or Update)
    // ======================================================
    [RelayCommand]
    private async Task Save()
    {
        if (!IsPriceLevelValid)
        {
            MessageBox.Show("Заполните обязательное поле: Уровень.",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string level = SelectedPriceLevel.Level?.Trim() ?? "";

        // ==========================
        //  AUTO-FILL CODE
        // ==========================
        // Extract the number from "Level1", "Level10", etc.
        var digits = new string(level.Where(char.IsDigit).ToArray());

        if (int.TryParse(digits, out int number))
            SelectedPriceLevel.Code = number;
        else
            SelectedPriceLevel.Code = 0;

        // ==========================
        //  AUTO-FILL PRICE TYPE
        // ==========================
        SelectedPriceLevel.PriceType = $"Уровень{(string.IsNullOrEmpty(digits) ? level : digits)}";


        bool isNew = !PriceLevels.Any(pl => pl.Level == SelectedPriceLevel.Level);

        if (isNew)
        {
            await _priceRepo.AddAsync(SelectedPriceLevel);
            MessageBox.Show("Уровень успешно добавлен.",
                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            await _priceRepo.UpdateAsync(
                p => p.Level == SelectedPriceLevel.Level,
                SelectedPriceLevel);

            MessageBox.Show("Изменения сохранены.",
                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        await LoadDataAsync();
    }



    // ======================================================
    // DELETE
    // ======================================================
    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedPriceLevel == null || string.IsNullOrWhiteSpace(SelectedPriceLevel.Level))
        {
            MessageBox.Show("Выберите уровень для удаления.",
                "Удаление", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Удалить уровень \"{SelectedPriceLevel.Level}\"?",
            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        await _priceRepo.RemoveAsync(p => p.Level == SelectedPriceLevel.Level);

        MessageBox.Show("Уровень удалён.",
            "Удаление", MessageBoxButton.OK, MessageBoxImage.Information);

        await LoadDataAsync();
        SelectedPriceLevel = new PriceLevelEntity();
    }


    // ======================================================
    // VALIDATION
    // ======================================================
    public bool IsPriceLevelValid =>
        SelectedPriceLevel != null &&
        !string.IsNullOrWhiteSpace(SelectedPriceLevel.Level);

    partial void OnSelectedPriceLevelChanged(PriceLevelEntity value)
    {
        OnPropertyChanged(nameof(IsPriceLevelValid));
        SaveCommand?.NotifyCanExecuteChanged();
    }
}
