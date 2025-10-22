
using System.ComponentModel.DataAnnotations;


namespace Infrastructure.Entities;

public class StockMovementEntity
{
    [Key]
    public int Id { get; set; }

    public DateTime MovementDate { get; set; }
     
    public int ItemCount { get; set; }

    public string MovementType { get; set; } =null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

}
