
namespace OrderProcessing.Phase2;

public static class BlockingDemo
{
    public static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("===== .Result / .Wait() demo =====");

        Task<int> slow = Task.Run(async () =>
        {
            await Task.Delay(1000);
            return 42;
        });

        Log.Info("Calling .Result on an unfinished task. This thread is now blocked...");
        int value = slow.Result;
        Log.Success($"Got {value}. The thread did nothing for about 1 second.");

        Task<int> broken = Task.Run<int>(async () =>
        {
            await Task.Delay(200);
            throw new InvalidOperationException("Something went wrong");
        });

        try
        {
            broken.Wait();
        }
        catch (AggregateException ex)
        {
            Log.Error($".Wait() gives an AggregateException. Real error inside: {ex.InnerException?.Message}");
        }

        try
        {
            _ = broken.Result;
        }
        catch (AggregateException ex)
        {
            Log.Error($".Result also gives an AggregateException. Real error inside: {ex.InnerException?.Message}");
        }

        try
        {
            broken.GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            Log.Error($"GetAwaiter().GetResult() gives the real exception: {ex.Message}");
        }
    }
}
