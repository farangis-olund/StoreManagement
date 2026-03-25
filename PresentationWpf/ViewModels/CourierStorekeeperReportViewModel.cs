using CommunityToolkit.Mvvm.ComponentModel;
using Infrastructure.Dtos;
using Infrastructure.Services;
using PresentationWpf.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentationWpf.ViewModels;

public partial class CourierStorekeeperReportViewModel : ObservableObject
{
    private readonly ReportService _reportService;
    private readonly UserSessionService _userSession;

    public ObservableCollection<CourierStorekeeperReportDto> Couriers { get; } = [];
    public ObservableCollection<CourierStorekeeperReportDto> Storekeepers { get; } = [];

    public CourierStorekeeperReportViewModel(
        ReportService reportService,
        UserSessionService userSession)
    {
        _reportService = reportService;
        _userSession = userSession;

        _ = LoadAsync();
    }

    [ObservableProperty]
    private DateTime? fromDate;

    [ObservableProperty]
    private DateTime? toDate;

    [ObservableProperty]
    private decimal percent = 1;

    partial void OnFromDateChanged(DateTime? value) => _ = LoadAsync();
    partial void OnToDateChanged(DateTime? value) => _ = LoadAsync();
    partial void OnPercentChanged(decimal value) => _ = LoadAsync();

    private async Task LoadAsync()
    {
        var result = await _reportService
            .GetCourierStorekeeperReportAsync(FromDate, ToDate, Percent);

        Couriers.Clear();
        Storekeepers.Clear();

        foreach (var c in result.couriers)
            Couriers.Add(c);

        foreach (var s in result.storekeepers)
            Storekeepers.Add(s);
    }
}