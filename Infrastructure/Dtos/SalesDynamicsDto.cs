namespace Infrastructure.Dtos;

public class SalesDynamicsDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string? Region { get; set; }

    public decimal Total { get; set; }

    public string? Firma { get; set; }
}
