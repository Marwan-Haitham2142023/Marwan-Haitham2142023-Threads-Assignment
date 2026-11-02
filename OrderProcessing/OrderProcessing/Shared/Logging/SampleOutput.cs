
namespace OrderProcessing.Shared.Logging;

public static class SampleOutput
{
    public static void Save(string name)
    {
        var target = Path.Combine(FindProjectRoot(), "sample-output", name);

        if (Directory.Exists(target))
            Directory.Delete(target, recursive: true);

        CopyDirectory(DataFiles.DataDirectory, target);
        Log.Info($"SampleOutput -> Data files saved to {target}");
    }

    private static string FindProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
                return dir.FullName;

            dir = dir.Parent;
        }

        return AppContext.BaseDirectory;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);

        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }
}
