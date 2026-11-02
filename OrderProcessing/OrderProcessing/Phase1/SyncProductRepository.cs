
namespace OrderProcessing.Phase1;

public class SyncProductRepository
{
    public Product? GetById(int id)
    {
        var store = new JsonFileStore();

        Log.Info("SyncProductRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info($"SyncProductRepository -> Executing query for Product #{id}...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        var products = store.ReadAll<Product>(DataFiles.Products);
        var product = products.FirstOrDefault(p => p.Id == id);

        if (product is null)
            Log.Info($"SyncProductRepository -> Product #{id} not found");
        else
            Log.Success($"SyncProductRepository -> Product #{id} found ({product.Name})");

        return product;
    }

    public bool ReserveStock(int productId, int qty)
    {
        var store = new JsonFileStore();

        Log.Info("SyncProductRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info("SyncProductRepository -> Beginning transaction...");
        Thread.Sleep(100);

        Log.Info($"SyncProductRepository -> Reserving {qty} of Product #{productId}...");
        Thread.Sleep(400);

        bool reserved = store.Update<Product, bool>(DataFiles.Products, products =>
        {
            var product = products.FirstOrDefault(p => p.Id == productId);
            if (product is null || product.StockQuantity < qty)
                return false;

            product.StockQuantity -= qty;
            return true;
        });

        if (!reserved)
        {
            Log.Error($"SyncProductRepository -> Cannot reserve {qty} of Product #{productId} (missing or not enough stock)");
            return false;
        }

        Log.Info("SyncProductRepository -> Committing transaction...");
        Thread.Sleep(200);

        Log.Success($"SyncProductRepository -> Reserved {qty} of Product #{productId}");
        return true;
    }

    public void ReleaseStock(int productId, int qty)
    {
        var store = new JsonFileStore();

        Log.Info("SyncProductRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info("SyncProductRepository -> Beginning transaction...");
        Thread.Sleep(100);

        Log.Info($"SyncProductRepository -> Releasing {qty} of Product #{productId}...");
        Thread.Sleep(400);

        store.Update<Product, bool>(DataFiles.Products, products =>
        {
            var product = products.FirstOrDefault(p => p.Id == productId);
            if (product is null)
                return false;

            product.StockQuantity += qty;
            return true;
        });

        Log.Info("SyncProductRepository -> Committing transaction...");
        Thread.Sleep(200);

        Log.Success($"SyncProductRepository -> Released {qty} of Product #{productId}");
    }
}
