
using CadProcessorService.Helpers;
using CadProcessorService.Infrastructure;
using CadProcessorService.Infrastructure.Logging;
using CadProcessorService.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data.SqlClient;

namespace CadProcessorService.Services;

public class RetryItem
{
    public string FilePath { get; set; } = "";
    public DbProviderConfig Provider { get; set; } = default!;
    public DbFolderConfig Folder { get; set; } = default!;
    public int Attempts { get; set; }
    public DateTime NextAttemptUtc { get; set; }
}

public class Worker : BackgroundService
{
    private readonly DatabaseExecutor _db;
    private readonly XmlProcessingService _xmlService;
    private readonly ParallelPipelineExample _pipeline;
    private readonly CadProcessingOptions _options;
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly LogFolderProvider _logFolderProvider;
    private readonly int _daysToKeepLogs;

    private int _applicationId;
    private List<DbProviderConfig> _providers = new();
    private readonly Dictionary<int, DbFolderConfig> _folderCache = new();
    private DbRetryConfig _retryConfig = new();
    private readonly Dictionary<int, DbRetryConfig> _providerRetryConfigs = new();

    private readonly List<RetryItem> _retryQueue = new();
    private DateTime _lastCacheLoadUtc = DateTime.MinValue;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public Worker(
        DatabaseExecutor db,
        XmlProcessingService xmlService,
        ParallelPipelineExample pipeline,
        IOptions<CadProcessingOptions> options,
        IConfiguration configuration,
        ILogger<Worker> logger,
        LogFolderProvider logFolderProvider,
        LoggingOptions loggingOptions)
    {
        _db = db;
        _xmlService = xmlService;
        _pipeline = pipeline;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
        _logFolderProvider = logFolderProvider;
        _daysToKeepLogs = loggingOptions.DaysToKeepLogs;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CAD Processor Worker started");

        try
        {
            var applicationId = _options.ApplicationId;
            _applicationId = _db.GetApplicationId(applicationId);
            _xmlService.SetApplicationId(_applicationId);
            _logger.LogInformation("Application ID resolved: {ApplicationId}", _applicationId);

            InitializeLogFolder();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize worker. Service will continue with defaults.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ReloadCacheIfNeeded();
                ProcessRetryQueue();
                ProcessProviders();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in worker loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
        }

        _pipeline.Stop();
        _logger.LogInformation("CAD Processor Worker stopped");
    }

    private void InitializeLogFolder()
    {
        string fallbackFolder = _logFolderProvider.GetFolder();
        string logFolder = fallbackFolder;

        try
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("CadDatabase"));
            using var cmd = new SqlCommand(@"
                SELECT SettingValue
                FROM CAD_ApplicationSettings
                WHERE ApplicationId = @appId
                  AND SettingKey = 'LogFolder'
                  AND IsActive = 1
            ", conn);
            cmd.Parameters.AddWithValue("@appId", _applicationId);
            conn.Open();

            var value = cmd.ExecuteScalar();
            if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                logFolder = value.ToString()!;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read LogFolder from database. Using fallback folder from appsettings.json.");
        }

        try
        {
            Directory.CreateDirectory(logFolder);
        }
        catch
        {
            logFolder = fallbackFolder;
            Directory.CreateDirectory(logFolder);
            _logger.LogWarning("DB folder invalid or inaccessible. Falling back to folder from appsettings.json: {LogFolder}", logFolder);
        }

