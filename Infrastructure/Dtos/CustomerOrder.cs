
namespace Infrastructure.Dtos;

public class CustomerOrder
{
	public string Id { get; set; } = null!;
	public DateTime Date { get; set; } 
	public double Rate { get; set; }
	public string? CustomerFullName { get; set; }
	public string? CustomerLevel { get; set; }
	public string? CustomerAddress { get; set; }
	public string? CustomerPhoneNumber { get; set; }
	public string UserFullName { get; set; } = null!;
	public bool WithoutInvoice { get; set; } = false;
	public bool DirectFromStock { get; set; }
	public bool Stock { get; set; }
	public string SuminWords { get; set; } = null!;
	public decimal TotalAmount { get; set; }
	public bool IsBarter { get; set; }
	public string? CourierId { get; set; }       

	public string? StorekeeperId { get; set; }            // Складчик
	public Customer? Customer { get; set; }

	public List<OrderDetail> OrderDetails { get; set; } = [];
}
