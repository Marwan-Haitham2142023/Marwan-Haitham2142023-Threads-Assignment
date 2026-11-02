
namespace OrderProcessing.Phase1;

public class ProductRepository
{
    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct)
    {
        var store = new JsonFileStore();

        Log.Info("ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"ProductRepository -> Executing query for Product #{id}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var products = await store.ReadAllAsync<Product>(DataFiles.Products, ct);
        var product = products.FirstOrDefault(p => p.Id == id);

        if (product is null)
            Log.Info($"ProductRepository -> Product #{id} not found");
        else
            Log.Success($"ProductRepository -> Product #{id} found ({product.Name})");

        return product;
    }

    public async Task<bool> ReserveStockAsync(int productId, int qty, CancellationToken ct)
    {
        var store = new JsonFileStore();

        Log.Info("ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("ProductRepository -> Beginning transaction...");
        await Task.Delay(100, ct);

        Log.Info($"ProductRepository -> Reserving {qty} of Product #{productId}...");
        await Task.Delay(400, ct);

        bool reserved = await store.UpdateAsync<Product, bool>(DataFiles.Products, products =>
        {
            var product = products.FirstOrDefault(p => p.Id == productId);
            if (product is null || product.StockQuantity < qty)
                return false;

            product.StockQuantity -= qty;
            return true;
        }, ct);

        if (!reserved)
        {
            Log.Error($"ProductRepository -> Cannot reserve {qty} of Product #{productId} (missing or not enough stock)");
            return false;
        }

        Log.Info("ProductRepository -> Committing transaction...");
        try
        {
            await Task.Delay(200, ct);
        }
        catch (OperationCanceledException)
        {
            await store.UpdateAsync<Product, bool>(DataFiles.Products, products =>
            {
                var product = products.FirstOrDefault(p => p.Id == productId);
                if (product is not null)
                    product.StockQuantity += qty;
                return true;
            }, CancellationToken.None);
            throw;
        }

        Log.Success($"ProductRepository -> Reserved {qty} of Product #{productId}");
        return true;
    }

    public async Task ReleaseStockAsync(int productId, int qty, CancellationToken ct)
    {
        var store = new JsonFileStore();

        Log.Info("ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("ProductRepository -> Beginning transaction...");
        await Task.Delay(100, ct);

        Log.Info($"ProductRepository -> Releasing {qty} of Product #{productId}...");
        await Task.Delay(400, ct);

        await store.UpdateAsync<Product, bool>(DataFiles.Products, products =>
        {
            var product = products.FirstOrDefault(p => p.Id == productId);
            if (product is null)
                return false;

            product.StockQuantity += qty;
            return true;
        }, ct);

        Log.Info("ProductRepository -> Committing transaction...");
        await Task.Delay(200, ct);

        Log.Success($"ProductRepository -> Released {qty} of Product #{productId}");
    }

    public async Task<bool> ReserveStockUnsafeAsync(int productId, int qty, CancellationToken ct)
    {
        var store = new JsonFileStore();

        var products = await store.ReadAllAsync<Product>(DataFiles.Products, ct);
        var product = products.FirstOrDefault(p => p.Id == productId);

        if (product is null || product.StockQuantity < qty)
        {
            Log.Error($"ProductRepository (UNSAFE) -> Cannot reserve {qty} of Product #{productId}");
            return false;
        }

        Log.Info($"ProductRepository (UNSAFE) -> Read stock of Product #{productId}: {product.StockQuantity}");

        Log.Info("ProductRepository (UNSAFE) -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("ProductRepository (UNSAFE) -> Beginning transaction...");
        await Task.Delay(100, ct);

        Log.Info($"ProductRepository (UNSAFE) -> Updating stock of Product #{productId}...");
        await Task.Delay(400, ct);

        product.StockQuantity -= qty;
        await store.WriteAllAsync(DataFiles.Products, products, ct);

        Log.Success($"ProductRepository (UNSAFE) -> Reserved {qty} of Product #{productId}");
        return true;
    }
}
