namespace Infrastructure.Dtos;

public sealed record OfficialMonthlyPlanSummary(string CustomerId, string CustomerName, decimal Amount, decimal Purchased,
    decimal NonOfficialThreshold = 0)
{
    public decimal Remaining => CalculateRemaining(Amount, Purchased, NonOfficialThreshold);

    public static decimal CalculateRemaining(decimal amount, decimal purchased, decimal threshold)
    {
        var remaining = Math.Max(0m, amount - purchased);
        return remaining > threshold ? remaining : 0m;
    }
}

public sealed record OfficialMonthlyPlanSetting(string CustomerId, string CustomerName, decimal? Amount);

public sealed record OfficialMonthlyPlanRetailState(DateTime Month, string? CustomerId, bool OfficialOrder,
    decimal? IndividualRemaining, decimal? TotalRemaining, decimal? OfficialOrderLimit = null);
