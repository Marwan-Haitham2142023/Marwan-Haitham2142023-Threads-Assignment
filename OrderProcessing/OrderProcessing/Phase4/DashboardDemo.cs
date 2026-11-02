using System.Diagnostics;
using OrderProcessing.Phase3;

namespace OrderProcessing.Phase4;

public class DashboardDemo
{
    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("===== 10.2 Customer dashboard (four loads at the same time) =====");

        var service = new CustomerDashboardService(
            new CustomerService(new CustomerRepository()),
            new OrderRepository(),
            failPaymentsForCustomerIds: new[] { 99 });

        await ShowAsync(service, 3, ct);
        await ShowAsync(service, 99, ct);
    }

    private static async Task ShowAsync(CustomerDashboardService service, int customerId, CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine($"--- Dashboard for Customer #{customerId} ---");

        ThreadTracker.Reset();
        var poolBefore = ThreadPool.ThreadCount;
        var watch = Stopwatch.StartNew();

        try
        {
            var dashboard = await service.LoadAsync(customerId, ct);
            watch.Stop();

            var orders = string.Join(", ", dashboard.Orders.Select(o => $"#{o.Id} {o.Status}"));
            var payments = string.Join(", ", dashboard.Payments.Select(p => $"#{p.OrderId} {p.Status}"));
            var emails = string.Join(", ", dashboard.Emails.Select(e => $"#{e.OrderId}"));

            Log.Plain($"Customer #{dashboard.Customer.Id} {dashboard.Customer.Name} — " +
                $"{dashboard.Orders.Count} orders ({orders}), " +
                $"{dashboard.Payments.Count} payments ({payments}), " +
                $"{dashboard.Emails.Count} email ({emails})");
        }
        catch (AggregateException ex)
        {
            watch.Stop();

            Log.Plain($"Customer #{customerId}: {ex.InnerExceptions.Count} failures:");
            foreach (var inner in ex.InnerExceptions)
                Log.Plain($"  - {inner.Message}");
        }

        Console.WriteLine($"Dashboard time: {watch.Elapsed.TotalSeconds:F1} s (the slowest single load is about 0.8 s, the sum of the four loads would be about 3 s)");
        PerfReport.Print($"10.2 dashboard #{customerId}", watch.Elapsed, ThreadTracker.Count, poolBefore, ThreadPool.ThreadCount);
    }
}
