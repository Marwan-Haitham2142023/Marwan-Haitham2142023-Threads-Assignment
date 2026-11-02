# TableBooking – ANSWERS.md

## What is wrong and why

### 1. Race condition on the check-then-act sequence

The guard that checks whether tables are still available and the decrement that actually books a table are two separate operations. Because forty threads run at the same time with no synchronization, many of them can read the counter while it is still positive, all pass the guard, and all decrement it. The result is overselling: the counter goes negative and the booked count exceeds five.

The artificial sleep between the check and the decrement makes the bug almost guaranteed to fire, because it parks each thread exactly in the window where every other thread can also pass the guard.

### 2. Lost updates on the counters

Decrementing the tables-left counter and incrementing the booked counter are read-modify-write operations on shared integers. Two threads can read the same value, modify it, and write it back, so one write overwrites the other. This corrupts both counters independently of the oversell.

### 3. One OS thread per customer

The demo creates forty raw threads. Threads are heavyweight — each has a stack, kernel resources, and scheduling cost — and here they sit blocked on sleep doing nothing useful. This pattern does not scale to hundreds or thousands of customers.

### 4. A plain lock would not be enough

Wrapping the body in a lock does not even compile if the body contains an await, and using a blocking sleep under a lock just serializes all work on one thread while still blocking it. The correct async-aware primitive is a semaphore.

### Symptom (before)

A typical run oversells the tables: the booked count drifts above five and the tables-left counter goes negative. The totals change on every run — sometimes five, sometimes seven, sometimes negative — so the result is never reliable.

## The fix

### What changed

The booking method is now asynchronous and guards its critical section with a semaphore that allows exactly one caller in at a time.

The guard, the delay, and the two counter updates all execute while the semaphore is held, so they behave as one atomic unit. The semaphore is acquired at the start and released in a finally block, which guarantees release even if the delay throws or the method returns early through the guard.

The blocking sleep is replaced by an asynchronous delay. The delay still happens inside the critical section, so atomicity is preserved, but the calling thread is freed during the wait instead of being blocked — that is the entire point of going async.

The demo driver no longer creates one thread per customer. It launches each booking as a task and awaits them all together, so concurrency is bounded by the semaphore rather than by the number of OS threads. This scales to thousands of simultaneous customers.

The counters are exposed as read-only properties, so external code cannot modify them; the invariants can only be violated inside the class, where the semaphore protects them.

### Why the semaphore is the right primitive

A normal lock is bound to the thread that acquired it and therefore cannot span an await, because the continuation may resume on a different thread and the exit would throw. A semaphore has no thread affinity, so it can be released from any thread, which makes it the correct primitive for async mutual exclusion.

Because the guard and the decrement are both inside the held semaphore, no other caller can observe a half-updated state. Either the counter is positive and we decrement it, or we return without touching anything. Overselling is impossible.

The finally block guarantees the semaphore is released even on exceptions or early returns. Without it, a single failure would permanently deadlock every future caller.

### Behavior after (every run)

Exactly five customers are booked and the tables-left counter ends at zero. The order in which the successful names are printed can vary between runs depending on who wins the semaphore first, but the final totals are always the same: booked equals five, tables left equals zero — never more, never less, never negative. The two extra customers are correctly rejected because the semaphore ensures they see the counter at zero when they enter.

### Verification

Running the program repeatedly always prints the same final totals: booked equals five and tables left equals zero. The race is gone.