namespace Infrastructure.Dtos;

public sealed record CustomerOrderAllowance(decimal? BuyLimit, decimal Balance, bool CanExceedBuyLimit = false,
    decimal? AvailableRestriction = null)
{
    public decimal Prepayment => Math.Max(0m, -Balance);
    public decimal? MaximumTotal
    {
        get
        {
            if (CanExceedBuyLimit) return null;
            decimal? orderLimit = BuyLimit is > 0 ? BuyLimit.Value + Prepayment : null;
            if (!AvailableRestriction.HasValue) return orderLimit;
            var available = Math.Max(0m, AvailableRestriction.Value);
            return orderLimit.HasValue ? Math.Min(orderLimit.Value, available) : available;
        }
    }
    public decimal? Remaining(decimal basketTotal) => MaximumTotal.HasValue
        ? Math.Max(0m, MaximumTotal.Value - basketTotal)
        : null;
    public decimal RemainingPrepayment(decimal basketTotal) => Math.Max(0m, Prepayment - basketTotal);
    public bool IsExceeded(decimal basketTotal) => MaximumTotal.HasValue && basketTotal > MaximumTotal.Value;
}
