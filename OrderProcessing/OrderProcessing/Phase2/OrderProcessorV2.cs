using System.Collections.Concurrent;

namespace OrderProcessing.Phase2;

public class OrderProcessorV2
{
    private readonly ProductRepository _products;
    private readonly OrderRepository _orders;
    private readonly OrderPricer _pricer;
    private readonly InvoiceGenerator _invoiceGenerator;
    private readonly ConcurrentDictionary<int, List<OrderItem>> _reserved = new();

    public OrderProcessorV2(
        ProductRepository products,
        OrderRepository orders,
        OrderPricer pricer,
        InvoiceGenerator invoiceGenerator)
    {
        _products = products;
        _orders = orders;
        _pricer = pricer;
        _invoiceGenerator = invoiceGenerator;
    }

    public async Task<bool> ProcessValidatedOrderAsync(Order order, CancellationToken ct)
    {
        var reserved = _reserved.GetOrAdd(order.Id, _ => new List<OrderItem>());

        foreach (var item in order.Items)
        {
            var ok = await _products.ReserveStockAsync(item.ProductId, item.Quantity, ct);
            if (!ok)
            {
                await ReleaseReservedStockAsync(order, ct);

                var product = await _products.GetByIdAsync(item.ProductId, ct);
                var name = product?.Name ?? $"Product #{item.ProductId}";

                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Insufficient stock for '{name}'";
                await _orders.SaveAsync(order, ct);

                Log.Error($"OrderProcessorV2 -> Order #{order.Id} rejected: {order.FailureReason}");
                return false;
            }

            reserved.Add(item);
        }

        await _pricer.PriceOrderAsync(order, ct);
        await _invoiceGenerator.GenerateAsync(order, ct);

        order.Status = OrderStatus.Completed;
        order.FailureReason = null;
        await _orders.SaveAsync(order, ct);

        _reserved.TryRemove(order.Id, out _);

        Log.Success($"OrderProcessorV2 -> Order #{order.Id} completed");
        return true;
    }

    public async Task ReleaseReservedStockAsync(Order order, CancellationToken ct)
    {
        if (!_reserved.TryGetValue(order.Id, out var items))
            return;

        while (items.Count > 0)
        {
            var item = items[^1];
            await _products.ReleaseStockAsync(item.ProductId, item.Quantity, ct);
            items.RemoveAt(items.Count - 1);
        }

        _reserved.TryRemove(order.Id, out _);
    }
}
