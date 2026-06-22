

namespace Infrastructure.Dtos;

public class ShopBonusRowDto
{
    public DateTime Date { get; set; }

    public int TotalQuantity { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal BonusAmount { get; set; }

    public int UserId { get; set; }
    public string UserFullName { get; set; } = "";
}