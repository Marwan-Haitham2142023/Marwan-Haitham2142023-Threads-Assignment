
namespace OrderProcessing.Phase1;

public class SyncOrderValidator
{
    private readonly SyncCustomerRepository _customers;
    private readonly SyncProductRepository _products;
    private readonly SyncCouponRepository _coupons;

    public SyncOrderValidator(
        SyncCustomerRepository customers,
        SyncProductRepository products,
        SyncCouponRepository coupons)
    {
        _customers = customers;
        _products = products;
        _coupons = coupons;
    }

    public bool ValidateOrder(Order order)
    {
        var customer = _customers.GetById(order.CustomerId);
        if (customer is null)
            return Reject(order, $"Customer #{order.CustomerId} not found");

        if (!customer.IsActive)
            return Reject(order, "Customer is inactive");

        var products = new Dictionary<int, Product>();
        foreach (var item in order.Items)
        {
            var product = _products.GetById(item.ProductId);
            if (product is null)
                return Reject(order, $"Product #{item.ProductId} not found");

            products[item.ProductId] = product;
        }

        foreach (var item in order.Items)
        {
            var product = products[item.ProductId];
            if (product.StockQuantity < item.Quantity)
                return Reject(order, $"Insufficient stock for '{product.Name}'");
        }

        if (order.CouponCode is not null)
        {
            var coupon = _coupons.GetByCode(order.CouponCode);

            if (coupon is null)
                return Reject(order, $"Coupon '{order.CouponCode}' does not exist");

            if (coupon.ExpiryDate < DateTime.Today)
                return Reject(order, $"Coupon '{order.CouponCode}' has expired");
        }

        order.Status = OrderStatus.Validated;
        order.FailureReason = null;
        Log.Success($"SyncOrderValidator -> Order #{order.Id} validated");
        return true;
    }

    private static bool Reject(Order order, string reason)
    {
        Log.Error($"SyncOrderValidator -> Rejecting Order #{order.Id}: {reason}");
        order.Status = OrderStatus.Rejected;
        order.FailureReason = reason;
        return false;
    }
}
