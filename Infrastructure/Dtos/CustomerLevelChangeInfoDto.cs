

namespace Infrastructure.Dtos;

public class CustomerLevelChangeInfoDto
{
    public int CurrentLevelCode { get; set; }
    public int CalculatedLevelCode { get; set; }

    public string? CurrentPriceType { get; set; }
    public string? CalculatedPriceType { get; set; }

    public decimal Last30DaysTotal { get; set; }

    public bool IsUp => CalculatedLevelCode > CurrentLevelCode;
    public bool IsDown => CalculatedLevelCode < CurrentLevelCode;
    public bool IsSame => CalculatedLevelCode == CurrentLevelCode;
}