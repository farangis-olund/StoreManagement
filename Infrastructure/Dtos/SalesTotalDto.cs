

namespace Infrastructure.Dtos;

public class SalesTotalDto
{
    public string Region { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public decimal Total { get; set; }
}
