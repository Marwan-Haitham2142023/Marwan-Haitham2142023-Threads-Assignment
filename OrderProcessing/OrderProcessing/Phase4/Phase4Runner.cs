
namespace OrderProcessing.Phase4;

public class Phase4Runner
{
    public async Task RunAsync(CancellationToken ct)
    {
        var liveCheckout = new LiveCheckoutDemo();

        try
        {
            await liveCheckout.RunAsync(ct);
            ct.ThrowIfCancellationRequested();

            await new DashboardDemo().RunAsync(ct);
            await new ShippingQuoteDemo().RunAsync(ct);
            await new FraudScoringDemo().RunAsync(ct);
            await new EmailResendDemo().RunAsync(ct);

            SampleOutput.Save("phase4");
        }
        catch (OperationCanceledException)
        {
            await PrintCancelledAsync(liveCheckout.Orders);
        }
    }

    private static async Task PrintCancelledAsync(List<Order> orders)
    {
        var cancelled = orders.Count(o => o.Status == OrderStatus.Pending);
        var finished = orders.Count - cancelled;

        Console.WriteLine();
        Console.WriteLine($"Run cancelled — {finished} orders finished, {cancelled} cancelled.");

        var store = new JsonFileStore();
        var savedOrders = await store.ReadAllAsync<Order>(DataFiles.Orders, CancellationToken.None);
        var payments = await store.ReadAllAsync<PaymentTransaction>(DataFiles.Payments, CancellationToken.None);
        var products = await store.ReadAllAsync<Product>(DataFiles.Products, CancellationToken.None);

        Console.WriteLine();
        Console.WriteLine("Files after the cancelled run:");
        Console.WriteLine($"  orders.json:    {savedOrders.Count} records: {Group(savedOrders.GroupBy(o => o.Status))}");
        Console.WriteLine($"  cancelled:      {savedOrders.Count(o => o.FailureReason == "Cancelled")} orders saved as Pending with FailureReason \"Cancelled\"");
        Console.WriteLine($"  payments.json:  {payments.Count} records: {Group(payments.GroupBy(p => p.Status))}");
        Console.WriteLine($"  stock now:      {string.Join(", ", products.OrderBy(p => p.Id).Select(p => $"P{p.Id}={p.StockQuantity}"))}");
        Console.WriteLine("  seed stock:     P1=10, P2=15, P3=2, P4=1, P5=30, P6=12, P7=6, P8=20");
    }

    private static string Group<TKey>(IEnumerable<IGrouping<TKey, object>> groups) where TKey : notnull =>
        groups.Count() == 0
            ? "none"
            : string.Join(", ", groups.OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}"));
}
