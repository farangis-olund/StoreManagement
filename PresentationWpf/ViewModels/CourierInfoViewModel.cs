using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace PresentationWpf.ViewModels;

public partial class CourierInfoViewModel : ObservableObject
{
    private readonly CourierService _courierService;

    public CourierInfoViewModel(CourierService courierService)
    {
        _courierService = courierService;
        _ = LoadAsync();
    }

    [ObservableProperty]
    private ObservableCollection<CourierEntity> couriers = [];

    [ObservableProperty]
    private CourierEntity? selectedCourier;

    // === Load all couriers ===
    private async Task LoadAsync()
    {
        var list = await _courierService.GetCouriersAsync();
        Couriers = new ObservableCollection<CourierEntity>(list);
    }

    // === Add new courier ===
    [RelayCommand]
    private void Add()
    {
        // Generate next courier ID (D1, D2, ...)
        var lastCourier = Couriers
            .Where(c => c.Id.StartsWith("D"))
            .OrderByDescending(c =>
            {
                // safely extract numeric part (e.g. D15 → 15)
                var part = c.Id.Length > 1 ? c.Id.Substring(1) : "0";
                return int.TryParse(part, out int num) ? num : 0;
            })
            .FirstOrDefault();

        int nextNumber = 1;

        if (lastCourier != null && lastCourier.Id.Length > 1)
        {
            var numericPart = lastCourier.Id.Substring(1);
            if (int.TryParse(numericPart, out int number))
                nextNumber = number + 1;
        }

        var newId = $"D{nextNumber}";


        // Create and add new courier
        SelectedCourier = new CourierEntity
        {
            Id = newId,
            FullName = string.Empty,
            Phone = string.Empty,
            Active = true
        };

        Couriers.Add(SelectedCourier);
    }

    // === Update ===
    [RelayCommand]
    private void Update()
    {
        if (SelectedCourier == null)
        {
            MessageBox.Show("Выберите курьера для редактирования.");
            return;
        }

        MessageBox.Show("Данные можно изменить в форме выше.");
    }

    // === Delete ===
    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedCourier == null)
        {
            MessageBox.Show("Выберите курьера для удаления.");
            return;
        }

        await _courierService.DeleteCourierAsync(SelectedCourier.Id);
        Couriers.Remove(SelectedCourier);
        SelectedCourier = null;
    }

    // === Save ===
    [RelayCommand]
    private async Task Save()
    {
        try
        {
            await _courierService.SaveCouriersAsync(Couriers);
            MessageBox.Show("Изменения успешно сохранены!");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}");
        }
    }
}
