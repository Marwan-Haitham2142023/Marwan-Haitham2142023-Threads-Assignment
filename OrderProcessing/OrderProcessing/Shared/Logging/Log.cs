namespace OrderProcessing.Shared.Logging;

public static class Log
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write(message, null);
    public static void Success(string message) => Write(message, ConsoleColor.Green);
    public static void Error(string message) => Write(message, ConsoleColor.Red);

    public static void Plain(string message)
    {
        lock (Gate)
        {
            Console.WriteLine(message);
        }
    }

    private static void Write(string message, ConsoleColor? color)
    {
        ThreadTracker.Record();

        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] {message}";

        lock (Gate)
        {
            if (color is { } c)
            {
                Console.ForegroundColor = c;
                Console.WriteLine(line);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(line);
            }
        }
    }
}
