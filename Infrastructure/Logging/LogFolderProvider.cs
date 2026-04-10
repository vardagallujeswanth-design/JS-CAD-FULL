namespace CadProcessorService.Infrastructure.Logging;

public sealed class LogFolderProvider
{
    private string _logFolder;
    private readonly object _lock = new();

    public LogFolderProvider(string initialFolder)
    {
        _logFolder = initialFolder;
        Directory.CreateDirectory(_logFolder);
    }

    public void SetFolder(string newFolder)
    {
        if (string.IsNullOrWhiteSpace(newFolder)) return;

        lock (_lock)
        {
            Directory.CreateDirectory(newFolder);
            _logFolder = newFolder;
        }
    }

    public string GetFolder()
    {
        lock (_lock)
        {
            return _logFolder;
        }
    }
}
