

namespace Infrastructure.Dtos;

public class TotalSalesReportRowDto
{
    public string Name { get; set; } = "";   // ПРОДАЖА, ВОЗВРАТ, etc.

    public decimal Euro { get; set; }        // EUR value
    public decimal Smn { get; set; }         // SMN (Tajik currency)

    // Optional (for styling TOTAL row)
    public bool IsTotal => Name == "ИТОГО";
}