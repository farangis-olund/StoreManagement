
using System.ComponentModel.DataAnnotations;


namespace Infrastructure.Entities;

public class StockImportErrorEntity
{
    [Key]
    public int Id { get; set; }

    public string ArticleNumber { get; set; } = null!;

    public int Quantity { get; set; }
        
   

}