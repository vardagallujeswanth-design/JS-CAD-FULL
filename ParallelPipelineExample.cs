using System.Threading.Channels;
using CadProcessorService.Models;
using Microsoft.Extensions.Logging;

namespace CadProcessorService.Services;

public class ParallelPipelineExample
{
    private readonly ILogger<ParallelPipelineExample> _logger;
    private Channel<FileWorkItem>? _channel;
    private CancellationTokenSource? _cts;
    private int _maxQueueSize ;
    private int _workerCount ;

    public ParallelPipelineExample(ILogger<ParallelPipelineExample> logger)
    {
        _logger = logger;
    }

    // PUBLIC API (USED BY WORKER)
    public void Process(
    IEnumerable<string> files,
    Action<string> processor,
    int maxQueueSize ,  
    int workerCount )     
    {
        _maxQueueSize = maxQueueSize;
        _workerCount = workerCount;
        EnsureStarted();
        foreach (var file in files)
        {
            _ = EnqueueInternalAsync(file, processor);
        }
    }

    public void Stop()
    {
        if (_cts == null) return;
        _logger.LogInformation("Stopping PARALLEL XML pipeline");
        _cts.Cancel();
        _channel = null;
        _cts = null;
    }

    // INTERNAL PIPELINE
    private void EnsureStarted()
    {
        if (_channel != null) return;

        _logger.LogInformation("Starting PARALLEL XML pipeline with {WorkerCount} workers", _workerCount);
        _cts = new CancellationTokenSource();
        _channel = Channel.CreateBounded<FileWorkItem>(
            new BoundedChannelOptions(_maxQueueSize)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = false
            });

        for (int i = 0; i < _workerCount; i++)
        {
            int workerId = i + 1;
            Task.Run(() => WorkerLoop(workerId, _cts.Token));
        }
    }

    private async Task EnqueueInternalAsync(string filePath, Action<string> processor)
    {
        if (_channel == null) return;
        await _channel.Writer.WriteAsync(new FileWorkItem(filePath, processor));
        _logger.LogInformation("Enqueued file for parallel processing: {File}", Path.GetFileName(filePath));
    }

    private async Task WorkerLoop(int workerId, CancellationToken token)
    {
        if (_channel == null) return;
        _logger.LogInformation("Parallel worker {WorkerId} started", workerId);

        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(token))
            {
                try
                {
                    _logger.LogInformation("Worker {WorkerId} processing: {File}", workerId, Path.GetFileName(item.FilePath));
                    item.Process();
                    _logger.LogInformation("Worker {WorkerId} completed: {File}", workerId, Path.GetFileName(item.FilePath));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker {WorkerId} failed for {File}", workerId, item.FilePath);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Parallel worker {WorkerId} stopped", workerId);
        }
    }

    // WORK ITEM
    private sealed class FileWorkItem
    {
        public string FilePath { get; }
        private readonly Action<string> _processor;

        public FileWorkItem(string filePath, Action<string> processor)
        {
            FilePath = filePath;
            _processor = processor;
        }

        public void Process()
        {
            _processor(FilePath);  // calls Worker.ProcessSingleFile
        }
    }
}