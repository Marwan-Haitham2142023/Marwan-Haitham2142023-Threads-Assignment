
namespace OrderProcessing.Phase1;

public class SyncOrderRepository
{
    public void Save(Order order)
    {
        var store = new JsonFileStore();

        Log.Info("SyncOrderRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info("SyncOrderRepository -> Beginning transaction...");
        Thread.Sleep(100);

        Log.Info($"SyncOrderRepository -> Saving Order #{order.Id}...");
        Thread.Sleep(400);

        store.Update<Order, bool>(DataFiles.Orders, orders =>
        {
            var index = orders.FindIndex(o => o.Id == order.Id);
            if (index >= 0)
                orders[index] = order;
            else
                orders.Add(order);
            return true;
        });

        Log.Info("SyncOrderRepository -> Committing transaction...");
        Thread.Sleep(200);

        Log.Success($"SyncOrderRepository -> Order #{order.Id} saved (Status: {order.Status})");
    }
}
