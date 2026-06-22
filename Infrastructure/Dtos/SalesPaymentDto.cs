

namespace Infrastructure.Dtos;

public class SalesPaymentDto
{
    public string CustomerCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Region { get; set; } = "";
    public string Firma { get; set; } = "";

    public DateTime OrderDate { get; set; }
    public DateTime PaymentDate { get; set; }

    public decimal Sales { get; set; }
    public decimal Payments { get; set; }
}