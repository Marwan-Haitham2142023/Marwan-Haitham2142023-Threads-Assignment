using System.Diagnostics;
using OrderProcessing.Phase3;

namespace OrderProcessing.Phase4;

public class EmailResendDemo
{
    private const int FirstOrderId = 1001;
    private const int EmailCount = 30;
    private const int MaxAtTheSameTime = 4;

    private int _running;
    private int _highestRunning;

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("===== 10.4 B: email re-send (I/O-bound, at most 4 sends at the same time) =====");

        IEmailService email = new EmailService();

        var tests = Enumerable.Range(FirstOrderId, EmailCount)
            .Select(id => (Order: NewOrder(id), Customer: NewCustomer(id)))
            .ToList();

        ThreadTracker.Reset();
        var poolBefore = ThreadPool.ThreadCount;
        var watch = Stopwatch.StartNew();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxAtTheSameTime,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(tests, options, async (item, token) =>
        {
            var now = Interlocked.Increment(ref _running);
            RememberHighest(now);

            try
            {
                await email.SendConfirmationAsync(item.Order, item.Customer, token);
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        });

        watch.Stop();

        var saved = await new JsonFileStore().ReadAllAsync<EmailMessage>(DataFiles.Emails, CancellationToken.None);
        var allOnce = Enumerable.Range(FirstOrderId, EmailCount).All(id => saved.Count(e => e.OrderId == id) == 1);

        Console.WriteLine();
        Console.WriteLine($"Emails sent:                              {EmailCount}");
        Console.WriteLine($"Highest number of sends at the same time: {_highestRunning}");
        Console.WriteLine($"Total time:                               {watch.Elapsed.TotalSeconds:F1} seconds (expected about 30 / 4 x 1 s = 8 s)");

        if (_highestRunning == MaxAtTheSameTime)
            Log.Success("OK       the highest number of running sends was exactly 4");
        else
            Log.Error($"MISMATCH the highest number of running sends was {_highestRunning}, expected 4");

        if (allOnce)
            Log.Success("OK       all 30 emails are in emails.json exactly once");
        else
            Log.Error("MISMATCH some of the 30 emails are missing or duplicated in emails.json");

        PerfReport.Print("10.4 B email re-send", watch.Elapsed, ThreadTracker.Count, poolBefore, ThreadPool.ThreadCount);
    }

    private void RememberHighest(int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref _highestRunning)))
        {
            if (Interlocked.CompareExchange(ref _highestRunning, value, current) == current)
                break;
        }
    }

    private static Order NewOrder(int id) => new()
    {
        Id = id,
        CustomerId = 1,
        TotalAmount = 100m,
        Status = OrderStatus.Completed,
        CreatedAt = DateTime.Now
    };

    private static Customer NewCustomer(int id) => new()
    {
        Id = 1,
        Name = $"Test Customer {id}",
        Email = $"test.customer{id}@example.com",
        IsActive = true,
        LoyaltyTier = LoyaltyTier.Bronze
    };
}
