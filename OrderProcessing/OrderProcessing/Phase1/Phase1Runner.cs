using System.Diagnostics;

namespace OrderProcessing.Phase1;

public class Phase1Runner
{
    public async Task RunAsync(CancellationToken ct)
    {
        var incomingRepo = new IncomingOrderRepository();
        var orderRepo = new OrderRepository();

        var syncValidator = new SyncOrderValidator(
            new SyncCustomerRepository(),
            new SyncProductRepository(),
            new SyncCouponRepository());

        var asyncValidator = new OrderValidator(
            new CustomerRepository(),
            new ProductRepository(),
            new CouponRepository(),
            orderRepo);

        int total = 0;
        int finished = 0;

        try
        {
            await DataSeeder.ResetAsync(ct);
            var orders = await incomingRepo.GetAllAsync(ct);
            total = orders.Count;

            Console.WriteLine();
            Console.WriteLine("===== Sync run =====");
            var syncWatch = Stopwatch.StartNew();
            foreach (var order in orders)
            {
                ct.ThrowIfCancellationRequested();
                syncValidator.ValidateOrder(order);
            }
            syncWatch.Stop();

            await DataSeeder.ResetAsync(ct);
            orders = await incomingRepo.GetAllAsync(ct);

            Console.WriteLine();
            Console.WriteLine("===== Async run =====");
            var asyncWatch = Stopwatch.StartNew();
            foreach (var order in orders)
            {
                await asyncValidator.ValidateOrderAsync(order, ct);
                finished++;
            }
            asyncWatch.Stop();

            var all = await orderRepo.GetAllAsync(ct);
            var validated = await orderRepo.GetByStatusAsync(OrderStatus.Validated, ct);
            var rejected = await orderRepo.GetByStatusAsync(OrderStatus.Rejected, ct);

            Console.WriteLine();
            Console.WriteLine("===== Validation Summary =====");
            Console.WriteLine($"Total Orders: {total}");
            Console.WriteLine($"Validated:    {validated.Count}");
            Console.WriteLine($"Rejected:     {rejected.Count}");
            foreach (var order in rejected.OrderBy(o => o.Id))
            {
                var label = $"Order #{order.Id}:".PadRight(11);
                Console.WriteLine($"  - {label}{order.FailureReason}");
            }
            Console.WriteLine($"Sync Time:  {syncWatch.Elapsed.TotalSeconds:F1} seconds");
            Console.WriteLine($"Async Time: {asyncWatch.Elapsed.TotalSeconds:F1} seconds");
            Console.WriteLine($"orders.json now contains {all.Count} records.");
            SampleOutput.Save("phase1");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
            Console.WriteLine($"Run cancelled — {finished} orders finished, {total - finished} cancelled.");
        }
    }
}
