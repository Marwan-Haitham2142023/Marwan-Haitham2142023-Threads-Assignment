
namespace OrderProcessing.Phase2;

public class OrderPricer
{
    private readonly PricingService _pricing;

    public OrderPricer(PricingService pricing)
    {
        _pricing = pricing;
    }

    public async Task PriceOrderAsync(Order order, CancellationToken ct)
    {
        order.SubTotal = await _pricing.CalculateSubTotalAsync(order, ct);

        var discountTask = _pricing.GetDiscountAsync(order, ct);
        var shippingTask = _pricing.CalculateShippingCostAsync(order, ct);

        await Task.WhenAll(discountTask, shippingTask);

        Finish(order, await discountTask, await shippingTask);
    }

    public async Task PriceOrderSequentialAsync(Order order, CancellationToken ct)
    {
        order.SubTotal = await _pricing.CalculateSubTotalAsync(order, ct);

        var discount = await _pricing.GetDiscountAsync(order, ct);
        var shipping = await _pricing.CalculateShippingCostAsync(order, ct);

        Finish(order, discount, shipping);
    }

    private static void Finish(Order order, decimal discount, decimal shipping)
    {
        order.Discount = discount;
        order.ShippingCost = shipping;
        order.TotalAmount = PricingCalculator.Total(order.SubTotal, order.Discount, order.ShippingCost);

        Log.Success($"OrderPricer -> Order #{order.Id}: SubTotal {order.SubTotal:F2}, Discount {order.Discount:F2}, Shipping {order.ShippingCost:F2}, Total {order.TotalAmount:F2}");
    }
}
