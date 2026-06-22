

namespace Infrastructure.Dtos;

public class SalesByGroupCustomerDto
{
    public string? ProductGroup { get; set; }
    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    public string? Firma { get; set; }     
    public string? Region { get; set; }    
    public decimal Total { get; set; }
    public decimal Quantity { get; set; }
}