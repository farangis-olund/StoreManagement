
namespace Infrastructure.Dtos;

public class OrderWithPaymentsDto
{
    public string OrderId { get; set; } = "";
    public DateTime Date { get; set; }

    public string CustomerId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Address { get; set; } = "";

    public bool IsPaid { get; set; }

    public decimal SaleAmount { get; set; }     // сумма заказа (из деталей)
    public decimal PaymentAmount { get; set; }  // сумма платежей по заказу
}
