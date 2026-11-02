# Order Processing System - Phase 1

This is Phase 1 of the e-commerce order processing project. It builds an async data access layer on top of JSON files, and then validates 20 sample orders in two ways: one by one with sync code, and one by one with async code.

## How to run

```
dotnet run -- phase1
```

Phases: `phase1`, `phase2`, `phase3`, `phase4` or `all`. With no phase name, a numbered menu is shown.

You can also cancel a run:

- Press Ctrl+C to cancel the run.
- Or use `dotnet run -- phase1 --cancel-after 5000` to cancel after 5000 milliseconds.

When a run is cancelled, it prints how many orders finished and how many were cancelled. The JSON files stay valid.

## Project structure

- Shared/Models: the models and enums (Customer, Product, Coupon, Order, and so on).
- Shared/Storage: DataFiles (file names), JsonConfig (shared JSON options), JsonFileStore (reads and writes the files), DataFileCorruptedException, and DataSeeder (resets the Data folder with the seed data).
- Shared/Logging: Log (colored console logger), ThreadTracker, PerfReport and SampleOutput.
- Shared/Exceptions: the custom exceptions (CustomerNotFound, InvalidCoupon, DatabaseWrite, SmtpUnavailable, ShippingProviderUnavailable).
- Phase1: the async and sync repositories, OrderValidator, SyncOrderValidator and Phase1Runner.
- Phase2: pricing, discount and shipping services, OrderProcessorV2, InvoiceGenerator, ReportGenerator, and the runners (Phase2Runner, RaceConditionDemo, PricingTimingRunner, BlockingDemo).
- Phase3: the checkout workflow (OrderProcessor, payment, email, inventory, customer services), the sync versions, the sync vs async comparison and the fire-and-forget demos.
- Phase4: live checkout, customer dashboard, shipping carriers race, fraud scoring and email re-send.
- sample-output: the final JSON files of each phase.
- Program.cs: the menu and the command line (`phase1`, `phase2`, `phase3`, `phase4`, `all`, and `--cancel-after <ms>`).

## JsonFileStore API

JsonFileStore is the only class that reads and writes the JSON files. Repositories use it and never touch the files directly. It has no delays inside it. The simulated delays live in the repositories.

Async methods (all take a CancellationToken):

- `ReadAllAsync<T>(fileName, ct)` reads the whole file and returns a list of T.
- `WriteAllAsync<T>(fileName, items, ct)` writes a whole list to the file.
- `UpdateAsync<T, TResult>(fileName, change, ct)` reads the list, lets you change it, and saves it back. It also returns a value from your change function. This is used for saving orders and for reserving stock.

Sync methods (only used by the sync comparison classes):

- `ReadAll<T>(fileName)`
- `WriteAll<T>(fileName, items)`
- `Update<T, TResult>(fileName, change)`

How it follows the rules:

- Missing file: reading a file that does not exist returns an empty list.
- Bad JSON: reading invalid JSON throws DataFileCorruptedException, and the message contains the file name.
- Many tasks at the same time: the store uses one lock (a SemaphoreSlim). Only one read, write, or update runs at a time. UpdateAsync keeps the lock for the whole read, change, and write, so no update is lost. This is what stops two orders from taking the last keyboard.
- Crash safety: the store writes to a temporary file first, then replaces the real file. If the program stops in the middle of a write, the real file is never half-written.

## Phase 1 result

The run gave the expected split:

- Total orders: 20
- Validated: 15
- Rejected: 5

The rejected orders:

- Order 4: Customer is inactive
- Order 9: Insufficient stock for 'Wireless Mouse'
- Order 12: Coupon 'OLD20' has expired
- Order 15: Customer #99 not found
- Order 18: Coupon 'FAKE50' does not exist

Times from my run:

- Sync time: 35.6 seconds
- Async time: 54.8 seconds

