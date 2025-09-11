
namespace Infrastructure.Dtos;

public class CustomerFinanceInfo
{
	public decimal CurrentSale { get; set; }
	public decimal CurrentPayment { get; set; }
	public decimal PreviousPayments { get; set; }
	public decimal CustomerDebt { get; set; }
	public decimal CreditLimit { get; set; }
	public decimal TotalSales { get; set; }
	public decimal TotalReturns { get; set; }
	public decimal OldDebt { get; set; }
	public decimal Balance { get; set; }
	public DateTime? ContractDate { get; set; }
}
