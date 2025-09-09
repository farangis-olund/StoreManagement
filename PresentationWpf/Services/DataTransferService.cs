
using Infrastructure.Dtos;

namespace PresentationWpf.Services;

public class DataTransferService
{
	public CustomerOrder SelectedOrder { get; set; } = null!;
	public string CustomerId { get; set; }= null!;
	public string? SelectedCustomerIdForReturn { get; set; }
}
