using System.Collections.Concurrent;
using System.Diagnostics;

namespace OrderProcessing.Phase4;

public class FraudScoringDemo
{
    private const int OrderCount = 10_000;
    private const int FlagThreshold = 90;

    public static int Score(int orderId)
    {
        long h = orderId;
        for (int i = 0; i < 100_000; i++)
            h = (h * 31 + i) % 1_000_000_007;
        return (int)(h % 100);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("===== 10.4 A: fraud scoring (CPU-bound, 10,000 orders in memory) =====");

        var orders = Enumerable.Range(1, OrderCount).Select(id => new Order { Id = id }).ToList();

        Console.WriteLine("Scoring with a plain sequential loop...");
        var sequentialWatch = Stopwatch.StartNew();
        var sequential = await Task.Run(() => ScoreSequential(orders, ct), ct);
        sequentialWatch.Stop();

        Console.WriteLine($"Scoring in parallel on {Environment.ProcessorCount} cores...");
        var poolBefore = ThreadPool.ThreadCount;
        var parallelWatch = Stopwatch.StartNew();
        var (parallel, threads) = await Task.Run(() => ScoreParallel(orders, ct), ct);
        parallelWatch.Stop();

        var sameResult = sequential.SequenceEqual(parallel);
        var speedUp = sequentialWatch.Elapsed.TotalSeconds / parallelWatch.Elapsed.TotalSeconds;

        Console.WriteLine();
        Console.WriteLine($"Flagged orders:               {parallel.Count}");
        Console.WriteLine($"First five flagged ids:       {string.Join(", ", parallel.Take(5))}");
        Console.WriteLine($"Sequential time:              {sequentialWatch.Elapsed.TotalSeconds:F2} s");
        Console.WriteLine($"Parallel time:                {parallelWatch.Elapsed.TotalSeconds:F2} s");
        Console.WriteLine($"Speed-up:                     {speedUp:F2}x");
        Console.WriteLine($"Distinct threads (parallel):  {threads}");
        Console.WriteLine($"Environment.ProcessorCount:   {Environment.ProcessorCount}");

        if (sameResult && parallel.Count == 1005)
            Log.Success("OK       both runs found the same 1005 flagged orders");
        else
            Log.Error($"MISMATCH sequential {sequential.Count}, parallel {parallel.Count}, same ids: {sameResult} (expected 1005)");

        PerfReport.Print("10.4 A sequential", sequentialWatch.Elapsed, 1, poolBefore, poolBefore);
        PerfReport.Print("10.4 A parallel", parallelWatch.Elapsed, threads, poolBefore, ThreadPool.ThreadCount);
    }

    private static List<int> ScoreSequential(List<Order> orders, CancellationToken ct)
    {
        var flagged = new List<int>();

        foreach (var order in orders)
        {
            ct.ThrowIfCancellationRequested();

            if (Score(order.Id) >= FlagThreshold)
                flagged.Add(order.Id);
        }

        return flagged;
    }

    private static (List<int> Flagged, int Threads) ScoreParallel(List<Order> orders, CancellationToken ct)
    {
        var flagged = new ConcurrentBag<int>();
        var threads = new ConcurrentDictionary<int, byte>();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = ct
        };

        Parallel.ForEach(orders, options, order =>
        {
            threads.TryAdd(Environment.CurrentManagedThreadId, 0);

            if (Score(order.Id) >= FlagThreshold)
                flagged.Add(order.Id);
        });

        return (flagged.OrderBy(id => id).ToList(), threads.Count);
    }
}
