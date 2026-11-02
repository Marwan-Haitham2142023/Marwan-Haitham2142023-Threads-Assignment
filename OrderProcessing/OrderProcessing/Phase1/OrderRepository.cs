
namespace OrderProcessing.Phase1;

public class OrderRepository
{
    private readonly HashSet<int> _failSaveForOrderIds;

    public OrderRepository(IEnumerable<int>? failSaveForOrderIds = null)
    {
        _failSaveForOrderIds = new HashSet<int>(failSaveForOrderIds ?? Array.Empty<int>());
    }

    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        var store = new JsonFileStore();

        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("OrderRepository -> Beginning transaction...");
        await Task.Delay(100, ct);

        Log.Info($"OrderRepository -> Saving Order #{order.Id}...");
        await Task.Delay(400, ct);

        if (order.Status == OrderStatus.Completed && _failSaveForOrderIds.Contains(order.Id))
        {
            Log.Error($"OrderRepository -> Order #{order.Id}: Connection lost during commit");
            throw new DatabaseWriteException("Connection lost during commit");
        }

        await store.UpdateAsync<Order, bool>(DataFiles.Orders, orders =>
        {
            var index = orders.FindIndex(o => o.Id == order.Id);
            if (index >= 0)
                orders[index] = order;
            else
                orders.Add(order);
            return true;
        }, ct);

        Log.Info("OrderRepository -> Committing transaction...");
        await Task.Delay(200, ct);

        Log.Success($"OrderRepository -> Order #{order.Id} saved (Status: {order.Status})");
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken ct)
    {
        var store = new JsonFileStore();

        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("OrderRepository -> Executing query for all orders...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var orders = await store.ReadAllAsync<Order>(DataFiles.Orders, ct);

        Log.Success($"OrderRepository -> {orders.Count} orders found");
        return orders;
    }

    public async Task<List<Order>> GetByStatusAsync(OrderStatus status, CancellationToken ct)
    {
        var store = new JsonFileStore();

        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"OrderRepository -> Executing query for orders with status {status}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var orders = await store.ReadAllAsync<Order>(DataFiles.Orders, ct);
        var filtered = orders.Where(o => o.Status == status).ToList();

        Log.Success($"OrderRepository -> {filtered.Count} orders with status {status}");
        return filtered;
    }
}
