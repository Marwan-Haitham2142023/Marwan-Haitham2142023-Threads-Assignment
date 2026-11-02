using System.Diagnostics;

namespace OrderProcessing.Phase3;

public class SyncVsAsyncRunner
{
    private sealed record RunResult(
        string Name,
        double Seconds,
        int DistinctThreads,
        int PoolThreadsBefore,
        int PoolThreadsAfter,
        int Completed,
        int Rejected,
        string RejectedIds);

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("===== Sync (10 threads) vs async (10 tasks): orders #1-#10, failures disabled =====");

        var syncResult = await RunSyncAsync(ct);
        var asyncResult = await RunAsyncVersionAsync(ct);

        Console.WriteLine();
        Console.WriteLine($"{"Version",-20} {"Time (s)",9} {"Threads used",13} {"ThreadPool before",18} {"ThreadPool after",17} {"Completed",10} {"Rejected",9}");
        Console.WriteLine(new string('-', 102));
        PrintRow(syncResult);
        PrintRow(asyncResult);

        Console.WriteLine();
        Check("Sync: 8 Completed, 2 Rejected (#4, #9)", syncResult);
        Check("Async: 8 Completed, 2 Rejected (#4, #9)", asyncResult);
    }

    private static async Task<RunResult> RunSyncAsync(CancellationToken ct)
    {
        await DataSeeder.ResetAsync(ct);
        var orders = await LoadFirstTenAsync(ct);

        var customers = new SyncCustomerRepository();
        var processor = new SyncOrderProcessor(
            customers,
            new SyncInventoryService(new SyncProductRepository()),
            new SyncPricingService(customers, new SyncCouponRepository()),
            new SyncPaymentGateway(),
            new SyncOrderRepository(),
            new SyncEmailService());

        Console.WriteLine();
        Console.WriteLine("--- Sync version: one Thread per order, start all 10, then Join each ---");

        var poolBefore = ThreadPool.ThreadCount;
        ThreadTracker.Reset();
        var watch = Stopwatch.StartNew();

        var threads = orders.Select(o => new Thread(() => processor.ProcessOrder(o))).ToList();
        foreach (var thread in threads)
            thread.Start();
        foreach (var thread in threads)
            thread.Join();

        watch.Stop();
        return Summarize("Sync (10 threads)", orders, watch, poolBefore);
    }

    private static async Task<RunResult> RunAsyncVersionAsync(CancellationToken ct)
    {
        await DataSeeder.ResetAsync(ct);
        var orders = await LoadFirstTenAsync(ct);
        var processor = Phase3Runner.CreateProcessor(withFailures: false);

        Console.WriteLine();
        Console.WriteLine("--- Async version: start all 10 tasks, then await each ---");

        var poolBefore = ThreadPool.ThreadCount;
        ThreadTracker.Reset();
        var watch = Stopwatch.StartNew();

        var tasks = orders.Select(o => processor.ProcessOrderAsync(o, ct)).ToList();
        foreach (var task in tasks)
            await task;

        watch.Stop();
        return Summarize("Async (10 tasks)", orders, watch, poolBefore);
    }

    private static async Task<List<Order>> LoadFirstTenAsync(CancellationToken ct)
    {
        var all = await new IncomingOrderRepository().GetAllAsync(ct);
        return all.Where(o => o.Id <= 10).OrderBy(o => o.Id).ToList();
    }

    private static RunResult Summarize(string name, List<Order> orders, Stopwatch watch, int poolBefore)
    {
        var rejected = orders.Where(o => o.Status == OrderStatus.Rejected).Select(o => o.Id).OrderBy(id => id).ToList();

        return new RunResult(
            name,
            watch.Elapsed.TotalSeconds,
            ThreadTracker.Count,
            poolBefore,
            ThreadPool.ThreadCount,
            orders.Count(o => o.Status == OrderStatus.Completed),
            rejected.Count,
            string.Join(", ", rejected.Select(id => "#" + id)));
    }

    private static void PrintRow(RunResult r)
    {
        Console.WriteLine($"{r.Name,-20} {r.Seconds,9:F1} {r.DistinctThreads,13} {r.PoolThreadsBefore,18} {r.PoolThreadsAfter,17} {r.Completed,10} {r.Rejected,9}");
    }

    private static void Check(string label, RunResult r)
    {
        var ok = r.Completed == 8 && r.Rejected == 2 && r.RejectedIds == "#4, #9";
        var text = ok ? "OK      " : "MISMATCH";
        var line = $"{text} {label} (rejected: {r.RejectedIds})";
        if (ok)
            Log.Success(line);
        else
            Log.Error(line);
    }
}
