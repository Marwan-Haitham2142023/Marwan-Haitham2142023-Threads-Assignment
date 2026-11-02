

## R1 – Run and observe

Running `dotnet run --project InvoiceExport` prints the start banner, then the five
`Wrote ...invoice-100X.txt` lines one after another (each appears after a visible pause),
and finally `Export finished in 6 s`.

> Note: the numbers below were calculated from the `Thread.Sleep` values in the code
> (800 ms for the pending-orders query, 400 ms per order lookup, 300 ms per customer file read,
> 300 ms per invoice write). Replace them with your real console output when you run it.

## R2 – Problems with the design (when many orders must be exported)

1. **Everything is blocking and sequential.** `foreach` handles one order at a time and every
   step (`Thread.Sleep`, `File.ReadAllText`, `File.WriteAllText`) blocks the thread while it
   just waits for I/O. Time grows linearly with the number of orders:
   about 1.0 s per order, so 10,000 orders ≈ 2.8 hours.
2. **Redundant database call per order (N+1 problem).** `GetPendingOrders()` already returns
   the full orders, yet the loop calls `GetOrderById(order.Id)` again for each one.
   That is 5 extra DB round-trips (400 ms each) for data we already have, and with many orders
   it becomes thousands of unnecessary queries.
3. **The same customer file is read again and again.** Customer 1 has two orders (1001 and 1004)
   and the file is read twice. A customer with many orders is re-read for each one.
4. **Independent work is not overlapped.** Orders don't depend on each other, so waiting for
   order 1 to finish before starting order 2 wastes time.
5. **No cancellation support.** A long nightly export can't be stopped cleanly.

## R3 – The Fix

See the code. Summary of the changes:

**`OrderDatabase.cs`** — `GetPendingOrders` and `GetOrderById` became `async` (using `Task.Delay` instead of `Thread.Sleep`) and now accept a `CancellationToken`.

**`CustomerFileStore.cs`** — `ReadProfileAsync` uses `File.ReadAllTextAsync` and a `ConcurrentDictionary<int, Lazy<Task<...>>>` cache, so each customer file is read once even when several orders need it at the same time.

**`InvoiceFileWriter.cs`** — `WriteInvoiceAsync` uses `File.WriteAllTextAsync`; the output folder is created once in the constructor. Invoice content is unchanged.

**`InvoiceExportService.cs`** — `ExportAllPendingAsync` removes the redundant `GetOrderById` call and processes orders with `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 8`,
so we get speed-up without opening unlimited concurrent operations. 

**`Program.cs`** — `await export.ExportAllPendingAsync();`
## R4 – What changed and why, with console output

**Why each change**

- *async I/O:* the thread is released while waiting instead of being blocked.
- *Parallel processing, capped at 8:* orders are independent, so they run at the same time;
  the cap protects the database and disk when there are thousands of orders.
- *Removing `GetOrderById`:* the order is already loaded, so the second query only adds latency.
- *Customer cache:* removes repeated reads of the same file.

**Before** (expected, sequential: 0.8 + 5 × (0.4 + 0.3 + 0.3) = 6 s)

```
Starting invoice export...
Customer files: <...>/Data
Invoice output: <...>/invoices

Wrote <...>/invoices/invoice-1001.txt
Wrote <...>/invoices/invoice-1002.txt
Wrote <...>/invoices/invoice-1003.txt
Wrote <...>/invoices/invoice-1004.txt
Wrote <...>/invoices/invoice-1005.txt
Export finished in 6 s
```

**After** (expected: 0.8 s for the query + about 0.3 s customer read + 0.3 s write in parallel ≈ 1.8 s)

```
Starting invoice export...
Customer files: <...>/Data
Invoice output: <...>/invoices

Wrote <...>/invoices/invoice-1001.txt
Wrote <...>/invoices/invoice-1002.txt
Wrote <...>/invoices/invoice-1003.txt
Wrote <...>/invoices/invoice-1004.txt
Wrote <...>/invoices/invoice-1005.txt
Export finished in 1.8 s
```

The order of the `Wrote` lines can vary between runs because orders now finish in parallel.
The same five invoice files (`invoice-1001.txt` … `invoice-1005.txt`) are produced with
the same content.
