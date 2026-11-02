using System.Diagnostics;
using OrderProcessing.Phase3;

namespace OrderProcessing.Phase4;

public class CustomerDashboardService
{
    private readonly CustomerService _customers;
    private readonly OrderRepository _orders;
    private readonly HashSet<int> _failPaymentsForCustomerIds;
    private readonly JsonFileStore _store = new();

    public CustomerDashboardService(
        CustomerService customers,
        OrderRepository orders,
        IEnumerable<int>? failPaymentsForCustomerIds = null)
    {
        _customers = customers;
        _orders = orders;
        _failPaymentsForCustomerIds = new HashSet<int>(failPaymentsForCustomerIds ?? Array.Empty<int>());
    }

    public async Task<CustomerDashboard> LoadAsync(int customerId, CancellationToken ct)
    {
        var profileTask = TimedAsync("profile", () => _customers.GetCustomerAsync(customerId, ct));
        var ordersTask = TimedAsync("orders", () => LoadOrdersAsync(customerId, ct));
        var paymentsTask = TimedAsync("payments", () => LoadPaymentsAsync(customerId, ct));
        var emailsTask = TimedAsync("emails", () => LoadEmailsAsync(customerId, ct));

        var all = Task.WhenAll(profileTask, ordersTask, paymentsTask, emailsTask);

        try
        {
            await all;
        }
        catch
        {
            ct.ThrowIfCancellationRequested();

            if (all.Exception is { } aggregate)
                throw aggregate;

            throw;
        }

        return new CustomerDashboard
        {
            Customer = await profileTask,
            Orders = await ordersTask,
            Payments = await paymentsTask,
            Emails = await emailsTask
        };
    }

    private async Task<List<Order>> LoadOrdersAsync(int customerId, CancellationToken ct)
    {
        var orders = await _orders.GetAllAsync(ct);
        return orders.Where(o => o.CustomerId == customerId).OrderBy(o => o.Id).ToList();
    }

    private async Task<List<PaymentTransaction>> LoadPaymentsAsync(int customerId, CancellationToken ct)
    {
        await SimulateQueryAsync("payments", ct);

        if (_failPaymentsForCustomerIds.Contains(customerId))
            throw new TimeoutException("Payments store timeout");

        var orderIds = await OrderIdsOfAsync(customerId, ct);
        var payments = await _store.ReadAllAsync<PaymentTransaction>(DataFiles.Payments, ct);
        return payments.Where(p => orderIds.Contains(p.OrderId)).OrderBy(p => p.OrderId).ToList();
    }

    private async Task<List<EmailMessage>> LoadEmailsAsync(int customerId, CancellationToken ct)
    {
        await SimulateQueryAsync("emails", ct);

        var orderIds = await OrderIdsOfAsync(customerId, ct);
        var emails = await _store.ReadAllAsync<EmailMessage>(DataFiles.Emails, ct);
        return emails.Where(e => orderIds.Contains(e.OrderId)).OrderBy(e => e.OrderId).ToList();
    }

    private async Task<HashSet<int>> OrderIdsOfAsync(int customerId, CancellationToken ct)
    {
        var orders = await _store.ReadAllAsync<Order>(DataFiles.Orders, ct);
        return orders.Where(o => o.CustomerId == customerId).Select(o => o.Id).ToHashSet();
    }

    private static async Task SimulateQueryAsync(string what, CancellationToken ct)
    {
        Log.Info($"CustomerDashboardService -> Opening connection ({what})...");
        await Task.Delay(200, ct);

        Log.Info($"CustomerDashboardService -> Executing query ({what})...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);
    }

    private static async Task<T> TimedAsync<T>(string name, Func<Task<T>> load)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var result = await load();
            Log.Success($"CustomerDashboardService -> {name} loaded in {watch.ElapsedMilliseconds} ms");
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error($"CustomerDashboardService -> {name} failed in {watch.ElapsedMilliseconds} ms: {ex.Message}");
            throw;
        }
    }
}
