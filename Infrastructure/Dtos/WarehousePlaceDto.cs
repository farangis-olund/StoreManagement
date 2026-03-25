

namespace Infrastructure.Dtos;

public class WarehousePlaceDto
{
    public string PlaceCode { get; set; } = null!;   // A12/7
    public string Article { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Mark { get; set; } = null!;
    public string Model { get; set; } = null!;
    public int Quantity { get; set; }

}