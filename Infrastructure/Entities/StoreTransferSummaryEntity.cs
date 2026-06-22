
namespace Infrastructure.Entities;

public class StoreTransferSummaryEntity
{
    public int Id { get; set; }

    public DateTime Date { get; set; } = DateTime.Now;

    public string StoreCode { get; set; } = "";

    public int TotalQuantity { get; set; }

    public decimal TotalAmount { get; set; }
    public int UserId { get; set; }

    public UserEntity? User { get; set; }
}