namespace Infrastructure.Entities;

public sealed class OfficialMonthlyPlanEntity
{
    public string CustomerId { get; set; } = "";
    public DateTime Month { get; set; }
    public decimal Amount { get; set; }
    public CustomerEntity Customer { get; set; } = null!;
}
