using System.Diagnostics;

namespace OrderProcessing.Phase4;

public class ShippingQuoteService
{
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(1200);

    private sealed record Outcome(string Carrier, decimal? Price, bool Cancelled, long ElapsedMs);

    private readonly List<IShippingCarrier> _carriers;

    public ShippingQuoteService(IEnumerable<IShippingCarrier> carriers)
    {
        _carriers = carriers.ToList();
    }

    public async Task<ShippingQuote> GetFastestQuoteAsync(Order order, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();

        using var race = CancellationTokenSource.CreateLinkedTokenSource(ct);
        race.CancelAfter(Budget);

        var running = _carriers.Select(c => AskAsync(c, order, watch, race.Token)).ToList();

        Outcome? winner = null;
        await foreach (var task in Task.WhenEach(running))
        {
            var outcome = await task;
            if (outcome.Price is not null)
            {
                winner = outcome;
                break;
            }
        }

        race.Cancel();
        var all = await Task.WhenAll(running);
        var cancelled = all.Where(o => o.Cancelled).Select(o => o.Carrier).ToList();

        if (winner is not null)
        {
            return new ShippingQuote
            {
                Carrier = winner.Carrier,
                Price = winner.Price!.Value,
                ElapsedMs = winner.ElapsedMs,
                CancelledCarriers = cancelled
            };
        }

        ct.ThrowIfCancellationRequested();

        if (cancelled.Count > 0)
        {
            return new ShippingQuote
            {
                Carrier = "Flat rate",
                Price = 25.00m,
                ElapsedMs = watch.ElapsedMilliseconds,
                CancelledCarriers = cancelled
            };
        }

        throw new ShippingProviderUnavailableException("All carriers failed");
    }

    private static async Task<Outcome> AskAsync(IShippingCarrier carrier, Order order, Stopwatch watch, CancellationToken ct)
    {
        try
        {
            var price = await carrier.GetQuoteAsync(order, ct);
            return new Outcome(carrier.Name, price, false, watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return new Outcome(carrier.Name, null, true, watch.ElapsedMilliseconds);
        }
        catch (ShippingProviderUnavailableException)
        {
            return new Outcome(carrier.Name, null, false, watch.ElapsedMilliseconds);
        }
    }
}
