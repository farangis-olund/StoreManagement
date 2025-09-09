
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Entities;

public class ExchangeRateEntity
{
	[Key]
	public DateTime Date { get; set; }
	public string Code { get; set; } = null!;
	public double Rate { get; set; }
	public virtual CurrencyEntity Currency { get; set; } = null!;

}
