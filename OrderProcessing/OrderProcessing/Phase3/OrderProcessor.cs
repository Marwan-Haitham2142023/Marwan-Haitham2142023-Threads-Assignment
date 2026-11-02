using System.Diagnostics;

namespace OrderProcessing.Phase3;

public class OrderProcessor
{
    private readonly CustomerService _customers;
    private readonly InventoryService _inventory;
    private readonly PricingService _pricing;
    private readonly IPaymentGateway _payments;
    private readonly OrderRepository _orders;
    private readonly EmailRetryService _email;

    public OrderProcessor(
        CustomerService customers,
        InventoryService inventory,
        PricingService pricing,
        IPaymentGateway payments,
        OrderRepository orders,
        EmailRetryService email)
    {
        _customers = customers;
        _inventory = inventory;
        _pricing = pricing;
        _payments = payments;
        _orders = orders;
        _email = email;
    }

    public async Task<bool> ProcessOrderAsync(Order order, CancellationToken ct)
    {
        var stockReserved = false;
        string? transactionId = null;

        try
        {
            var sw = Stopwatch.StartNew();

            Customer customer;
            try
            {
                customer = await _customers.GetCustomerAsync(order.CustomerId, ct);
            }
            catch (CustomerNotFoundException ex)
            {
                Log.Error($"Order #{order.Id} -> Step 1/6 Customer ... NOT FOUND in {sw.ElapsedMilliseconds} ms");
                return await StopAsync(order, OrderStatus.Rejected, ex.Message, ct);
            }

            if (!customer.IsActive)
            {
                Log.Error($"Order #{order.Id} -> Step 1/6 Customer ... INACTIVE in {sw.ElapsedMilliseconds} ms");
                return await StopAsync(order, OrderStatus.Rejected, "Customer is inactive", ct);
            }

            Log.Success($"Order #{order.Id} -> Step 1/6 Customer ... OK ({customer.Name}) in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            if (!await _inventory.ReserveStockAsync(order, ct))
            {
                var name = _inventory.TakeShortageName(order.Id) ?? "unknown product";
                Log.Error($"Order #{order.Id} -> Step 2/6 Inventory ... INSUFFICIENT STOCK in {sw.ElapsedMilliseconds} ms");
                return await StopAsync(order, OrderStatus.Rejected, $"Insufficient stock for '{name}'", ct);
            }

            stockReserved = true;
            Log.Success($"Order #{order.Id} -> Step 2/6 Inventory ... OK in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            try
            {
                await _pricing.CalculateTotalAsync(order, ct);
            }
            catch (InvalidCouponException ex)
            {
                Log.Error($"Order #{order.Id} -> Step 3/6 Pricing ... INVALID COUPON in {sw.ElapsedMilliseconds} ms, released");
                await _inventory.ReleaseStockAsync(order, CancellationToken.None);
                stockReserved = false;
                return await StopAsync(order, OrderStatus.Rejected, ex.Message, ct);
            }

            Log.Success($"Order #{order.Id} -> Step 3/6 Pricing ... OK (total {order.TotalAmount:F2}) in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            using var paymentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            paymentCts.CancelAfter(TimeSpan.FromSeconds(3));

            PaymentResult payment;
            try
            {
                payment = await _payments.ProcessPaymentAsync(order.Id, order.TotalAmount, paymentCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Log.Error($"Order #{order.Id} -> Step 4/6 Payment ... TIMEOUT after {sw.ElapsedMilliseconds} ms, released");
                await _inventory.ReleaseStockAsync(order, CancellationToken.None);
                stockReserved = false;
                return await StopAsync(order, OrderStatus.PaymentFailed, "Payment gateway timeout (3s)", ct);
            }

            if (!payment.Success)
            {
                Log.Error($"Order #{order.Id} -> Step 4/6 Payment ... DECLINED in {sw.ElapsedMilliseconds} ms, released");
                await _inventory.ReleaseStockAsync(order, CancellationToken.None);
                stockReserved = false;
                return await StopAsync(order, OrderStatus.PaymentFailed, payment.Message ?? "Card declined by issuer", ct);
            }

            transactionId = payment.TransactionId;
            Log.Success($"Order #{order.Id} -> Step 4/6 Payment ... OK ({transactionId}) in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            order.TransactionId = transactionId;
            order.Status = OrderStatus.Completed;
            order.FailureReason = null;
            try
            {
                await _orders.SaveAsync(order, ct);
            }
            catch (DatabaseWriteException ex)
            {
                Log.Error($"Order #{order.Id} -> Step 5/6 Save ... FAILED ({ex.Message}) in {sw.ElapsedMilliseconds} ms, refunding");

                await _payments.RefundAsync(transactionId!, CancellationToken.None);
                await _inventory.ReleaseStockAsync(order, CancellationToken.None);

                var refunded = transactionId;
                transactionId = null;
                stockReserved = false;

                return await StopAsync(
                    order,
                    OrderStatus.Failed,
                    $"Database save failed after payment; payment {refunded} refunded",
                    CancellationToken.None);
            }

            Log.Success($"Order #{order.Id} -> Step 5/6 Save ... OK in {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            try
            {
                await _email.SendConfirmationWithRetryAsync(order, customer, ct);
                Log.Success($"Order #{order.Id} -> Step 6/6 Email ... DONE in {sw.ElapsedMilliseconds} ms");
            }
            catch (OperationCanceledException)
            {
                Log.Error($"Order #{order.Id} -> Step 6/6 Email ... CANCELLED in {sw.ElapsedMilliseconds} ms");
            }

            return order.Status == OrderStatus.Completed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await CompensateAsync(order, transactionId, stockReserved);

            order.Status = OrderStatus.Pending;
            order.FailureReason = "Cancelled";
            order.TransactionId = null;
            await _orders.SaveAsync(order, CancellationToken.None);

            Log.Error($"Order #{order.Id} -> Cancelled, stock released and payment refunded if needed");
            return false;
        }
        catch (Exception ex)
        {
            await CompensateAsync(order, transactionId, stockReserved);

            order.Status = OrderStatus.Failed;
            order.FailureReason = ex.Message;
            await _orders.SaveAsync(order, CancellationToken.None);

            Log.Error($"Order #{order.Id} -> Failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> StopAsync(Order order, OrderStatus status, string reason, CancellationToken ct)
    {
        order.Status = status;
        order.FailureReason = reason;
        await _orders.SaveAsync(order, ct);
        return false;
    }

    private async Task CompensateAsync(Order order, string? transactionId, bool stockReserved)
    {
        if (transactionId is not null)
            await _payments.RefundAsync(transactionId, CancellationToken.None);

        if (stockReserved)
            await _inventory.ReleaseStockAsync(order, CancellationToken.None);
    }
}
