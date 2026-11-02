
namespace OrderProcessing.Phase4;

public abstract class ShippingCarrier : IShippingCarrier
{
    private readonly HashSet<int> _failForOrderIds;
    private readonly JsonFileStore _store = new();

    protected ShippingCarrier(IEnumerable<int>? failForOrderIds)
    {
        _failForOrderIds = new HashSet<int>(failForOrderIds ?? Array.Empty<int>());
    }

    public abstract string Name { get; }

    protected abstract int MinResponseMs { get; }

    protected abstract int MaxResponseMs { get; }

    protected abstract decimal PriceFactor { get; }

    protected virtual bool FailsByDefaultFor(int orderId) => false;

    public async Task<decimal> GetQuoteAsync(Order order, CancellationToken ct)
    {
        try
        {
            Log.Info($"[{Name}] handshake for Order #{order.Id}...");
            await Task.Delay(150, ct);

            if (FailsByDefaultFor(order.Id) || _failForOrderIds.Contains(order.Id))
            {
                Log.Error($"[{Name}] Order #{order.Id}: provider unavailable");
                throw new ShippingProviderUnavailableException($"{Name} unavailable");
            }

            Log.Info($"[{Name}] waiting for the quote of Order #{order.Id}...");
            await Task.Delay(Random.Shared.Next(MinResponseMs, MaxResponseMs + 1), ct);

            var price = await CalculatePriceAsync(order, ct);

            Log.Success($"[{Name}] Order #{order.Id}: quote {price:F2}");
            return price;
        }
        catch (OperationCanceledException)
        {
            Log.Info($"[{Name}] cancelled.");
            throw;
        }
    }

    private async Task<decimal> CalculatePriceAsync(Order order, CancellationToken ct)
    {
        var products = await _store.ReadAllAsync<Product>(DataFiles.Products, ct);

        decimal subTotal = 0m;
        decimal totalWeight = 0m;
        int totalQuantity = 0;

        foreach (var item in order.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            subTotal += PricingCalculator.Round(product.Price) * item.Quantity;
            totalWeight += product.WeightKg * item.Quantity;
            totalQuantity += item.Quantity;
        }

        var baseCost = PricingCalculator.ShippingCost(PricingCalculator.Round(subTotal), totalQuantity, totalWeight);
        return PricingCalculator.Round(baseCost * PriceFactor);
    }
}
