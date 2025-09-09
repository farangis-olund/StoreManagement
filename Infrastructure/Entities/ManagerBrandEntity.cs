namespace Infrastructure.Entities;

public class ManagerBrandEntity
{
	public string ManagerId { get; set; } = null!;
	public SalesManagerEntity Manager { get; set; } = null!;

	public string Brand { get; set; } = null!;

	public double SalesPercentage { get; set; }  // % для продажи
}
