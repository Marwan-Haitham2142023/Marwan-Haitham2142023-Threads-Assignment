using System.Diagnostics;

namespace OrderProcessing.Phase3;

public class FireAndForgetDemo
{
    private static Order NewOrder() => new()
    {
        Id = 2,
        CustomerId = 2,
        Items = new List<OrderItem> { new() { ProductId = 2, Quantity = 1, UnitPrice = 800m } },
        SubTotal = 800m,
        Discount = 40m,
        ShippingCost = 7.30m,
        TotalAmount = 767.30m,
        Status = OrderStatus.Completed,
        CreatedAt = DateTime.Now
    };

    private static Customer NewCustomer() => new()
    {
        Id = 2,
        Name = "Sara Mohamed",
        Email = "sara.mohamed@example.com",
        IsActive = true,
        LoyaltyTier = LoyaltyTier.Silver
    };

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("===== 9.7 Fire-and-forget investigation =====");

        await BrokenSlowEmailAsync(ct);
        await BrokenFailingEmailAsync(ct);
        await FixedAsync(ct, emailFails: false);
        await FixedAsync(ct, emailFails: true);
    }

    public async Task RunExitDemoAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("===== 9.7 Fire-and-forget: the app exits before the email is sent =====");

        await DataSeeder.ResetAsync(ct);

        var service = new BrokenOrderService(new OrderRepository(), new EmailService());
        await service.ProcessOrderAsync(NewOrder(), NewCustomer());

        var emails = await new JsonFileStore().ReadAllAsync<EmailMessage>(DataFiles.Emails, ct);
        Console.WriteLine($"emails.json has {emails.Count} record(s) right now.");
        Console.WriteLine("Main ends here, so the process exits. The email task is killed in the middle of sending.");
        Console.WriteLine("Open Data/emails.json after the program ends: it stays empty. The email is lost.");
    }

    private static async Task BrokenSlowEmailAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("--- Broken version: the email is still being sent when the method returns ---");
        await DataSeeder.ResetAsync(ct);

        var service = new BrokenOrderService(new OrderRepository(), new EmailService());
        var watch = Stopwatch.StartNew();

        await service.ProcessOrderAsync(NewOrder(), NewCustomer());
        var after = await CountEmailsAsync(ct);
        Console.WriteLine($"Method returned after {watch.ElapsedMilliseconds} ms. emails.json has {after} record(s). The email is not sent yet.");

        await Task.Delay(1500, ct);
        var later = await CountEmailsAsync(ct);
        Console.WriteLine($"1.5 s later emails.json has {later} record(s). The email was sent late, and nobody waited for it.");
    }

    private static async Task BrokenFailingEmailAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("--- Broken version: the email operation throws an exception ---");
        await DataSeeder.ResetAsync(ct);

        string? unobserved = null;
        void Handler(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            unobserved = e.Exception.InnerException?.Message ?? e.Exception.Message;
            e.SetObserved();
        }

        TaskScheduler.UnobservedTaskException += Handler;
        try
        {
            var failingEmail = new EmailService(new Dictionary<int, int> { [2] = 1 });
            var service = new BrokenOrderService(new OrderRepository(), failingEmail);

            await service.ProcessOrderAsync(NewOrder(), NewCustomer());
            await Task.Delay(1500, ct);

            Console.WriteLine("The email failed, but nothing was printed and no exception reached the caller.");
            Console.WriteLine("The exception is stored inside a Task that nobody awaited.");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(200, ct);

            Console.WriteLine(unobserved is null
                ? "UnobservedTaskException has not fired yet (it only fires when the garbage collector finalizes the task)."
                : $"Only when the task was garbage collected, UnobservedTaskException fired: \"{unobserved}\"");
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Handler;
        }
    }

    private static async Task FixedAsync(CancellationToken ct, bool emailFails)
    {
        Console.WriteLine();
        Console.WriteLine(emailFails
            ? "--- Fixed version: the email operation throws an exception ---"
            : "--- Fixed version: the email works ---");
        await DataSeeder.ResetAsync(ct);

        IEmailService email = emailFails
            ? new EmailService(new Dictionary<int, int> { [2] = 1 })
            : new EmailService();

        var service = new FixedOrderService(new OrderRepository(), email);
        var watch = Stopwatch.StartNew();

        await service.ProcessOrderAsync(NewOrder(), NewCustomer(), ct);

        var count = await CountEmailsAsync(ct);
        Console.WriteLine($"Method returned after {watch.ElapsedMilliseconds} ms. emails.json has {count} record(s).");
    }

    private static async Task<int> CountEmailsAsync(CancellationToken ct)
    {
        var emails = await new JsonFileStore().ReadAllAsync<EmailMessage>(DataFiles.Emails, ct);
        return emails.Count;
    }
}
