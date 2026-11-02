
namespace OrderProcessing.Phase2;

public class ShippingService
{
    public async Task<decimal> GetShippingCostAsync(Order order, decimal subTotal, int totalQuantity, decimal totalWeightKg, CancellationToken ct)
    {
        Log.Info($"ShippingService -> DNS/handshake for Order #{order.Id}...");
        await Task.Delay(150, ct);

        Log.Info($"ShippingService -> Sending request for Order #{order.Id}...");
        await Task.Delay(300, ct);

        Log.Info($"ShippingService -> Waiting for response for Order #{order.Id}...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        var cost = PricingCalculator.ShippingCost(subTotal, totalQuantity, totalWeightKg);

        Log.Success($"ShippingService -> Order #{order.Id} shipping is {cost:F2}");
        return cost;
    }
}
