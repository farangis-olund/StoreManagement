using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Entities;

public class PriceLevelEntity
{
	[Key]
	public string Level { get; set; } = null!;        // Уровень (acts as a key or name)
	public string? Coefficient { get; set; }          // Коэффициент
	public double? MinLimit { get; set; }             // Ограничение_нач
	public double? MaxLimit { get; set; }             // Ограничение
	public int? Code { get; set; }                    // Code
	public string PriceType { get; set; } = null!;    // Тип цены

	public ICollection<CustomerEntity>? Customers { get; set; }

}
