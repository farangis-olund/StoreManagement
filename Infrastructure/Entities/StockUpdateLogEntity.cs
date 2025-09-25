namespace Infrastructure.Entities;

public class StockUpdateLogEntity
{
    public int Id { get; set; }
    public DateTime UpdateDate { get; set; }           // the business date that was posted
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Comment { get; set; }
}