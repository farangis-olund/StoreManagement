

namespace Infrastructure.Dtos;

public class PendingOrderDto
{
    public string Invoice { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public bool IsSent { get; set; }
}
