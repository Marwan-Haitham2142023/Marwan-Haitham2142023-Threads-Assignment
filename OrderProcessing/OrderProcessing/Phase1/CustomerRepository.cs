
namespace OrderProcessing.Phase1;

public class CustomerRepository
{
    public async Task<Customer?> GetByIdAsync(int id, CancellationToken ct)
    {
        var store = new JsonFileStore();

        Log.Info("CustomerRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"CustomerRepository -> Executing query for Customer #{id}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var customers = await store.ReadAllAsync<Customer>(DataFiles.Customers, ct);
        var customer = customers.FirstOrDefault(c => c.Id == id);

        if (customer is null)
            Log.Info($"CustomerRepository -> Customer #{id} not found");
        else
            Log.Success($"CustomerRepository -> Customer #{id} found ({customer.Name})");

        return customer;
    }
}