Why the async time is longer: the async run also saves every order to orders.json, and the sync run does not save anything. One save takes 900 ms (200 + 100 + 400 + 200). For 20 orders that is 18 seconds, which is exactly the gap between the two times. The validation work itself takes the same time in both.

## Questions

### 1. Why are the sync and async times almost the same when orders are awaited one after another?

Because awaiting one order after another does not run anything at the same time. Each order must finish before the next one starts, so the total time is still the sum of all the delays. This is true for both versions.

Async does not make a single operation faster. It only frees the thread while it waits. The time saving comes when we run many orders at the same time, for example with Task.WhenAll. In this phase we do not do that.

### 2. Is the thread ID before an await always the same as after it? Why?

No, it is not always the same. In a console app there is no special thread that the code must return to. When the awaited task finishes, the rest of the method continues on any free thread from the thread pool. Sometimes it is the same thread, but we cannot count on it.

Evidence from my own log lines. In the async run, the thread changed after an await:

```
[22:56:19.667] [Thread 10] CustomerRepository -> Executing query for Customer #1...
[22:56:20.042] [Thread 15] CustomerRepository -> Customer #1 found (Ahmed Ali)
```

The line before Task.Delay ran on Thread 10. The next line of the same method, after the await, ran on Thread 15. Another example:

```
[22:56:20.867] [Thread 15] ProductRepository -> Executing query for Product #3...
[22:56:21.259] [Thread 14] ProductRepository -> Product #3 found (Wireless Mouse)
```

Here it moved from Thread 15 to Thread 14. It does not change every time (many lines stay on the same thread), so we can never rely on it.

In the sync run, every line used Thread 10, because Thread.Sleep never gives the thread back:

```
[22:55:43.183] [Thread 10] SyncCustomerRepository -> Opening connection...
[22:55:43.781] [Thread 10] SyncCustomerRepository -> Customer #1 found (Ahmed Ali)
```

### 3. What does the thread do while Task.Delay() is waiting? And while Thread.Sleep() is waiting?

With Task.Delay, the thread does not wait. The method gives the thread back to the thread pool, and a timer wakes the method up later. While it waits, the thread can do other work.

With Thread.Sleep, the thread is blocked. It stays stuck doing nothing for the whole time, and nobody else can use it. This is why sync code scales badly: many waiting operations need many blocked threads.

### 4. Why should a repository method return Task<T> instead of T?

A repository method does slow work, like talking to a database. If it returned T directly, the caller would have to wait and block a thread until the answer is ready.

Task<T> says "the answer will come later". The caller can await it without blocking a thread. A Task also carries errors and cancellation, and it lets us start many operations together and wait for all of them. With a plain T we lose all of this.

### 5. What is the difference between File.ReadAllText and File.ReadAllTextAsync? Does the async version occupy a thread while the disk is reading?

File.ReadAllText blocks the calling thread until the whole file is read. The thread waits and does nothing else.

File.ReadAllTextAsync returns a Task right away. The thread is free to do other work, and the method continues when the read is done.

Normally, the async version does not occupy a thread while the disk is reading. The operating system does the reading and tells .NET when it is finished. Only a short moment of work happens on a thread at the start and at the end. (On some systems, small parts of file work can still use a pool thread inside, but the calling code does not wait on one.)

## Phase 2 - Concurrent Processing

How to run: `dotnet run -- phase2`. Extra command: `pricing` (concurrent vs sequential pricing) and `blocking` (the only place where .Result and .Wait() are used).

What the run does: reset the data, race demo, pricing comparison for order #1, validate all 20 orders at the same time, process the 15 validated orders at the same time, then make a report.

Results of my run:

