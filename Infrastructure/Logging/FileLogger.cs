using Microsoft.Extensions.Logging;
using System.Text;

namespace CadProcessorService.Infrastructure.Logging;

public sealed class FileLogger : ILogger
{
    private readonly string _category;
    private readonly LogFolderProvider _folderProvider;
    private static readonly object _lock = new();

    public FileLogger(string category, LogFolderProvider folderProvider)
    {
        _category = category;
        _folderProvider = folderProvider ?? throw new ArgumentNullException(nameof(folderProvider));

        // Ensure folder exists
        Directory.CreateDirectory(_folderProvider.GetFolder());
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel)
        => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var sb = new StringBuilder();
        sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        sb.Append(" | ");
        sb.Append(logLevel.ToString().ToUpper());
        sb.Append(" | ");
        sb.Append(_category);
        sb.Append(" | ");
        sb.Append(formatter(state, exception));

        if (exception != null)
        {
            sb.AppendLine();
            sb.Append(exception);
        }

        sb.AppendLine();

        lock (_lock)
        {
            try
            {
                string logFilePath = Path.Combine(
                    _folderProvider.GetFolder(),
                    $"cad-processor-{DateTime.UtcNow:yyyy-MM-dd}.log");

                File.AppendAllText(logFilePath, sb.ToString());
            }
            catch
            {
                // swallow logging errors to avoid breaking service
            }
        }
    }
}
