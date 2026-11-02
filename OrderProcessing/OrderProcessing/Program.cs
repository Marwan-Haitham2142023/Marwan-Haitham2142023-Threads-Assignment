using System.Text;
using OrderProcessing.Phase3;
using OrderProcessing.Phase4;

Console.OutputEncoding = Encoding.UTF8;

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

int? cancelAfterMs = null;
var cancelIndex = Array.IndexOf(args, "--cancel-after");
if (cancelIndex >= 0 && cancelIndex + 1 < args.Length && int.TryParse(args[cancelIndex + 1], out var milliseconds))
{
    cancelAfterMs = milliseconds;
}

var phase = args.Length > 0 && !args[0].StartsWith("--")
    ? args[0].ToLowerInvariant()
    : AskForPhase();

if (cancelAfterMs is { } delay)
{
    cts.CancelAfter(delay);
}

var ct = cts.Token;

if (phase == "all")
{
    foreach (var name in new[] { "phase1", "phase2", "phase3", "phase4" })
    {
        if (ct.IsCancellationRequested)
            break;

        await RunPhaseAsync(name, ct);
    }
}
else if (phase != "exit")
{
    if (!await RunPhaseAsync(phase, ct))
    {
        Console.WriteLine($"Unknown phase '{phase}'. Try: phase1, phase2, phase3, phase4, all (or compare, fireforget, fireforget-exit, pricing, blocking)");
    }
}

static string AskForPhase()
{
    Console.WriteLine();
    Console.WriteLine("===== Order Processing System =====");
    Console.WriteLine("  1) Phase 1 - async data access layer");
    Console.WriteLine("  2) Phase 2 - concurrent processing");
    Console.WriteLine("  3) Phase 3 - full checkout workflow");
    Console.WriteLine("  4) Phase 4 - scale-up");
    Console.WriteLine("  5) All phases");
    Console.WriteLine("  0) Exit");
    Console.Write("Choose a number: ");

    return Console.ReadLine()?.Trim() switch
    {
        "1" => "phase1",
        "2" => "phase2",
        "3" => "phase3",
        "4" => "phase4",
        "5" => "all",
        _ => "exit"
    };
}

static async Task<bool> RunPhaseAsync(string phase, CancellationToken ct)
{
    switch (phase)
    {
        case "phase1":
            await new Phase1Runner().RunAsync(ct);
            return true;
        case "phase2":
            await new Phase2Runner().RunAsync(ct);
            return true;
        case "phase3":
            await new Phase3Runner().RunAsync(ct);
            return true;
        case "phase4":
            await new Phase4Runner().RunAsync(ct);
            return true;
        case "compare":
            await new SyncVsAsyncRunner().RunAsync(ct);
            return true;
        case "fireforget":
            await new FireAndForgetDemo().RunAsync(ct);
            return true;
        case "fireforget-exit":
            await new FireAndForgetDemo().RunExitDemoAsync(ct);
            return true;
        case "pricing":
            await new PricingTimingRunner().RunAsync(ct);
            return true;
        case "blocking":
            BlockingDemo.Run();
            return true;
        default:
            return false;
    }
}
