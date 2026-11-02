namespace OrderProcessing.Shared.Storage;

public class DataFileCorruptedException : Exception
{
    public string FileName { get; }

    public DataFileCorruptedException(string fileName, Exception? inner = null)
        : base($"Data file '{fileName}' contains invalid JSON.", inner)
    {
        FileName = fileName;
    }
}
