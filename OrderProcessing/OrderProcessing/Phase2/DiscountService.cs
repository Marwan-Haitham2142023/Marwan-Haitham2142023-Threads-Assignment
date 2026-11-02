
namespace OrderProcessing.Phase2;

public class DiscountService
{
    public async Task<int> GetDiscountPercentAsync(Order order, Customer customer, Coupon? coupon, CancellationToken ct)
    {
        Log.Info($"DiscountService -> DNS/handshake for Order #{order.Id}...");
        await Task.Delay(150, ct);

        Log.Info($"DiscountService -> Sending request for Order #{order.Id}...");
        await Task.Delay(300, ct);

        Log.Info($"DiscountService -> Waiting for response for Order #{order.Id}...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        var percent = PricingCalculator.DiscountPercent(customer.LoyaltyTier, coupon);

        Log.Success($"DiscountService -> Order #{order.Id} discount is {percent}%");
        return percent;
    }
}
