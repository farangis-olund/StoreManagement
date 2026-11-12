using Infrastructure.Dtos;

internal static class OrderAllowanceTests
{
    public static void Run()
    {
        var allowance = new CustomerOrderAllowance(500, -1999);
        Check(allowance.Prepayment == 1999 && allowance.MaximumTotal == 2499, "Prepayment must extend the order limit.");
        foreach (var (basket, prepaidLeft, totalLeft) in new (decimal, decimal, decimal)[]
        {
            (0, 1999, 2499), (500, 1499, 1999), (1998, 1, 501),
            (1999, 0, 500), (2000, 0, 499), (2499, 0, 0)
        })
        {
            Check(!allowance.IsExceeded(basket), "Basket within prepayment plus limit must be allowed.");
            Check(allowance.RemainingPrepayment(basket) == prepaidLeft, "Prepayment must be used first.");
            Check(allowance.Remaining(basket) == totalLeft, "Incorrect remaining basket allowance.");
        }
        Check(allowance.IsExceeded(2499.01m) && allowance.Remaining(2499.01m) == 0, "Excess must be rejected.");
        Check(allowance.Remaining(2400) == 99, "Quantity rejection must show the amount available for cheaper products.");
        Check(allowance.Remaining(2200) == 299, "Removing products must release basket allowance.");
        Check(allowance.Balance == -1999 && allowance.BuyLimit == 500, "Basket calculations must not change stored values.");

        var nextOrder = new CustomerOrderAllowance(500, 0);
        Check(nextOrder.MaximumTotal == 500 && nextOrder.IsExceeded(500.01m), "Consumed prepayment must not be reused.");
        var remainingCredit = new CustomerOrderAllowance(500, -999);
        Check(remainingCredit.MaximumTotal == 1499, "Partial prepayment use must reduce the next basket's allowance.");
        var debt = new CustomerOrderAllowance(500, 1000);
        Check(debt.Prepayment == 0 && debt.MaximumTotal == 500, "Positive debt is not prepayment.");
        var fractional = new CustomerOrderAllowance(500.25m, -1999.10m);
        Check(fractional.MaximumTotal == 2499.35m && !fractional.IsExceeded(2499.35m), "Currency fractions must be exact.");
        foreach (var limit in new decimal?[] { null, 0, -1 })
        {
            var unlimited = new CustomerOrderAllowance(limit, -1999);
            Check(unlimited.MaximumTotal == null && unlimited.Remaining(10000) == null && !unlimited.IsExceeded(10000),
                "Disabled order limits must remain unlimited.");
        }
        Console.WriteLine("PASS: prepayment-first basket allowance, exact/exceeded boundaries, remaining amounts, removal, refreshed balance, and unlimited customers");

        var permissions = new TestPermissions();
        CustomerOrderAllowance CurrentAllowance(decimal balance) => new(500, balance,
            permissions.Has(Infrastructure.Constants.PermissionCatalog.OrdersExceedBuyLimit));
        Check(CurrentAllowance(-1999).IsExceeded(2500), "The override must not be granted by default.");
        permissions.Keys.Add(Infrastructure.Constants.PermissionCatalog.OrdersExceedBuyLimit);
        foreach (var balance in new[] { -1999m, 0m, 1000m })
        {
            var unrestricted = CurrentAllowance(balance);
            Check(unrestricted.MaximumTotal == null && !unrestricted.IsExceeded(10000) && unrestricted.Remaining(10000) == null,
                "Override permission must bypass basket amount limits with or without prepayment.");
            Check(unrestricted.BuyLimit == 500, "Override must not change the customer's saved limit.");
        }
        permissions.Keys.Clear();
        Check(CurrentAllowance(-1999).IsExceeded(2500) && CurrentAllowance(0).IsExceeded(501),
            "Revoking the permission must restore the order limit.");
        Console.WriteLine("PASS: explicit order-limit override, default denial, retained customer limit, and live revocation");

        foreach (var buyLimit in new decimal?[] { null, 0, -1, 500 })
        {
            var displayed = new CustomerOrderAllowance(buyLimit, 0, AvailableRestriction: 50);
            Check(displayed.MaximumTotal == 50 && !displayed.IsExceeded(50) && displayed.IsExceeded(50.01m),
                "The displayed restriction must be enforced even without a separate order limit.");
        }
        var smallerOrderLimit = new CustomerOrderAllowance(30, 0, AvailableRestriction: 50);
        Check(smallerOrderLimit.MaximumTotal == 30, "The smaller of the two limits must apply.");
        var prepaidRestriction = new CustomerOrderAllowance(500, -100, AvailableRestriction: 150);
        Check(prepaidRestriction.MaximumTotal == 150 && prepaidRestriction.IsExceeded(150.01m),
            "The displayed restriction already includes prepayment; it must not be added a second time.");
        foreach (var available in new[] { 0m, -50m })
        {
            var exhausted = new CustomerOrderAllowance(null, 0, AvailableRestriction: available);
            Check(exhausted.MaximumTotal == 0 && exhausted.IsExceeded(.01m),
                "Zero or negative available restriction must prevent further sales.");
        }
        var overrideRestriction = new CustomerOrderAllowance(30, 0, true, AvailableRestriction: 50);
        Check(overrideRestriction.MaximumTotal == null && !overrideRestriction.IsExceeded(1000),
            "The explicit override must bypass both limits.");
        Console.WriteLine("PASS: displayed restriction, optional order limit, no double-counted prepayment, exhausted credit and override");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