- Concurrent pricing for order #1: 3.3 seconds. Sequential pricing for the same order: 5.5 seconds. Both gave the same total, 980.00. Concurrent is faster because the discount call and the shipping call wait at the same time, so the time is the slowest one, not the sum.
- Total time for validate and process, all concurrent: 13.1 seconds. In Phase 1 the async run took 54.8 seconds, because every order waited for the one before it. In Phase 2 all orders wait at the same time, so the total is close to the time of the slowest order.
- Race demo: with the unsafe reservation both order #10 and order #11 got the last keyboard (the bug). With the safe reservation one got it and one did not, and the stock was 0.
- Order #7 faulted with "Shipping provider unavailable". The other orders were not stopped. The order was marked Failed and its stock was released.
- orders.json had exactly 20 records: 13 Completed, 6 Rejected, 1 Failed. No write was lost.
- Report: 20 orders, 13 Completed, 6 Rejected, 1 Failed, revenue 5966.45, average 458.96, top product USB-C Hub (5 units).

Why Task.Run fits the invoice but not the database calls: the invoice is CPU work (a loop of 20,000,000 steps), so a thread is really busy and Task.Run moves that work off the calling thread. A database call is waiting, not working. The async method already frees the thread while it waits, so Task.Run would only waste a pool thread to wait.

### Phase 2 questions

1. What is the difference between starting a task and awaiting it? Starting a task begins the work and gives you a Task object right away, the code goes on. Awaiting waits for the task to finish and gives you its result or its exception. If you start many tasks first and await later, they all run at the same time.
2. Does every Task create a new thread? No. A Task is only a promise of a result. A Task from an async method that is waiting on Task.Delay or a file read uses no thread while it waits. Only Task.Run puts work on a pool thread.
3. Does Task.Delay() use a thread while waiting? No. It uses a timer. When the time is over, the rest of the method continues on a free pool thread. Thread.Sleep blocks the thread for the whole time.
4. What happens if you call .Result or .Wait() on an unfinished task? The calling thread is blocked until the task ends. It is dangerous because it wastes a thread, and in an app with a UI or a special synchronization context it can cause a deadlock (the task needs that thread to finish, and the thread waits for the task). Exceptions also come wrapped in AggregateException. I used it only in BlockingDemo.
5. What happens to an exception thrown inside a task that is never awaited? It is stored in the task and nobody sees it. The program continues as if nothing happened. Later, when the task is garbage collected, UnobservedTaskException fires. The work is lost without any message.
6. When should you use Task.Run(), and when should you avoid it? Use it for CPU-bound work, like the invoice rendering, so the calling thread is not blocked. Avoid it for I/O work (database, files, APIs) because async methods already do not block, and Task.Run would only waste a thread.
7. What is the difference between Task and Task<T>? Task means work that finishes with no value. Task<T> means work that finishes with a value of type T, which you get with await (or .Result).
8. Why can a lock block not contain an await? A lock belongs to one thread. After an await, the code may continue on a different thread, and that thread cannot release the lock. It could also block other callers for a long time. The compiler does not allow it. I used SemaphoreSlim with WaitAsync instead, because it does not belong to a thread.
9. Why did the unprotected stock reservation oversell, and how did you prevent it? Both orders read the stock (1) at the same time, then both waited, then both wrote 0 and both thought they got the keyboard. The check and the write were separate steps. I fixed it by doing the check and the update inside one locked update (one SemaphoreSlim around read, check, modify and write), so the second order sees stock 0 and is rejected.

## Phase 3 - Full Checkout Workflow

How to run: `dotnet run -- phase3`. Extra commands: `compare` (sync vs async), `fireforget` (the broken and fixed email code), `fireforget-exit` (console exits while the email is still running).

The workflow of one order has six steps: validate, reserve stock, take payment, save the order, write the invoice, send the email. If a step fails after an earlier step worked, the earlier work is undone (stock is released, the payment is refunded).

Result of my run (20 orders, all started together):

