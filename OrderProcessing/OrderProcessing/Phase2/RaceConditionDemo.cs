
namespace OrderProcessing.Phase2;

public class RaceConditionDemo
{
    private const int KeyboardId = 4;

    public async Task RunAsync(CancellationToken ct)
    {
        var products = new ProductRepository();

        Console.WriteLine();
        Console.WriteLine("===== Race condition demo: the last keyboard =====");

        Console.WriteLine();
        Console.WriteLine("--- Unsafe reservation (no protection) ---");
        await SetKeyboardStockAsync(1, ct);

        var unsafe10 = products.ReserveStockUnsafeAsync(KeyboardId, 1, ct);
        var unsafe11 = products.ReserveStockUnsafeAsync(KeyboardId, 1, ct);
        var unsafeResults = await Task.WhenAll(unsafe10, unsafe11);

        var unsafeStock = await ReadKeyboardStockAsync(ct);
        Console.WriteLine($"Order #10 got the keyboard: {unsafeResults[0]}");
        Console.WriteLine($"Order #11 got the keyboard: {unsafeResults[1]}");
        Console.WriteLine($"Keyboard stock in products.json: {unsafeStock}");
        if (unsafeResults[0] && unsafeResults[1])
            Console.WriteLine("BUG: both orders got the last keyboard, but we only had one.");

        Console.WriteLine();
        Console.WriteLine("--- Safe reservation (check and update under one lock) ---");
        await SetKeyboardStockAsync(1, ct);

        var safe10 = products.ReserveStockAsync(KeyboardId, 1, ct);
        var safe11 = products.ReserveStockAsync(KeyboardId, 1, ct);
        var safeResults = await Task.WhenAll(safe10, safe11);

        var safeStock = await ReadKeyboardStockAsync(ct);
        Console.WriteLine($"Order #10 got the keyboard: {safeResults[0]}");
        Console.WriteLine($"Order #11 got the keyboard: {safeResults[1]}");
        Console.WriteLine($"Keyboard stock in products.json: {safeStock}");
    }

    private static async Task SetKeyboardStockAsync(int stock, CancellationToken ct)
    {
        var store = new JsonFileStore();
        await store.UpdateAsync<Product, bool>(DataFiles.Products, products =>
        {
            products.First(p => p.Id == KeyboardId).StockQuantity = stock;
            return true;
        }, ct);
    }

    private static async Task<int> ReadKeyboardStockAsync(CancellationToken ct)
    {
        var store = new JsonFileStore();
        var products = await store.ReadAllAsync<Product>(DataFiles.Products, ct);
        return products.First(p => p.Id == KeyboardId).StockQuantity;
    }
}
