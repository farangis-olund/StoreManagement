

namespace Infrastructure.Dtos;

public sealed class InactivesSummaryRowDto
{
    public string ClientCode { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime? MaxDate { get; set; }
    public decimal Balance { get; set; }
    public double Restriction { get; set; } = 0;

}

