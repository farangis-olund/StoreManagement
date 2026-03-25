

namespace Infrastructure.Dtos;

public class SalesManagerReportRow
{
    public string ManagerId { get; set; } = null!;
    public string ManagerName { get; set; } = null!;
    public Dictionary<string, CompanySalesInfo> CompanyData { get; set; } = new();
    public decimal TotalReturnAmount { get; set; } = 0m;
    public Dictionary<string, decimal> CompanyReturnData { get; set; } = new();
}

public class CompanySalesInfo
{
    public decimal SalesAmount { get; set; }
    public decimal ReturnAmount { get; set; }
    public double Percentage { get; set; }
    public decimal CommissionAmount { get; set; }

    public decimal NetSalesAmount => SalesAmount - ReturnAmount;
}