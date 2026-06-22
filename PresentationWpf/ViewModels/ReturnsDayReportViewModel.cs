using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Entities;
using Infrastructure.Services;
using PresentationWpf.Services;
using System.Collections.ObjectModel;

namespace PresentationWpf.ViewModels;

public partial class ReturnsDayReportViewModel : ObservableObject
{
    private readonly ReturnsDayReportService _returnsDayReportService;
    private readonly ReturnService _returnService;

    public ObservableCollection<DateTime> ReturnDates { get; } = new();

    public ObservableCollection<ReturnEntity> Returns { get; } = new();

    [ObservableProperty]
    private DateTime? selectedReturnDate;

    public ReturnsDayReportViewModel(ReturnService returnService, ReturnsDayReportService returnsDayReportService)
    {
        _returnService = returnService;
        _ = LoadReturnDatesAsync();
        _returnsDayReportService = returnsDayReportService;
    }

    private async Task LoadReturnDatesAsync()
    {
        var dates = await _returnService.GetReturnDatesAsync();

        ReturnDates.Clear();

        foreach (var date in dates)
            ReturnDates.Add(date);
    }

    [RelayCommand]
    private async Task ShowReportAsync()
    {
        if (SelectedReturnDate == null)
            return;

        await _returnsDayReportService.ShowReturnsDayReportAsync(
            SelectedReturnDate.Value.Date);
    }
}