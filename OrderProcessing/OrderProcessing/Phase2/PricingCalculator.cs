namespace OrderProcessing.Phase2;

public static class PricingCalculator
{
    public static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static int LoyaltyPercent(LoyaltyTier tier) => tier switch
    {
        LoyaltyTier.Silver => 5,
        LoyaltyTier.Gold => 10,
        _ => 0
    };

    public static int DiscountPercent(LoyaltyTier tier, Coupon? coupon)
    {
        var percent = LoyaltyPercent(tier) + (coupon?.DiscountPercent ?? 0);
        return Math.Min(percent, 30);
    }

    public static decimal DiscountAmount(decimal subTotal, int discountPercent) =>
        Round(subTotal * discountPercent / 100m);

    public static decimal ShippingCost(decimal subTotal, int totalQuantity, decimal totalWeightKg)
    {
        if (subTotal >= 1000m)
            return 0m;

        return Round(5.00m + 1.50m * totalQuantity + 2.00m * totalWeightKg);
    }

    public static decimal Total(decimal subTotal, decimal discount, decimal shipping) =>
        Round(subTotal - discount + shipping);
}
