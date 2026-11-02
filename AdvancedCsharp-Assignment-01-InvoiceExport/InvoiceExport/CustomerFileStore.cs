using System.Collections.Concurrent;

namespace InvoiceExport;

public sealed class CustomerFileStore
{
    readonly string _folder;

    readonly ConcurrentDictionary<int,Lazy<Task<CustomerProfile>>> _Cache=new(); 
    public CustomerFileStore(string folder) => _folder = folder;

 
    public async Task<CustomerProfile> ReadProfileAsync(int customerId, CancellationToken ct = default)
    {
        var lazy = _Cache.GetOrAdd(customerId, id => new Lazy<Task<CustomerProfile>>(() => LoadProfileAsync(id, ct)));
        return await lazy.Value;
    }

     async Task<CustomerProfile> LoadProfileAsync(int customerId, CancellationToken ct = default)
    {
        string path = Path.Combine(_folder, $"customer-{customerId}.txt");
        await Task.Delay(300);
        string text = await File.ReadAllTextAsync(path);
        string[] parts = text.Split('|');

        return new CustomerProfile(int.Parse(parts[0]), parts[1], parts[2]);
    }
  
}
