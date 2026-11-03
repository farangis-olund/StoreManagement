namespace Infrastructure.Dtos;

public sealed record CustomerOrderAllowance(decimal? BuyLimit, decimal Balance, bool CanExceedBuyLimit = false)
{
    public decimal Prepayment => Math.Max(0m, -Balance);
    public decimal? MaximumTotal => !CanExceedBuyLimit && BuyLimit is > 0 ? BuyLimit.Value + Prepayment : null;
    public decimal? Remaining(decimal basketTotal) => MaximumTotal.HasValue
        ? Math.Max(0m, MaximumTotal.Value - basketTotal)
        : null;
    public decimal RemainingPrepayment(decimal basketTotal) => Math.Max(0m, Prepayment - basketTotal);
    public bool IsExceeded(decimal basketTotal) => MaximumTotal.HasValue && basketTotal > MaximumTotal.Value;
}
