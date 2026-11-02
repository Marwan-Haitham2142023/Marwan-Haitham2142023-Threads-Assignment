using System.Diagnostics;

namespace OrderProcessing.Phase2;

public class Phase2Runner
{
    public async Task RunAsync(CancellationToken ct)
    {
        var incomingRepo = new IncomingOrderRepository();
        var orderRepo = new OrderRepository();
        var productRepo = new ProductRepository();
        var customerRepo = new CustomerRepository();
        var couponRepo = new CouponRepository();

        var validator = new OrderValidator(customerRepo, productRepo, couponRepo, orderRepo);
        var pricing = new PricingService(customerRepo, couponRepo, new[] { 7 });
        var processor = new OrderProcessorV2(productRepo, orderRepo, new OrderPricer(pricing), new InvoiceGenerator());
        var reportGenerator = new ReportGenerator(orderRepo, productRepo);

        var orders = new List<Order>();

        try
        {
            await DataSeeder.ResetAsync(ct);
            await new RaceConditionDemo().RunAsync(ct);
            await DataSeeder.ResetAsync(ct);

            orders = await incomingRepo.GetAllAsync(ct);
            var pricingCopy = Copy(orders.First(o => o.Id == 1));
            await new PricingTimingRunner().CompareAsync(pricingCopy, ct);

            var watch = Stopwatch.StartNew();

            Console.WriteLine();
            Console.WriteLine("===== Validating all orders concurrently =====");
            try
            {
                await Task.WhenAll(orders.Select(o => validator.ValidateOrderAsync(o, ct)).ToList());
            }
            catch (OperationCanceledException)
            {
                foreach (var order in orders.Where(o => o.Status is OrderStatus.Pending or OrderStatus.Validated))
                {
                    order.Status = OrderStatus.Pending;
                    order.FailureReason = "Cancelled";
                    await orderRepo.SaveAsync(order, CancellationToken.None);
                }

                PrintCancelled(orders);
                return;
            }

            var validated = orders.Where(o => o.Status == OrderStatus.Validated).ToList();
            Console.WriteLine($"{validated.Count} orders are Validated and ready to process.");

            Console.WriteLine();
            Console.WriteLine("===== Processing validated orders concurrently =====");
            var tasks = validated.Select(o => processor.ProcessValidatedOrderAsync(o, ct)).ToList();

            Console.WriteLine($"Status of task[0] right after creation: {tasks[0].Status}");

            try
            {
                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException)
            {
            }

            Console.WriteLine("Status of every task after 1 second (still running):");
            for (int i = 0; i < tasks.Count; i++)
                Console.WriteLine($"  task[{i}] (Order #{validated[i].Id}): {tasks[i].Status}");

            for (int i = 0; i < tasks.Count; i++)
            {
                var order = validated[i];
                try
                {
                    await tasks[i];
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"task[{i}] (Order #{order.Id}) ended with status: {tasks[i].Status}");
                    await processor.ReleaseReservedStockAsync(order, CancellationToken.None);
                    order.Status = OrderStatus.Pending;
                    order.FailureReason = "Cancelled";
                    await orderRepo.SaveAsync(order, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"task[{i}] (Order #{order.Id}) ended with status: {tasks[i].Status}");
                    Log.Error($"Phase2Runner -> Order #{order.Id} failed: {ex.Message}");
                    await processor.ReleaseReservedStockAsync(order, CancellationToken.None);
                    order.Status = OrderStatus.Failed;
                    order.FailureReason = ex.Message;
                    await orderRepo.SaveAsync(order, CancellationToken.None);
                }
            }

            watch.Stop();

            Console.WriteLine();
            Console.WriteLine("===== Final status of every task =====");
            for (int i = 0; i < tasks.Count; i++)
                Console.WriteLine($"  task[{i}] (Order #{validated[i].Id}): {tasks[i].Status}");
            Console.WriteLine($"Total time (validate + process, all concurrent): {watch.Elapsed.TotalSeconds:F1} seconds");

            if (ct.IsCancellationRequested)
            {
                PrintCancelled(orders);
                return;
            }

            Console.WriteLine();
            var reportTask = reportGenerator.GenerateAsync(ct);
            Console.WriteLine("Generating report...");
            var report = await reportTask;
            PrintReport(report);

            var saved = await orderRepo.GetAllAsync(ct);
            Console.WriteLine();
            Console.WriteLine($"orders.json contains {saved.Count} records: " +
                $"{saved.Count(o => o.Status == OrderStatus.Completed)} Completed, " +
                $"{saved.Count(o => o.Status == OrderStatus.Rejected)} Rejected, " +
                $"{saved.Count(o => o.Status == OrderStatus.Failed)} Failed.");
            Console.WriteLine(saved.Count == 20
                ? "OK: exactly 20 records, no write was lost."
                : "PROBLEM: orders.json should have exactly 20 records.");
            SampleOutput.Save("phase2");
        }
        catch (OperationCanceledException)
        {
            PrintCancelled(orders);
        }
    }

    private static void PrintCancelled(List<Order> orders)
    {
        var finished = orders.Count(o => o.Status is OrderStatus.Completed or OrderStatus.Rejected or OrderStatus.Failed);
        var cancelled = orders.Count(o => o.Status == OrderStatus.Pending);
        Console.WriteLine();
        Console.WriteLine($"Run cancelled — {finished} orders finished, {cancelled} cancelled.");
    }

    private static void PrintReport(Report report)
    {
        Console.WriteLine();
        Console.WriteLine("===== Report =====");
        Console.WriteLine($"Total orders:          {report.TotalOrders}");
        Console.WriteLine($"Completed:             {report.CompletedCount}");
        Console.WriteLine($"Rejected:              {report.RejectedCount}");
        Console.WriteLine($"Failed:                {report.FailedCount}");
        Console.WriteLine($"Payment failed:        {report.PaymentFailedCount}");
        Console.WriteLine($"Total revenue:         {report.TotalRevenue:F2}");
        Console.WriteLine($"Average order value:   {report.AverageOrderValue:F2}");
        Console.WriteLine($"Top selling product:   {report.TopSellingProduct} ({report.TopSellingQuantity} units)");
    }

    private static Order Copy(Order order) => new()
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        CouponCode = order.CouponCode,
        Status = order.Status,
        CreatedAt = order.CreatedAt,
        Items = order.Items
            .Select(i => new OrderItem { ProductId = i.ProductId, Quantity = i.Quantity, UnitPrice = i.UnitPrice })
            .ToList()
    };
}
