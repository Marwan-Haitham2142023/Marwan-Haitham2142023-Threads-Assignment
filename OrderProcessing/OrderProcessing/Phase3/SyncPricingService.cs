
namespace OrderProcessing.Phase3;

public class SyncPricingService
{
    private readonly SyncCustomerRepository _customers;
    private readonly SyncCouponRepository _coupons;
    private readonly JsonFileStore _store = new();

    public SyncPricingService(SyncCustomerRepository customers, SyncCouponRepository coupons)
    {
        _customers = customers;
        _coupons = coupons;
    }

    public decimal CalculateTotal(Order order)
    {
        Log.Info($"SyncPricingService -> Opening connection for Order #{order.Id}...");
        Thread.Sleep(200);

        Log.Info($"SyncPricingService -> Executing query for products of Order #{order.Id}...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        var products = _store.ReadAll<Product>(DataFiles.Products);

        decimal subTotal = 0m;
        decimal totalWeight = 0m;
        int totalQuantity = 0;
        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId)
                ?? throw new InvalidOperationException($"Product #{item.ProductId} not found");

            item.UnitPrice = PricingCalculator.Round(product.Price);
            subTotal += item.UnitPrice * item.Quantity;
            totalWeight += product.WeightKg * item.Quantity;
            totalQuantity += item.Quantity;
        }

        order.SubTotal = PricingCalculator.Round(subTotal);

        Log.Info($"SyncDiscountApi -> DNS/handshake for Order #{order.Id}...");
        Thread.Sleep(150);
        Log.Info($"SyncDiscountApi -> Sending request for Order #{order.Id}...");
        Thread.Sleep(300);
        Log.Info($"SyncDiscountApi -> Waiting for response for Order #{order.Id}...");
        Thread.Sleep(Random.Shared.Next(500, 1501));

        var customer = _customers.GetById(order.CustomerId)
            ?? throw new CustomerNotFoundException($"Customer #{order.CustomerId} not found");

        Coupon? coupon = null;
        if (order.CouponCode is not null)
        {
            coupon = _coupons.GetByCode(order.CouponCode)
                ?? throw new InvalidCouponException($"Coupon '{order.CouponCode}' does not exist");

            if (coupon.ExpiryDate < DateTime.Today)
                throw new InvalidCouponException($"Coupon '{order.CouponCode}' has expired");
        }

        var percent = PricingCalculator.DiscountPercent(customer.LoyaltyTier, coupon);
        order.Discount = PricingCalculator.DiscountAmount(order.SubTotal, percent);

        Log.Info($"SyncShippingApi -> DNS/handshake for Order #{order.Id}...");
        Thread.Sleep(150);
        Log.Info($"SyncShippingApi -> Sending request for Order #{order.Id}...");
        Thread.Sleep(300);
        Log.Info($"SyncShippingApi -> Waiting for response for Order #{order.Id}...");
        Thread.Sleep(Random.Shared.Next(500, 1501));

        order.ShippingCost = PricingCalculator.ShippingCost(order.SubTotal, totalQuantity, totalWeight);
        order.TotalAmount = PricingCalculator.Total(order.SubTotal, order.Discount, order.ShippingCost);

        Log.Success($"SyncPricingService -> Order #{order.Id}: SubTotal {order.SubTotal:F2}, Discount {order.Discount:F2}, Shipping {order.ShippingCost:F2}, Total {order.TotalAmount:F2}");
        return order.TotalAmount;
    }
}
