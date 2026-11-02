# 03 — Locking Research

## Q1. What is `System.Threading.Monitor` used for?

`System.Threading.Monitor` is a .NET synchronization class used to protect a critical section so that only one thread at a time can execute that section while holding the same lock object. Its methods include `Enter` and `Exit` to acquire and release a lock, `TryEnter` to attempt to acquire one, and `Wait`, `Pulse`, and `PulseAll` for coordinating threads that need to wait for a condition or notify other waiting threads.

## Q2. What does the C# `lock` statement compile to?

For a normal reference-type lock object, the `lock` statement is implemented using `Monitor` operations. Conceptually, the compiler acquires the monitor, executes the body inside a `try` block, and releases the monitor in a `finally` block. This ensures the lock is released even if the body throws an exception.

> Note: Newer C# versions have special handling for `System.Threading.Lock` (C# 13 and later). The traditional `Monitor.Enter` / `Monitor.Exit` explanation applies to the usual `lock` statement with an ordinary object.

## Q3. List at least two differences between `lock` and calling `Monitor` APIs directly.

1. **Safety and simplicity:** `lock` automatically releases the lock when its body exits, including when an exception occurs. With `Monitor.Enter` / `Monitor.Exit`, the developer must use `try` / `finally` correctly to avoid leaving the lock held.
2. **Available control:** `lock` is a straightforward way to protect a block. Direct `Monitor` APIs offer extra operations such as `TryEnter` (attempt acquisition with a timeout) and `Wait` / `Pulse` / `PulseAll` (coordinate threads based on state).
3. **Structure:** `lock` expresses a single lexical critical section. Direct `Monitor` calls allow lock acquisition and release to be managed explicitly, but this flexibility makes mistakes easier.

## Q4. When might a developer use `Monitor.TryEnter`, `Monitor.Pulse`, or `Monitor.Wait` instead of a plain `lock`?

- **`Monitor.TryEnter`:** When the program should try to acquire a lock without waiting forever, such as attempting acquisition with a timeout and taking an alternative action if it fails.
- **`Monitor.Wait`:** When a thread holds a monitor but cannot continue until a condition becomes true. `Wait` releases that monitor while the thread waits, allowing another thread to enter the protected section and change the state. The waiting thread must reacquire the monitor before `Wait` returns.
- **`Monitor.Pulse`:** When a thread changes shared state and wants to notify one thread waiting on that monitor that it may be able to proceed. Use `PulseAll` when all waiting threads should be notified. A notification does not itself transfer ownership of the lock; the awakened thread must reacquire it.

`Wait`, `Pulse`, and `PulseAll` must be called while owning the relevant monitor. Code using `Wait` should check its condition in a loop, because waking up does not guarantee the condition is now true.

## Q5. Write the same critical section once with `lock` and once with `Monitor.Enter` / `Monitor.Exit`.

Both versions below protect the same shared counter. The lock object is private so unrelated code cannot lock it accidentally.

### Using `lock`

```csharp
private readonly object _sync = new object();
private int _counter;

public void Increment()
{
    lock (_sync)
    {
        _counter++;
    }
}
```

### Using `Monitor.Enter` / `Monitor.Exit`

```csharp
private readonly object _sync = new object();
private int _counter;

public void Increment()
{
    bool lockTaken = false;

    try
    {
        Monitor.Enter(_sync, ref lockTaken);
        _counter++;
    }
    finally
    {
        if (lockTaken)
        {
            Monitor.Exit(_sync);
        }
    }
}
```

The second version makes the acquire/release steps explicit. The `lockTaken` overload helps ensure `Exit` is called only if the lock was successfully acquired. For ordinary critical sections, `lock` is usually clearer and less error-prone.

## Sources

1. Microsoft Learn — [Monitor Class](https://learn.microsoft.com/en-us/dotnet/api/system.threading.monitor)
2. ZetCOde - https://www.zetcode.com/csharp/monitor/?utm_source=chatgpt.com
