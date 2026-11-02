using System.Text;
using System.Text.Json;

namespace OrderProcessing.Shared.Storage;

public class JsonFileStore
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly UTF8Encoding Utf8 = new(false);

    private static string PathOf(string fileName) => Path.Combine(DataFiles.DataDirectory, fileName);

    public async Task<List<T>> ReadAllAsync<T>(string fileName, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try { return await ReadFileAsync<T>(fileName, ct); }
        finally { Gate.Release(); }
    }

    public async Task WriteAllAsync<T>(string fileName, List<T> items, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try { await WriteFileAsync(fileName, items, ct); }
        finally { Gate.Release(); }
    }

    public async Task<TResult> UpdateAsync<T, TResult>(string fileName, Func<List<T>, TResult> change, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var items = await ReadFileAsync<T>(fileName, ct);
            var result = change(items);
            await WriteFileAsync(fileName, items, ct);
            return result;
        }
        finally { Gate.Release(); }
    }

    public List<T> ReadAll<T>(string fileName)
    {
        Gate.Wait();
        try { return ReadFile<T>(fileName); }
        finally { Gate.Release(); }
    }

    public void WriteAll<T>(string fileName, List<T> items)
    {
        Gate.Wait();
        try { WriteFile(fileName, items); }
        finally { Gate.Release(); }
    }

    public TResult Update<T, TResult>(string fileName, Func<List<T>, TResult> change)
    {
        Gate.Wait();
        try
        {
            var items = ReadFile<T>(fileName);
            var result = change(items);
            WriteFile(fileName, items);
            return result;
        }
        finally { Gate.Release(); }
    }

    private static async Task<List<T>> ReadFileAsync<T>(string fileName, CancellationToken ct)
    {
        var path = PathOf(fileName);
        if (!File.Exists(path)) return new List<T>();

        var json = await File.ReadAllTextAsync(path, Utf8, ct);
        return FromJson<T>(fileName, json);
    }

    private static List<T> ReadFile<T>(string fileName)
    {
        var path = PathOf(fileName);
        if (!File.Exists(path)) return new List<T>();

        var json = File.ReadAllText(path, Utf8);
        return FromJson<T>(fileName, json);
    }

    private static async Task WriteFileAsync<T>(string fileName, List<T> items, CancellationToken ct)
    {
        var path = PathOf(fileName);
        var tempPath = path + ".tmp";
        Directory.CreateDirectory(DataFiles.DataDirectory);

        await File.WriteAllTextAsync(tempPath, ToJson(items), Utf8, ct);
        File.Move(tempPath, path, overwrite: true);
    }

    private static void WriteFile<T>(string fileName, List<T> items)
    {
        var path = PathOf(fileName);
        var tempPath = path + ".tmp";
        Directory.CreateDirectory(DataFiles.DataDirectory);

        File.WriteAllText(tempPath, ToJson(items), Utf8);
        File.Move(tempPath, path, overwrite: true);
    }

    private static string ToJson<T>(List<T> items) =>
        JsonSerializer.Serialize(items, JsonConfig.Options);

    private static List<T> FromJson<T>(string fileName, string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonConfig.Options) ?? new List<T>();
        }
        catch (JsonException ex)
        {
            throw new DataFileCorruptedException(fileName, ex);
        }
    }
}
