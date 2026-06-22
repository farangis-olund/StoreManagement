using CommunityToolkit.Mvvm.ComponentModel;

namespace PresentationWpf.Dtos;

public partial class CustomerLevelReviewDto : ObservableObject
{
    [ObservableProperty]
    private bool isSelected;

    public string CustomerId { get; set; } = "";
    public string FullName { get; set; } = "";
    public decimal CurrentMonthTotal { get; set; }

    public string CurrentLevel { get; set; } = "";
    public int CurrentLevelCode { get; set; }
    public string? CurrentLevelId { get; set; }

    public string SuggestedLevel { get; set; } = "";
    public int SuggestedLevelCode { get; set; }
    public string? SuggestedLevelId { get; set; }
}