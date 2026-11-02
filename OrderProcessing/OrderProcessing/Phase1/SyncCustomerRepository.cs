
namespace OrderProcessing.Phase1;

public class SyncCustomerRepository
{
    public Customer? GetById(int id)
    {
        var store = new JsonFileStore();

        Log.Info("SyncCustomerRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info($"SyncCustomerRepository -> Executing query for Customer #{id}...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        var customers = store.ReadAll<Customer>(DataFiles.Customers);
        var customer = customers.FirstOrDefault(c => c.Id == id);

        if (customer is null)
            Log.Info($"SyncCustomerRepository -> Customer #{id} not found");
        else
            Log.Success($"SyncCustomerRepository -> Customer #{id} found ({customer.Name})");

        return customer;
    }
}
