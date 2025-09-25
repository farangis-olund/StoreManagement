

namespace Infrastructure.Dtos;

public class MovementChangeDto
{
    public string Article { get; set; } = "";
    public int Quantity { get; set; }
    public string Type { get; set; } = "";   // "Приход" or "Расход"
    public int Total { get; set; }           // new stock after change
    public string Location { get; set; } = "";
}
