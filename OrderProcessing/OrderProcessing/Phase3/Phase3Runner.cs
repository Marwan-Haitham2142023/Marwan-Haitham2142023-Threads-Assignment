using System.Diagnostics;

namespace OrderProcessing.Phase3;

public class Phase3Runner
{
    public async Task RunAsync(CancellationToken ct)
    {
        var orders = new List<Order>();

        try
        {
            await DataSeeder.ResetAsync(ct);

            orders = await new IncomingOrderRepository().GetAllAsync(ct);

            var processor = CreateProcessor();

            Console.WriteLine();
            Console.WriteLine("===== Phase 3: full checkout for all orders =====");

            var watch = Stopwatch.StartNew();
            var tasks = orders.Select(o => processor.ProcessOrderAsync(o, ct)).ToList();

            foreach (var task in tasks)
            {
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    Log.Error($"Phase3Runner -> A checkout task failed: {ex.Message}");
                }
            }

            watch.Stop();

            PrintTable(orders);
            Console.WriteLine($"Total time: {watch.Elapsed.TotalSeconds:F1} seconds");

            if (ct.IsCancellationRequested)
            {
                PrintCancelled(orders);
                return;
            }

            await PrintFileCheckAsync(CancellationToken.None);
            SampleOutput.Save("phase3");

            await new SyncVsAsyncRunner().RunAsync(ct);
            await new FireAndForgetDemo().RunAsync(ct);
        }
        catch (OperationCanceledException)
        {
            PrintCancelled(orders);
        }
    }

    public static OrderProcessor CreateProcessor(bool withFailures = true)
    {
        var customerRepo = new CustomerRepository();
        var productRepo = new ProductRepository();
        var couponRepo = new CouponRepository();

        var orderRepo = withFailures
            ? new OrderRepository(failSaveForOrderIds: new[] { 13 })
            : new OrderRepository();

        var pricing = new PricingService(customerRepo, couponRepo, Array.Empty<int>());

        IPaymentGateway payments = withFailures
            ? new PaymentGateway(timeoutOrderIds: new[] { 5 }, declineOrderIds: new[] { 8 })
            : new PaymentGateway(Array.Empty<int>(), Array.Empty<int>());

        IEmailService email = withFailures
            ? new EmailService(failAttempts: new Dictionary<int, int> { [3] = 2, [17] = int.MaxValue })
            : new EmailService();

        return new OrderProcessor(
            new CustomerService(customerRepo),
            new InventoryService(productRepo),
            pricing,
            payments,
            orderRepo,
            new EmailRetryService(email));
    }

    private static void PrintTable(List<Order> orders)
    {
        Console.WriteLine();
        Console.WriteLine($"{"Order",5}  {"Status",-14} {"Total",10}  Reason");
        Console.WriteLine(new string('-', 80));
        foreach (var order in orders.OrderBy(o => o.Id))
        {
            Console.WriteLine($"{order.Id,5}  {order.Status,-14} {order.TotalAmount,10:F2}  {order.FailureReason}");
        }
    }

    private static void PrintCancelled(List<Order> orders)
    {
        var cancelled = orders.Count(o => o.Status == OrderStatus.Pending);
        var finished = orders.Count - cancelled;
        Console.WriteLine();
        Console.WriteLine($"Run cancelled — {finished} orders finished, {cancelled} cancelled.");
    }

    public static async Task PrintFileCheckAsync(CancellationToken ct)
    {
        var store = new JsonFileStore();
        var saved = await store.ReadAllAsync<Order>(DataFiles.Orders, ct);
        var payments = await store.ReadAllAsync<PaymentTransaction>(DataFiles.Payments, ct);
        var emails = await store.ReadAllAsync<EmailMessage>(DataFiles.Emails, ct);
        var retry = await store.ReadAllAsync<EmailMessage>(DataFiles.EmailRetry, ct);
        var products = await store.ReadAllAsync<Product>(DataFiles.Products, ct);

        Console.WriteLine();
        Console.WriteLine("===== Files check (expected end state) =====");

        Check("orders.json: 20 records", saved.Count == 20, $"{saved.Count}");
        Check("orders.json: 11 Completed", saved.Count(o => o.Status == OrderStatus.Completed) == 11,
            $"{saved.Count(o => o.Status == OrderStatus.Completed)}");
        Check("orders.json: 6 Rejected", saved.Count(o => o.Status == OrderStatus.Rejected) == 6,
            $"{saved.Count(o => o.Status == OrderStatus.Rejected)}");
        Check("orders.json: 2 PaymentFailed (#5, #8)", saved.Count(o => o.Status == OrderStatus.PaymentFailed) == 2,
            $"{saved.Count(o => o.Status == OrderStatus.PaymentFailed)}");
        Check("orders.json: 1 Failed (#13)", saved.Count(o => o.Status == OrderStatus.Failed) == 1,
            $"{saved.Count(o => o.Status == OrderStatus.Failed)}");

        Check("payments.json: 13 records", payments.Count == 13, $"{payments.Count}");
        Check("payments.json: 11 Captured", payments.Count(p => p.Status == PaymentStatus.Captured) == 11,
            $"{payments.Count(p => p.Status == PaymentStatus.Captured)}");
        Check("payments.json: 1 Declined (#8)", payments.Count(p => p.Status == PaymentStatus.Declined) == 1,
            $"{payments.Count(p => p.Status == PaymentStatus.Declined)}");
        Check("payments.json: 1 Refunded (#13)", payments.Count(p => p.Status == PaymentStatus.Refunded) == 1,
            $"{payments.Count(p => p.Status == PaymentStatus.Refunded)}");
        Check("payments.json: none for #5", payments.All(p => p.OrderId != 5), "");

        Check("emails.json: 10 records, none for #17", emails.Count == 10 && emails.All(e => e.OrderId != 17), $"{emails.Count}");
        Check("emails.json: #3 has Attempts = 3", emails.Any(e => e.OrderId == 3 && e.Attempts == 3), "");

        Check("email_retry.json: 1 record (#17), Attempts = 4",
            retry.Count == 1 && retry[0].OrderId == 17 && retry[0].Attempts == 4 && !string.IsNullOrEmpty(retry[0].LastError),
            $"{retry.Count}");

        var keyboard = products.FirstOrDefault(p => p.Id == 4)?.StockQuantity;
        Check("products.json: Mechanical Keyboard stock = 0", keyboard == 0, $"{keyboard}");
    }

    private static void Check(string label, bool ok, string actual)
    {
        var text = ok ? "OK      " : "MISMATCH";
        var line = $"{text} {label}" + (ok || actual == "" ? "" : $" (actual: {actual})");
        if (ok)
            Log.Success(line);
        else
            Log.Error(line);
    }
}
