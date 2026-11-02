
namespace OrderProcessing.Phase1;

public class OrderValidator
{
    private readonly CustomerRepository _customers;
    private readonly ProductRepository _products;
    private readonly CouponRepository _coupons;
    private readonly OrderRepository _orders;

    public OrderValidator(
        CustomerRepository customers,
        ProductRepository products,
        CouponRepository coupons,
        OrderRepository orders)
    {
        _customers = customers;
        _products = products;
        _coupons = coupons;
        _orders = orders;
    }

    public async Task<bool> ValidateOrderAsync(Order order, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(order.CustomerId, ct);
        if (customer is null)
            return await RejectAsync(order, $"Customer #{order.CustomerId} not found", ct);

        if (!customer.IsActive)
            return await RejectAsync(order, "Customer is inactive", ct);

        var products = new Dictionary<int, Product>();
        foreach (var item in order.Items)
        {
            var product = await _products.GetByIdAsync(item.ProductId, ct);
            if (product is null)
                return await RejectAsync(order, $"Product #{item.ProductId} not found", ct);

            products[item.ProductId] = product;
        }

        foreach (var item in order.Items)
        {
            var product = products[item.ProductId];
            if (product.StockQuantity < item.Quantity)
                return await RejectAsync(order, $"Insufficient stock for '{product.Name}'", ct);
        }

        if (order.CouponCode is not null)
        {
            var coupon = await _coupons.GetByCodeAsync(order.CouponCode, ct);

            if (coupon is null)
                return await RejectAsync(order, $"Coupon '{order.CouponCode}' does not exist", ct);

            if (coupon.ExpiryDate < DateTime.Today)
                return await RejectAsync(order, $"Coupon '{order.CouponCode}' has expired", ct);
        }

        order.Status = OrderStatus.Validated;
        order.FailureReason = null;
        await _orders.SaveAsync(order, ct);

        Log.Success($"OrderValidator -> Order #{order.Id} validated");
        return true;
    }

    private async Task<bool> RejectAsync(Order order, string reason, CancellationToken ct)
    {
        Log.Error($"OrderValidator -> Rejecting Order #{order.Id}: {reason}");
        order.Status = OrderStatus.Rejected;
        order.FailureReason = reason;
        await _orders.SaveAsync(order, ct);
        return false;
    }
}
