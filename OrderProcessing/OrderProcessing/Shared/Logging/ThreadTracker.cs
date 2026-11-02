using System.Collections.Concurrent;

namespace OrderProcessing.Shared.Logging;

public static class ThreadTracker
{
    private static readonly ConcurrentDictionary<int, byte> Seen = new();

    public static void Record() => Seen.TryAdd(Environment.CurrentManagedThreadId, 0);

    public static void Reset() => Seen.Clear();

    public static int Count => Seen.Count;
}