- Completed: 11
- Rejected: 6
- PaymentFailed: 2 (#5 gateway timeout after 3 seconds, #8 card declined)
- Failed: 1 (#13, the database save failed after payment, so the payment was refunded)
- Total time: about 14 seconds
- Email for #3 needed 3 attempts. Email for #17 failed 4 times, so it was written to email_retry.json and the order stayed Completed.
- The "Files check" at the end of the run prints OK for every line.

### 9.6 Sync vs async (orders #1 to #10, failures turned off)

- Sync (10 threads): 11.6 seconds, 10 threads used, ThreadPool.ThreadCount 8 -> 4, 8 Completed, 2 Rejected.
- Async (10 tasks): 10.1 seconds, 5 threads used, ThreadPool.ThreadCount 4 -> 6, 8 Completed, 2 Rejected.

Answers:

1. Does async make one order faster? No. One order still waits for the same delays (payment, email, saving). Async only stops the thread from being blocked while it waits.
2. Why does async scale better? A waiting async method gives its thread back. 10 orders used only 5 threads. The sync version needed one thread per order and each thread just sat and waited.
3. What happens to sync with 1,000 orders? It would need 1,000 threads. Each thread uses memory (about 1 MB of stack) and the system wastes time switching between them. The thread pool grows slowly, so many orders would wait to start. The async version would still use a handful of threads.
4. Why is async essential for databases, files and APIs? These calls spend almost all their time waiting, not working. Async lets the same few threads serve many requests while they wait. A web server that blocks threads runs out of threads under load.

### 9.7 Fire-and-forget investigation

The broken method calls `SendConfirmationAsync` without await.

1. The problem: the method says "Order processing finished" before the email is sent, and nobody watches the email task.
2. If the method returns early: it returned after 929 ms and emails.json had 0 records. 1.5 seconds later it had 1 record. If the console app exits first, the email is lost completely.
3. If the email throws: nothing is printed and the caller gets no exception. The exception stays inside the task. Only when the task was garbage collected, UnobservedTaskException fired with "SMTP server unavailable (attempt 1)".
4. The fix: `await` the email. After the fix the method returned after 1964 ms and emails.json had 1 record. When the email fails, the retry logic runs and the failure is handled inside the workflow, not lost.

When await is necessary: when you need the result, when you need to know it finished, when you need its exception, or when later code depends on it.

When fire-and-forget is acceptable: almost never. It is only acceptable for work that is allowed to be lost (for example a small log line), and then only if the task is wrapped in a try/catch inside so the exception is always handled, and the app is not exiting. A better way is to put the work in a queue (like email_retry.json).

Why async void is dangerous: the caller cannot await it, cannot catch its exception (it crashes the process), and cannot know when it ends.

Why it is acceptable for event handlers: an event handler must return void to match the event signature, and nobody needs to await a button click. The handler should catch its own exceptions.

### Phase 3 answers to the checkout questions

- Why are stock and payment compensated? A later failure must not leave stock reserved or money taken for an order that did not finish.
- Why must a failed email not fail the order? The customer already paid and the order is saved. The email can be sent again later, so it goes to email_retry.json.
- Why does the payment timeout use a linked cancellation token? It stops only the payment call after 3 seconds, and still reacts to the user pressing Ctrl+C.

## Phase 4 - Scale-Up

How to run: `dotnet run -- phase4`. A cancelled run: `dotnet run -- phase4 --cancel-after 4000`.

### 10.1 Live checkout progress

All 20 orders start together. `Task.WhenEach` gives each task as soon as it finishes, so the screen shows "[ 7/20] Order #11 -> Completed" in the order of finishing, not the order of starting. Final counts: Completed 11, Rejected 6, PaymentFailed 2, Failed 1. Time 14.3 seconds.

### 10.2 Customer dashboard

Four loads (profile, orders, payments, emails) start together and one `Task.WhenAll` waits for them. Customer #3 shows: 2 orders, 2 payments, 1 email. Time 0.6 seconds, the sum of the four loads would be about 3 seconds. Customer #99 (0.8 seconds) gives two errors together: "Customer #99 not found" and "Payments store timeout".

### 10.3 Fastest shipping quote

Three carriers race. The first good quote wins, the other two are cancelled, and the whole race has a 1200 ms budget. Results: #1 CairoExpress 542 ms, #2 NileCourier 1055 ms, #3 CairoExpress 1057 ms, #4 NileCourier 1163 ms, #5 nobody answered in time so the flat rate 25.00 was used. For #6 every carrier failed, so ShippingProviderUnavailableException("All carriers failed") was thrown. When the caller cancels after 300 ms, all three carriers stop (311 ms).

### 10.4 A Fraud scoring (CPU-bound)

10,000 orders scored. Sequential 4.83 seconds, `Parallel.ForEach` 0.29 seconds, speed-up 16.89x on 20 cores (ProcessorCount = 20), 20 distinct threads. Both ways flagged the same 1005 orders. The first five flagged ids are 4, 8, 12, 29, 33.

### 10.4 B Email re-send (I/O-bound)

30 emails sent with `Parallel.ForEachAsync` and `MaxDegreeOfParallelism = 4`. The highest number of sends at the same time was exactly 4. Total time 8.3 seconds (30 / 4 x 1 second). All 30 emails are in emails.json exactly once.

### Phase 4 questions

1. Which tool and why not one by one?
   - 10.1: `Task.WhenEach`. Awaiting one by one shows results in start order, so a fast order waits behind a slow one.
   - 10.2: `Task.WhenAll`. Awaiting one by one stops at the first failure, so the other errors are never seen.
   - 10.3: `Task.WhenEach` with a linked cancellation token. We must stop at the first success and cancel the rest, one by one would wait for the slowest carrier.
   - 10.4 A: `Parallel.ForEach`. One by one uses one core only.
   - 10.4 B: `Parallel.ForEachAsync` with MaxDegreeOfParallelism 4. One by one would take 30 seconds, and starting all 30 would break the limit of 4.
2. In 10.2, how do I get every exception? Keep the task from `Task.WhenAll` and read `all.Exception`, which is an AggregateException with all the errors. A plain `await` only throws the first exception and hides the others.
3. In 10.3, what happens to the losing carriers if never cancelled? They keep running until they finish. They use time, network and threads for a quote nobody needs, and their results or errors may be unobserved.
4. Why does 10.4 A get faster with more cores and 10.4 B not? 10.4 A uses the CPU all the time, so more cores means more work at the same moment. 10.4 B spends its time waiting, not computing, so the limit is the number we set (4) and the waiting time, not the cores.
5. Parallel work (10.4 A) is many threads running at the same time on different cores. Concurrent async work (10.1) is many operations in progress at the same time, often on very few threads, because they are waiting. Parallel is for CPU work, async is for waiting work.
6. A cancelled run (`--cancel-after 4000`): 4 orders had already finished (6.2 seconds in total) (all Rejected, they never reached payment) and 16 were cancelled. The 16 cancelled orders were saved as Pending with FailureReason "Cancelled". payments.json had 0 records because no payment was taken. Stock was the same as the seed data (P1=10, P2=15, P3=2, P4=1, P5=30, P6=12, P7=6, P8=20) because every reserved stock was released again. The JSON files stayed valid.

## Problems I found and how I solved them

- Writing orders with SaveAsync replaced the whole file with one order. I fixed it with an upsert (read all, change one, write all) under a lock.
- Two orders reserving the last item at the same time (race condition). I fixed it by checking and updating stock inside one locked update.
- A crash while writing could break a JSON file. I write to a temp file first and then move it over the real file.
- Colors mixed in the console when many threads logged. I made Log use one lock around color, write and reset.
- Fire-and-forget email lost errors. I fixed it with await.
- `.Result` inside async code can block threads. I removed it, and only BlockingDemo uses it on purpose.

## Storage design

Only JsonFileStore touches the JSON files. It reads and writes lists, uses one SemaphoreSlim so read-modify-write is never mixed between threads, writes through a temp file, treats a missing file as an empty list, and throws DataFileCorruptedException for broken JSON. The files are in the `Data` folder next to the program (bin/Debug/net10.0/Data). Final copies of every phase are in `sample-output`.
