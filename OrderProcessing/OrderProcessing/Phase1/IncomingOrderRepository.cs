
namespace OrderProcessing.Phase1;

public class IncomingOrderRepository
{
    public async Task<List<Order>> GetAllAsync(CancellationToken ct)
    {
        var store = new JsonFileStore();

        Log.Info("IncomingOrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("IncomingOrderRepository -> Executing query for incoming orders...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var orders = await store.ReadAllAsync<Order>(DataFiles.IncomingOrders, ct);

        Log.Success($"IncomingOrderRepository -> {orders.Count} incoming orders found");
        return orders;
    }
}
