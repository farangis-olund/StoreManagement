

namespace Infrastructure.Dtos;

public class CourierStorekeeperReportDto
{
    public string Name { get; set; } = "";

    public decimal OrdersSum { get; set; }

    public decimal ReturnSum { get; set; }

    public decimal NetSum { get; set; }

    public decimal PercentAmount { get; set; }
}