        _logFolderProvider.SetFolder(logFolder);
        CleanupOldLogs(logFolder);
        _logger.LogInformation("Logging initialized to folder: {LogFolder}", logFolder);
    }

    private void ReloadCacheIfNeeded()
    {
        if ((DateTime.UtcNow - _lastCacheLoadUtc) < CacheTtl)
            return;

        _providers = _db.GetProviders(_applicationId);
        _folderCache.Clear();

        foreach (var p in _providers)
        {
            var folderConfig = _db.GetProviderFolders(p.ProviderId);
            if (folderConfig != null)
            {
                _folderCache[p.ProviderId] = folderConfig;
            }
            else
            {
                _logger.LogWarning("Skipping provider {ProviderId} ({ProviderCode}) - no folder configuration found", p.ProviderId, p.ProviderCode);
            }

            // Load provider-specific retry settings
            var providerRetryConfig = _db.GetProviderRetrySettings(p.ProviderId);
            _providerRetryConfigs[p.ProviderId] = providerRetryConfig;
        }

        // Keep global config for backward compatibility
        _retryConfig = _db.GetRetrySettings(_applicationId);
        _xmlService.SetRetryConfig(_retryConfig);

        _lastCacheLoadUtc = DateTime.UtcNow;

        _logger.LogInformation(
            "Cache refreshed. Providers={Count}, Retry={Retry}",
            _providers.Count,
            _retryConfig.Enabled);
    }

    private DbRetryConfig GetRetryConfigForProvider(DbProviderConfig provider)
    {
        if (_providerRetryConfigs.TryGetValue(provider.ProviderId, out var config))
        {
            return config;
        }
        // Fall back to global if provider-specific not found
        return _retryConfig;
    }

    private string GetProcessingMode()
    {
        try
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("CadDatabase"));
            using var cmd = new SqlCommand(@"
                SELECT SettingValue
                FROM CAD_ApplicationSettings
                WHERE ApplicationId = @appId
                  AND SettingKey = 'EnableParallelPipeline'
                  AND IsActive = 1
            ", conn);
            cmd.Parameters.AddWithValue("@appId", _applicationId);
            conn.Open();
            var result = cmd.ExecuteScalar();
            string value = result?.ToString() ?? "false";
            return value.ToLower() == "true" ? "Parallel" : "Sequential";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read EnableParallelPipeline from DB. Defaulting to Sequential.");
            return "Sequential";
        }
    }

    private void ProcessProviders()
    {
        _logger.LogInformation("Processing providers. Count={Count}", _providers.Count);

        var mode = GetProcessingMode();
        _logger.LogInformation("Processing mode: {Mode}", mode);

        foreach (var provider in _providers)
        {
            if (!_folderCache.TryGetValue(provider.ProviderId, out var folder))
            {
                _logger.LogWarning("Missing folder configuration for provider {ProviderId}", provider.ProviderId);
                continue;
            }

            if (!ValidateFolderConfig(folder, out var missingFolders))
            {
                _logger.LogWarning("Skipping provider {ProviderId} because folder configuration is missing values for: {MissingFolders}", provider.ProviderId, string.Join(", ", missingFolders));
                continue;
            }

            EnsureFolders(folder);

            List<string> files;
            try
            {
                files = Directory
                    .EnumerateFiles(folder.SourceFolder, "*.xml", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => File.GetCreationTimeUtc(f))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed enumerating files for provider {Provider}", provider.ProviderCode);
                continue;
            }

            if (files.Count == 0)
                continue;

            _logger.LogInformation("Found {Count} files for provider {Provider}", files.Count, provider.ProviderCode);

            if (mode == "Parallel")
            {
                var (maxQueueSize, workerCount) = GetParallelSettings();
                _pipeline.Process(files,file => ProcessSingleFile(provider, folder, file), maxQueueSize,workerCount);
            }
            else
            {
                _logger.LogInformation("Using SEQUENTIAL processing for provider {Provider}", provider.ProviderCode);
                foreach (var file in files)
                    ProcessSingleFile(provider, folder, file);
            }
        }
    }

    private static bool ValidateFolderConfig(DbFolderConfig folder, out IReadOnlyCollection<string> missingFolders)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(folder.SourceFolder)) missing.Add(nameof(folder.SourceFolder));
        if (string.IsNullOrWhiteSpace(folder.DoneFolder)) missing.Add(nameof(folder.DoneFolder));
        if (string.IsNullOrWhiteSpace(folder.ErrorFolder)) missing.Add(nameof(folder.ErrorFolder));
        if (string.IsNullOrWhiteSpace(folder.RetryFolder)) missing.Add(nameof(folder.RetryFolder));
        if (string.IsNullOrWhiteSpace(folder.OtherAgencyFolder)) missing.Add(nameof(folder.OtherAgencyFolder));

        missingFolders = missing;
        return missing.Count == 0;
    }

    private static void EnsureFolders(DbFolderConfig folder)
    {
        FileHelper.EnsureDirectory(folder.SourceFolder);
        FileHelper.EnsureDirectory(folder.DoneFolder);
        FileHelper.EnsureDirectory(folder.ErrorFolder);
        FileHelper.EnsureDirectory(folder.RetryFolder);
        FileHelper.EnsureDirectory(folder.OtherAgencyFolder);
    }

    private void ProcessSingleFile(DbProviderConfig provider, DbFolderConfig folder, string sourceFile)
    {
        _logger.LogInformation("Processing file {File}", sourceFile);

        string tempFile;
        try
        {
            tempFile = StageToTemp(folder, sourceFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed staging file {File}", sourceFile);
            MoveToError(folder, sourceFile);
            return;
        }

        FileProcessResult result;

        try
        {
            result = _xmlService.ProcessFile(provider, folder, tempFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal processing error {File}", tempFile);
            var providerRetryConfig = GetRetryConfigForProvider(provider);
            result = new FileProcessResult { Success = false, ShouldRetry = providerRetryConfig.Enabled };
        }

        try
        {
            HandleResult(provider, folder, tempFile, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed handling result for {File}", tempFile);
            MoveToError(folder, tempFile);
        }
    }

    private void HandleResult(DbProviderConfig provider, DbFolderConfig folder, string filePath, FileProcessResult result)
    {
        var providerRetryConfig = GetRetryConfigForProvider(provider);
        if (!result.Success && result.ShouldRetry && providerRetryConfig.Enabled)
        {
            EnqueueRetry(provider, folder, filePath);
            return;
        }

        MoveFinal(folder, filePath, result);
    }

    private void EnqueueRetry(DbProviderConfig provider, DbFolderConfig folder, string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var retryDest = Path.Combine(folder.RetryFolder, fileName);

        FileHelper.MoveWithCreate(filePath, retryDest);

        var providerRetryConfig = GetRetryConfigForProvider(provider);
        _retryQueue.Add(new RetryItem
        {
            FilePath = retryDest,
            Provider = provider,
            Folder = folder,
            Attempts = 0,
            NextAttemptUtc = DateTime.UtcNow.AddSeconds(providerRetryConfig.DelaySeconds)
        });

        _logger.LogInformation("Enqueued retry {File}", fileName);
    }

    private void ProcessRetryQueue()
    {
        // Process retry queue if any items exist (check provider-specific settings per item)
        if (!_retryQueue.Any())
            return;

        var now = DateTime.UtcNow;
        var dueItems = _retryQueue.Where(r => r.NextAttemptUtc <= now).ToList();

        foreach (var item in dueItems)
        {
            var providerRetryConfig = GetRetryConfigForProvider(item.Provider);
            
            if (!providerRetryConfig.Enabled)
            {
                // Provider retry disabled, move to final
                MoveFinal(item.Folder, item.FilePath, new FileProcessResult { Success = false });
                _retryQueue.Remove(item);
                continue;
            }

            if (!File.Exists(item.FilePath))
            {
                _retryQueue.Remove(item);
                continue;
            }

            item.Attempts++;

            FileProcessResult result;
            try
            {
                result = _xmlService.ProcessFile(item.Provider, item.Folder, item.FilePath);
            }
            catch
            {
                result = new FileProcessResult { Success = false };
            }

            if (result.Success || item.Attempts >= providerRetryConfig.MaxAttempts)
            {
                MoveFinal(item.Folder, item.FilePath, result);
                _retryQueue.Remove(item);
            }
            else
            {
                item.NextAttemptUtc = DateTime.UtcNow.AddSeconds(providerRetryConfig.DelaySeconds);
                _logger.LogWarning("Retry {Attempt}/{Max} scheduled for {File}", item.Attempts, providerRetryConfig.MaxAttempts, item.FilePath);
            }
        }
    }

    private static string StageToTemp(DbFolderConfig folder, string sourceFile)
    {
        var tempRoot = Path.Combine(folder.SourceFolder, "_processing");
        FileHelper.EnsureDirectory(tempRoot);

        var tempName = $"{Path.GetFileNameWithoutExtension(sourceFile)}_{Guid.NewGuid():N}.xml";
        var tempPath = Path.Combine(tempRoot, tempName);

        try
        {
            File.Move(sourceFile, tempPath);
            return tempPath;
        }
        catch (IOException)
        {
            File.Copy(sourceFile, tempPath, overwrite: true);
            File.Delete(sourceFile);
            return tempPath;
        }
    }

    private static void MoveToError(DbFolderConfig folder, string file)
    {
        try
        {
            var fileName = Path.GetFileName(file);
            var target = Path.Combine(folder.ErrorFolder, fileName);

            if (File.Exists(target))
                target = Path.Combine(folder.ErrorFolder, $"{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid():N}.xml");

            File.Move(file, target);
        }
        catch { }
    }

    private static void MoveFinal(DbFolderConfig folder, string filePath, FileProcessResult result)
    {
        var fileName = Path.GetFileName(filePath);

        var root = result.IsOtherAgency ? folder.OtherAgencyFolder :
                   result.Success ? folder.DoneFolder :
                   folder.ErrorFolder;

        var agency = string.IsNullOrWhiteSpace(result.AgencyCode) ? "UNKNOWN" : result.AgencyCode;

        FileHelper.MoveWithCreate(filePath, Path.Combine(root, agency, fileName));
    }



    //--------------------------------------------------------------------------------------------------
    private (int MaxQueueSize, int WorkerCount) GetParallelSettings()
    {
        int maxQueueSize = 200; 
        int workerCount = 4;    

        try
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("CadDatabase"));
            using var cmd = new SqlCommand(@"
            SELECT SettingKey, SettingValue
            FROM CAD_ApplicationSettings
            WHERE ApplicationId = @appId
              AND SettingKey IN ('MaxQueueSize', 'WorkerCount')
              AND IsActive = 1
        ", conn);
            cmd.Parameters.AddWithValue("@appId", _applicationId);
            conn.Open();

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string key = rdr.GetString(0);
                string value = rdr.GetString(1);

                if (key == "MaxQueueSize" && int.TryParse(value, out int mqs))
                    maxQueueSize = mqs;

                if (key == "WorkerCount" && int.TryParse(value, out int wc))
                    workerCount = wc;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read parallel settings. Using defaults.");
        }

        return (maxQueueSize, workerCount);
    }
    //=-----------------------------------------------------------------------------------------------

    private void CleanupOldLogs(string baseFolder)
    {
        try
        {
            if (!Directory.Exists(baseFolder)) return;

            var cutoff = DateTime.UtcNow.AddDays(-_daysToKeepLogs);

            foreach (var dir in Directory.GetDirectories(baseFolder))
            {
                var folderName = Path.GetFileName(dir);
                if (DateTime.TryParse(folderName, out var folderDate) && folderDate < cutoff)
                {
                    Directory.Delete(dir, true);
                    _logger.LogInformation("Deleted old log folder: {Folder}", dir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old logs");
        }
    }
}