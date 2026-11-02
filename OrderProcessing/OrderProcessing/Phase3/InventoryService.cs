using System.Collections.Concurrent;

namespace OrderProcessing.Phase3;

public class InventoryService
{
    private readonly ProductRepository _products;
    private readonly ConcurrentDictionary<int, string> _shortages = new();

    public InventoryService(ProductRepository products)
    {
        _products = products;
    }

    public async Task<bool> ReserveStockAsync(Order order, CancellationToken ct)
    {
        var reserved = new List<OrderItem>();

        try
        {
            foreach (var item in order.Items)
            {
                var ok = await _products.ReserveStockAsync(item.ProductId, item.Quantity, ct);
                if (!ok)
                {
                    var product = await _products.GetByIdAsync(item.ProductId, ct);
                    _shortages[order.Id] = product?.Name ?? $"Product #{item.ProductId}";

                    await RollbackAsync(order, reserved);
                    return false;
                }

                reserved.Add(item);
            }

            return true;
        }
        catch
        {
            await RollbackAsync(order, reserved);
            throw;
        }
    }

    public async Task ReleaseStockAsync(Order order, CancellationToken ct)
    {
        foreach (var item in order.Items)
            await _products.ReleaseStockAsync(item.ProductId, item.Quantity, ct);
    }

    public string? TakeShortageName(int orderId) =>
        _shortages.TryRemove(orderId, out var name) ? name : null;

    private async Task RollbackAsync(Order order, List<OrderItem> reserved)
    {
        if (reserved.Count == 0)
            return;

        Log.Info($"InventoryService -> Rolling back {reserved.Count} reserved item(s) of Order #{order.Id}");

        for (int i = reserved.Count - 1; i >= 0; i--)
            await _products.ReleaseStockAsync(reserved[i].ProductId, reserved[i].Quantity, CancellationToken.None);

        reserved.Clear();
    }
}
