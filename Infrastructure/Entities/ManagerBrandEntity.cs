using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Entities;

public class ManagerBrandEntity
{
	public string ManagerId { get; set; } = null!;
	
	public int BrandId { get; set; } 

	public double SalesPercentage { get; set; }  // % для продажи

	[ForeignKey(nameof(ManagerId))]
	public virtual SalesManagerEntity Manager { get; set; } = null!;

	[ForeignKey(nameof(BrandId))]
	public virtual BrandEntity Brand { get; set; } = null!;
}
