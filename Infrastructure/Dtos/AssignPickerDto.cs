

namespace Infrastructure.Dtos;

public class AssignPickerDto
{
    public string OrderId { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public DateTime Date { get; set; }

    public string? PickerId { get; set; }     // ✅ StorekeeperId
    public string? PickerName { get; set; }   // ✅ FullName
}
