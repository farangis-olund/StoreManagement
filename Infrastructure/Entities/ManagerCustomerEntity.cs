
namespace Infrastructure.Entities;

public class ManagerCustomerEntity
{
	public string ManagerId { get; set; } = null!;

	public string CustomerId { get; set; } = null!;
		

	// Navigation properties
	public SalesManagerEntity Manager { get; set; } = null!;
	public CustomerEntity Customer { get; set; } = null!;
}
