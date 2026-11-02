using System.Diagnostics;

namespace OrderProcessing.Phase2;

public class PricingTimingRunner
{
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await DataSeeder.ResetAsync(ct);

            var incoming = await new IncomingOrderRepository().GetAllAsync(ct);
            var order = incoming.First(o => o.Id == 1);

            await CompareAsync(order, ct);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
            Console.WriteLine("Run cancelled.");
        }
    }

    public async Task CompareAsync(Order order, CancellationToken ct)
    {
        var pricing = new PricingService(
            new CustomerRepository(),
            new CouponRepository(),
            new[] { 7 });
        var pricer = new OrderPricer(pricing);

        Console.WriteLine();
        Console.WriteLine($"===== Concurrent pricing (Order #{order.Id}) =====");
        var concurrentWatch = Stopwatch.StartNew();
        await pricer.PriceOrderAsync(order, ct);
        concurrentWatch.Stop();

        Console.WriteLine();
        Console.WriteLine($"===== Sequential pricing (Order #{order.Id}) =====");
        var sequentialWatch = Stopwatch.StartNew();
        await pricer.PriceOrderSequentialAsync(order, ct);
        sequentialWatch.Stop();

        Console.WriteLine();
        Console.WriteLine("===== Pricing Time =====");
        Console.WriteLine($"Concurrent: {concurrentWatch.Elapsed.TotalSeconds:F1} seconds");
        Console.WriteLine($"Sequential: {sequentialWatch.Elapsed.TotalSeconds:F1} seconds");
    }
}
