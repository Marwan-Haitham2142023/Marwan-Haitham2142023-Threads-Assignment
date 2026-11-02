
namespace OrderProcessing.Phase2;

public class PricingService
{
    private readonly CustomerRepository _customers;
    private readonly CouponRepository _coupons;
    private readonly JsonFileStore _store = new();
    private readonly HashSet<int> _failShippingForOrderIds;

    public PricingService(
        CustomerRepository customers,
        CouponRepository coupons,
        IEnumerable<int> failShippingForOrderIds)
    {
        _customers = customers;
        _coupons = coupons;
        _failShippingForOrderIds = new HashSet<int>(failShippingForOrderIds);
    }

    public async Task<decimal> CalculateSubTotalAsync(Order order, CancellationToken ct)
    {
        Log.Info($"PricingService -> Opening connection for Order #{order.Id}...");
        await Task.Delay(200, ct);

        Log.Info($"PricingService -> Executing query for products of Order #{order.Id}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var products = await _store.ReadAllAsync<Product>(DataFiles.Products, ct);

        decimal subTotal = 0m;
        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId)
                ?? throw new InvalidOperationException($"Product #{item.ProductId} not found");

            item.UnitPrice = PricingCalculator.Round(product.Price);
            subTotal += item.UnitPrice * item.Quantity;
        }

        subTotal = PricingCalculator.Round(subTotal);
        Log.Success($"PricingService -> Order #{order.Id} SubTotal is {subTotal:F2}");
        return subTotal;
    }

    public async Task<decimal> GetDiscountAsync(Order order, CancellationToken ct)
    {
        Log.Info($"DiscountApi -> DNS/handshake for Order #{order.Id}...");
        await Task.Delay(150, ct);

        Log.Info($"DiscountApi -> Sending request for Order #{order.Id}...");
        await Task.Delay(300, ct);

        Log.Info($"DiscountApi -> Waiting for response for Order #{order.Id}...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        var customer = await _customers.GetByIdAsync(order.CustomerId, ct)
            ?? throw new InvalidOperationException($"Customer #{order.CustomerId} not found");

        Coupon? coupon = null;
        if (order.CouponCode is not null)
        {
            coupon = await _coupons.GetByCodeAsync(order.CouponCode, ct)
                ?? throw new InvalidCouponException($"Coupon '{order.CouponCode}' does not exist");

            if (coupon.ExpiryDate < DateTime.Today)
                throw new InvalidCouponException($"Coupon '{order.CouponCode}' has expired");
        }

        var percent = PricingCalculator.DiscountPercent(customer.LoyaltyTier, coupon);
        var discount = PricingCalculator.DiscountAmount(order.SubTotal, percent);

        Log.Success($"DiscountApi -> Order #{order.Id} discount is {discount:F2} ({percent}%)");
        return discount;
    }

    public async Task<decimal> CalculateShippingCostAsync(Order order, CancellationToken ct)
    {
        Log.Info($"ShippingApi -> DNS/handshake for Order #{order.Id}...");
        await Task.Delay(150, ct);

        if (_failShippingForOrderIds.Contains(order.Id))
        {
            Log.Error($"ShippingApi -> Order #{order.Id}: Shipping provider unavailable");
            throw new ShippingProviderUnavailableException("Shipping provider unavailable");
        }

        Log.Info($"ShippingApi -> Sending request for Order #{order.Id}...");
        await Task.Delay(300, ct);

        Log.Info($"ShippingApi -> Waiting for response for Order #{order.Id}...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        var products = await _store.ReadAllAsync<Product>(DataFiles.Products, ct);

        decimal totalWeight = 0m;
        int totalQuantity = 0;
        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId)
                ?? throw new InvalidOperationException($"Product #{item.ProductId} not found");

            totalWeight += product.WeightKg * item.Quantity;
            totalQuantity += item.Quantity;
        }

        var cost = PricingCalculator.ShippingCost(order.SubTotal, totalQuantity, totalWeight);

        Log.Success($"ShippingApi -> Order #{order.Id} shipping is {cost:F2}");
        return cost;
    }

    public async Task<decimal> CalculateTotalAsync(Order order, CancellationToken ct)
    {
        order.SubTotal = await CalculateSubTotalAsync(order, ct);

        var discountTask = GetDiscountAsync(order, ct);
        var shippingTask = CalculateShippingCostAsync(order, ct);

        await Task.WhenAll(discountTask, shippingTask);

        order.Discount = await discountTask;
        order.ShippingCost = await shippingTask;
        order.TotalAmount = PricingCalculator.Total(order.SubTotal, order.Discount, order.ShippingCost);

        Log.Success($"PricingService -> Order #{order.Id}: SubTotal {order.SubTotal:F2}, Discount {order.Discount:F2}, Shipping {order.ShippingCost:F2}, Total {order.TotalAmount:F2}");
        return order.TotalAmount;
    }
}
