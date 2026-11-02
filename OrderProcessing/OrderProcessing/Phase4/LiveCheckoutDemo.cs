using System.Diagnostics;
using OrderProcessing.Phase3;

namespace OrderProcessing.Phase4;

public class LiveCheckoutDemo
{
    public List<Order> Orders { get; private set; } = new();

    public async Task RunAsync(CancellationToken ct)
    {
        await DataSeeder.ResetAsync(ct);
        Orders = await new IncomingOrderRepository().GetAllAsync(ct);
        var processor = Phase3Runner.CreateProcessor();

        Console.WriteLine();
        Console.WriteLine("===== 10.1 Live checkout progress (all 20 orders start at once) =====");

        ThreadTracker.Reset();
        var poolBefore = ThreadPool.ThreadCount;
        var watch = Stopwatch.StartNew();

        var tasks = Orders.Select(o => ProcessAsync(processor, o, ct)).ToList();

        var finished = 0;
        await foreach (var task in Task.WhenEach(tasks))
        {
            var (order, seconds) = await task;
            finished++;
            Log.Plain($"[{finished,2}/{Orders.Count}] Order #{order.Id} -> {Describe(order)} after {seconds:F1} s");
        }

        watch.Stop();

        PrintCounts();
        Console.WriteLine($"Total time: {watch.Elapsed.TotalSeconds:F1} seconds");
        PerfReport.Print("10.1 live checkout", watch.Elapsed, ThreadTracker.Count, poolBefore, ThreadPool.ThreadCount);

        if (!ct.IsCancellationRequested)
            await Phase3Runner.PrintFileCheckAsync(CancellationToken.None);
    }

    private static async Task<(Order Order, double Seconds)> ProcessAsync(OrderProcessor processor, Order order, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        await processor.ProcessOrderAsync(order, ct);
        return (order, watch.Elapsed.TotalSeconds);
    }

    private static string Describe(Order order)
    {
        if (order.Status == OrderStatus.Completed)
            return $"Completed (total {order.TotalAmount:F2})";

        return string.IsNullOrEmpty(order.FailureReason)
            ? order.Status.ToString()
            : $"{order.Status} ({order.FailureReason})";
    }

    private void PrintCounts()
    {
        Console.WriteLine();
        Console.WriteLine("Orders per status:");
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            var count = Orders.Count(o => o.Status == status);
            var always = status is OrderStatus.Completed or OrderStatus.Rejected or OrderStatus.PaymentFailed or OrderStatus.Failed;
            if (count > 0 || always)
                Console.WriteLine($"  {status,-14} {count}");
        }

        Console.WriteLine("Expected (9.4): Completed 11, Rejected 6, PaymentFailed 2, Failed 1");
    }
}
