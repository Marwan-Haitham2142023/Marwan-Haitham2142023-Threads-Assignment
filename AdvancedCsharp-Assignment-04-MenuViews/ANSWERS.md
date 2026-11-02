# ANSWERS.md

## R1 / R2: Expected vs. Actual

**Expected:** `400,000` (8 threads × 50,000 views per thread).

**Actual:** The reported value is less than `400,000` in the observed runs. The exact number can vary between runs because thread scheduling and timing affect how many updates are lost.

## R3: Why the numbers disagree

The root cause is that `Views++` is **not an atomic operation**. Although it looks like a single statement, it involves reading the current value, incrementing it, and writing the result back.

When multiple threads execute these steps concurrently, their operations can interleave and cause updates to be lost. For example, two threads might both read `10`, both calculate `11`, and both write `11`. Two increments were attempted, but the counter increased by only one. This is a classic race condition.

The more threads and iterations there are, the more opportunities exist for interleaving that loses an update.

## The fix: Use `Interlocked.Increment`

For a single integer counter shared across threads, `System.Threading.Interlocked.Increment` is an appropriate solution. It performs the increment atomically, preventing threads from overwriting one another's updates. The runtime and processor handle the necessary synchronization; the implementation is platform-dependent, so it is not accurate to claim it always compiles to one CPU instruction.

The fixed `RegisterView` method should call `Interlocked.Increment(ref Views)` instead of `Views++`. This eliminates the read-increment-write race for that counter.

A `lock` would also solve the problem, but it is more suitable when multiple operations or related shared variables must be protected together. For a simple counter increment, `Interlocked.Increment` is usually the simpler and more efficient choice.

After the fix, every run should print `Expected: 400000` and `Actual: 400000`, assuming all 400,000 increments complete successfully.

## R4: When would you protect this kind of update differently?

**When the critical section involves more than a single variable.** `Interlocked` provides specific atomic operations, rather than protecting an arbitrary block of code. If an update requires modifying multiple related fields together, those operations must be treated as one atomic unit. A `lock` is appropriate because it can protect the entire block.

**When consistency with surrounding shared state matters.** Even if a counter update is infrequent and contention is low, a `lock` may be preferable when the counter is part of a larger operation already protected by the same lock. The important choice is to use synchronization that correctly protects the full shared-state invariant.
