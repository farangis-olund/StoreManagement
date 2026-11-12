namespace Infrastructure.Entities;

public sealed class OfficialMonthlyPlanSettingsEntity
{
    public int Id { get; set; } = 1;
    public decimal NonOfficialThreshold { get; set; }
}
