# ANSWERS.md

## R1 / R2: Failures observed

Running the project several times shows:

**View counting (before fix).** The `Dictionary` is not thread-safe. `RecordView` performs a non-atomic check-then-act sequence: check `ContainsKey`, add `0`, then read, increment, and write back. Concurrent threads can both see the key as missing and both call `Add`, causing an `ArgumentException`. Even when the key already exists, lost updates can make the final count lower than the number of calls.

**Email jobs (before fix).** The `Queue` is not thread-safe. A worker checks `Count > 0` and then calls `Dequeue` separately. Between those steps, another worker can drain the queue, causing `Dequeue` to throw an `InvalidOperationException`. Since `ProducersDone` is never set, workers can spin forever.

## The fix

- Replace `Dictionary` with `ConcurrentDictionary`. `RecordView` uses a single `AddOrUpdate` call to update a count without a duplicate-key exception or a lost update from a competing update.
- Replace `Queue` with `ConcurrentQueue`. `PlaceOrder` calls `Enqueue`, and the worker calls `TryDequeue` once instead of checking `Count` before dequeuing.
- Keep `SentEmails` as a `List` protected by a lock, because `List<T>` is not thread-safe for concurrent writes.

## Problems with the submitted fix

**1. The worker holds the lock across `Thread.Sleep(1)`.** The outer lock wraps the whole loop body, so every other thread that needs the same lock is blocked during the sleep. This creates unnecessary contention.

**2. `RecordView` and `PlaceOrder` take the lock unnecessarily.** Both collections are already thread-safe for these individual operations. The lock adds contention. `RecordView` should use one `AddOrUpdate` call rather than a locked check-then-add-then-update sequence.

**3. `Count > 0` followed by `TryDequeue` is still check-then-act.** It is safe from the empty-queue exception because `TryDequeue` handles the race, but the preliminary count check is redundant. Call `TryDequeue` directly.

**4. `ProducersDone` is never set to `true`.** As a result, workers may never exit. This bug was present in the original code and remains unfixed.

## Corrected fix (description)

`RecordView` should use one `AddOrUpdate` call on the `ConcurrentDictionary`. `PlaceOrder` should use one `Enqueue` call on the `ConcurrentQueue`. `EmailWorker` should call `TryDequeue` once per iteration. When a job is successfully dequeued, it should take the lock only long enough to add the item to `SentEmails`, then continue. If no item is available, it should check whether `ProducersDone` is true and the queue is empty before returning; otherwise, it can briefly sleep before trying again.

No lock should be held during a sleep. Avoid check-then-act logic where a single thread-safe operation is available. The lock should protect only `SentEmails.Add`, the operation on the remaining non-thread-safe collection that is shared between threads.

## R3: Why safer collections don't make the business rule safe

Thread-safe collections make **individual operations** safe to execute concurrently. They do not make **sequences of operations** atomic, and they do not automatically protect **invariants spanning multiple collections**.

For example, suppose the business rule is “increment the view count and append the product to a recently viewed list.” These are two operations on two collections. A thread can finish the count update and be interrupted before adding the product to the list, leaving the count and list inconsistent. A `ConcurrentDictionary` cannot make both operations happen as one transaction; a lock shared by all relevant code can protect the compound operation.

The same concern applies to email jobs: dequeuing a job and recording it as sent are separate operations. If a worker fails between them, the job may have been removed from the queue without being recorded as sent. Each collection operation can be thread-safe while the larger business rule remains unguaranteed.

Using thread-safe collections improves safety for individual operations, but developers still need to reason about concurrency at the level of the business rule. In the submitted fix, wrapping already thread-safe operations in a shared lock adds contention without protecting any additional compound invariant unless that lock covers the complete multi-operation rule.
