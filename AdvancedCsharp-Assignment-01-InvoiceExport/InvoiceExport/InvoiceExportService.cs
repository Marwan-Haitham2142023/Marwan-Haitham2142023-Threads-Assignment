using System.Diagnostics;

namespace InvoiceExport;

public sealed class InvoiceExportService
{
    const int MaxConcurrency = 8;
    readonly OrderDatabase _db;
    readonly CustomerFileStore _customers;
    readonly InvoiceFileWriter _invoices;

    public InvoiceExportService(OrderDatabase db, CustomerFileStore customers, InvoiceFileWriter invoices)
    {
        _db = db;
        _customers = customers;
        _invoices = invoices;
    }

    public async Task ExportAllPendingAsync(CancellationToken ct = default)
    {
        var watch = Stopwatch.StartNew();
        var orders =await _db.GetPendingOrdersAsync();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxConcurrency,
            CancellationToken = ct
        };
        
        await Parallel.ForEachAsync(orders, options, async (order, token) =>
        {
         
            CustomerProfile customer = await _customers.ReadProfileAsync(order.CustomerId, token);
            string path = await _invoices.WriteInvoiceAsync(order, customer, token);
            Console.WriteLine($"Wrote {path}");
        });

        Console.WriteLine($"Export finished in {watch.Elapsed.TotalSeconds:F1} s");
    }
}
