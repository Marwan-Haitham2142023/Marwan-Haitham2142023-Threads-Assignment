namespace OrderProcessing.Shared.Logging;

public static class PerfReport
{
    public static void Print(string label, TimeSpan time, int distinctThreads, int poolBefore, int poolAfter)
    {
        Log.Plain($"[Perf] {label}: {time.TotalSeconds:F1} s | distinct threads: {distinctThreads} | ThreadPool.ThreadCount: {poolBefore} -> {poolAfter}");
    }
}
