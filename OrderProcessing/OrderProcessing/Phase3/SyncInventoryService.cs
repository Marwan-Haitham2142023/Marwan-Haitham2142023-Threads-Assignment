
namespace OrderProcessing.Phase3;

public class SyncInventoryService
{
    private readonly SyncProductRepository _products;

    public SyncInventoryService(SyncProductRepository products)
    {
        _products = products;
    }

    public bool ReserveStock(Order order, out string? shortageName)
    {
        shortageName = null;
        var reserved = new List<OrderItem>();

        try
        {
            foreach (var item in order.Items)
            {
                if (!_products.ReserveStock(item.ProductId, item.Quantity))
                {
                    var product = _products.GetById(item.ProductId);
                    shortageName = product?.Name ?? $"Product #{item.ProductId}";

                    Rollback(order, reserved);
                    return false;
                }

                reserved.Add(item);
            }

            return true;
        }
        catch
        {
            Rollback(order, reserved);
            throw;
        }
    }

    public void ReleaseStock(Order order)
    {
        foreach (var item in order.Items)
            _products.ReleaseStock(item.ProductId, item.Quantity);
    }

    private void Rollback(Order order, List<OrderItem> reserved)
    {
        if (reserved.Count == 0)
            return;

        Log.Info($"SyncInventoryService -> Rolling back {reserved.Count} reserved item(s) of Order #{order.Id}");

        for (int i = reserved.Count - 1; i >= 0; i--)
            _products.ReleaseStock(reserved[i].ProductId, reserved[i].Quantity);

        reserved.Clear();
    }
}
