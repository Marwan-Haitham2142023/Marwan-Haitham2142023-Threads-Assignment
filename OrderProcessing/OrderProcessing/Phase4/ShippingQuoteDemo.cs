using System.Diagnostics;

namespace OrderProcessing.Phase4;

public class ShippingQuoteDemo
{
    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("===== 10.3 Fastest shipping quote (three carriers race, budget 1.2 s) =====");

        var incoming = await new IncomingOrderRepository().GetAllAsync(ct);

        var service = new ShippingQuoteService(new IShippingCarrier[]
        {
            new FastShipCarrier(),
            new CairoExpressCarrier(),
            new NileCourierCarrier()
        });

        ThreadTracker.Reset();
        var poolBefore = ThreadPool.ThreadCount;
        var total = Stopwatch.StartNew();

        var rows = new List<(int OrderId, ShippingQuote Quote)>();
        foreach (var order in incoming.Where(o => o.Id <= 5).OrderBy(o => o.Id))
        {
            Console.WriteLine();
            Console.WriteLine($"--- Quote for Order #{order.Id} ---");
            var quote = await service.GetFastestQuoteAsync(order, ct);
            rows.Add((order.Id, quote));
        }

        total.Stop();

        Console.WriteLine();
        Console.WriteLine($"{"Order",5}  {"Winner",-13} {"Price",8} {"Time (ms)",10}  Cancelled");
        Console.WriteLine(new string('-', 70));
        foreach (var (orderId, quote) in rows)
        {
            var cancelled = quote.CancelledCarriers.Count == 0 ? "-" : string.Join(", ", quote.CancelledCarriers);
            Log.Plain($"{"#" + orderId,5}  {quote.Carrier,-13} {quote.Price,8:F2} {quote.ElapsedMs,10}  {cancelled}");
        }

        PerfReport.Print("10.3 shipping quotes (5 orders)", total.Elapsed, ThreadTracker.Count, poolBefore, ThreadPool.ThreadCount);

        await AllCarriersFailAsync(incoming.First(o => o.Id == 6), ct);
        await CallerCancelsAsync(incoming.First(o => o.Id == 1), ct);
    }

    private static async Task AllCarriersFailAsync(Order order, CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine($"--- Every carrier is configured to fail for order #{order.Id} ---");

        var failing = new ShippingQuoteService(new IShippingCarrier[]
        {
            new FastShipCarrier(new[] { order.Id }),
            new CairoExpressCarrier(new[] { order.Id }),
            new NileCourierCarrier(new[] { order.Id })
        });

        try
        {
            var quote = await failing.GetFastestQuoteAsync(order, ct);
            Console.WriteLine($"Unexpected quote from {quote.Carrier}.");
        }
        catch (ShippingProviderUnavailableException ex)
        {
            Log.Plain($"Order #{order.Id} -> {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task CallerCancelsAsync(Order order, CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine($"--- The caller cancels its own token after 300 ms (order #{order.Id}) ---");

        var service = new ShippingQuoteService(new IShippingCarrier[]
        {
            new FastShipCarrier(),
            new CairoExpressCarrier(),
            new NileCourierCarrier()
        });

        using var callerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        callerCts.CancelAfter(300);
        var watch = Stopwatch.StartNew();

        try
        {
            await service.GetFastestQuoteAsync(order, callerCts.Token);
            Console.WriteLine("Unexpected: a quote arrived.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Log.Plain($"The quote was cancelled after {watch.ElapsedMilliseconds} ms, so every carrier stopped.");
        }
    }
}
