using Microsoft.Extensions.Logging;

namespace CadProcessorService.Infrastructure.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly LogFolderProvider _folderProvider;

    public FileLoggerProvider(LogFolderProvider folderProvider)
    {
        _folderProvider = folderProvider;
    }

    public ILogger CreateLogger(string categoryName)
        => new FileLogger(categoryName, _folderProvider);

    public void Dispose() { }
}
