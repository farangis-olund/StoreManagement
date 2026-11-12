
namespace Infrastructure.Dtos;

public sealed class UnpaidOrderDto
{
    public string Id { get; set; } = "";
    public DateTime Date { get; set; }
    public string CustomerId { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal Paid { get; set; }
    public int? PaymentId { get; set; } 
    public string ReceiptNumber { get; set; } = "";
    public string DocumentNumber => !string.IsNullOrEmpty(ReceiptNumber) ? ReceiptNumber : PaymentId?.ToString() ?? Id;

}
