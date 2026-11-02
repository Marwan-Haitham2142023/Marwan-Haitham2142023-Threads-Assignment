using System.Diagnostics;

namespace OrderProcessing.Phase3;

public class SyncOrderProcessor
{
    private readonly SyncCustomerRepository _customers;
    private readonly SyncInventoryService _inventory;
    private readonly SyncPricingService _pricing;
    private readonly SyncPaymentGateway _payments;
    private readonly SyncOrderRepository _orders;
    private readonly SyncEmailService _email;

    public SyncOrderProcessor(
        SyncCustomerRepository customers,
        SyncInventoryService inventory,
        SyncPricingService pricing,
        SyncPaymentGateway payments,
        SyncOrderRepository orders,
        SyncEmailService email)
    {
        _customers = customers;
        _inventory = inventory;
        _pricing = pricing;
        _payments = payments;
        _orders = orders;
        _email = email;
    }

    public bool ProcessOrder(Order order)
    {
        var stockReserved = false;

        try
        {
            var sw = Stopwatch.StartNew();

            var customer = _customers.GetById(order.CustomerId);
            if (customer is null)
            {
                Log.Error($"Order #{order.Id} -> Step 1/6 Customer ... NOT FOUND in {sw.ElapsedMilliseconds} ms");
                return Stop(order, OrderStatus.Rejected, $"Customer #{order.CustomerId} not found");
            }

            if (!customer.IsActive)
            {
                Log.Error($"Order #{order.Id} -> Step 1/6 Customer ... INACTIVE in {sw.ElapsedMilliseconds} ms");
                return Stop(order, OrderStatus.Rejected, "Customer is inactive");
            }

            Log.Success($"Order #{order.Id} -> Step 1/6 Customer ... OK ({customer.Name}) in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            if (!_inventory.ReserveStock(order, out var shortageName))
            {
                Log.Error($"Order #{order.Id} -> Step 2/6 Inventory ... INSUFFICIENT STOCK in {sw.ElapsedMilliseconds} ms");
                return Stop(order, OrderStatus.Rejected, $"Insufficient stock for '{shortageName ?? "unknown product"}'");
            }

            stockReserved = true;
            Log.Success($"Order #{order.Id} -> Step 2/6 Inventory ... OK in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            try
            {
                _pricing.CalculateTotal(order);
            }
            catch (InvalidCouponException ex)
            {
                Log.Error($"Order #{order.Id} -> Step 3/6 Pricing ... INVALID COUPON in {sw.ElapsedMilliseconds} ms, released");
                _inventory.ReleaseStock(order);
                stockReserved = false;
                return Stop(order, OrderStatus.Rejected, ex.Message);
            }

            Log.Success($"Order #{order.Id} -> Step 3/6 Pricing ... OK (total {order.TotalAmount:F2}) in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            var payment = _payments.ProcessPayment(order.Id, order.TotalAmount);
            if (!payment.Success)
            {
                Log.Error($"Order #{order.Id} -> Step 4/6 Payment ... DECLINED in {sw.ElapsedMilliseconds} ms, released");
                _inventory.ReleaseStock(order);
                stockReserved = false;
                return Stop(order, OrderStatus.PaymentFailed, payment.Message ?? "Card declined by issuer");
            }

            Log.Success($"Order #{order.Id} -> Step 4/6 Payment ... OK ({payment.TransactionId}) in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            order.TransactionId = payment.TransactionId;
            order.Status = OrderStatus.Completed;
            order.FailureReason = null;
            _orders.Save(order);
            Log.Success($"Order #{order.Id} -> Step 5/6 Save ... OK in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            try
            {
                _email.SendConfirmation(order, customer);
                Log.Success($"Order #{order.Id} -> Step 6/6 Email ... DONE in {sw.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                Log.Error($"Order #{order.Id} -> Step 6/6 Email ... FAILED ({ex.Message}) in {sw.ElapsedMilliseconds} ms");
            }

            return order.Status == OrderStatus.Completed;
        }
        catch (Exception ex)
        {
            if (stockReserved)
                _inventory.ReleaseStock(order);

            order.Status = OrderStatus.Failed;
            order.FailureReason = ex.Message;
            _orders.Save(order);

            Log.Error($"Order #{order.Id} -> Failed: {ex.Message}");
            return false;
        }
    }

    private bool Stop(Order order, OrderStatus status, string reason)
    {
        order.Status = status;
        order.FailureReason = reason;
        _orders.Save(order);
        return false;
    }
}
