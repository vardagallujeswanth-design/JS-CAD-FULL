using CadProcessorService.Infrastructure;
using CadProcessorService.Infrastructure.Logging;
using CadProcessorService.Services;
using CadProcessorService.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CadProcessorService;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration["urls"] = "http://0.0.0.0:5000";

#if !DEBUG
        // Required if running as Windows Service
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "CadProcessorService";
        });
#else
        {
            builder.Services.AddWindowsService(options =>
            {
            options.ServiceName = "CadProcessorService";
            });
        }
#endif


        //builder.Services.AddWindowsService(options =>
        //{
        //    options.ServiceName = "CadProcessorService";
        //});

        // Load configuration options

        builder.Services.Configure<CadProcessingOptions>(
            builder.Configuration.GetSection("CADProcessing"));

        var loggingOptions = builder.Configuration
            .GetSection("LoggingOptions")
            .Get<LoggingOptions>();

        if (loggingOptions == null)
            throw new Exception("LoggingOptions section missing in appsettings.json");

        builder.Services.AddSingleton(loggingOptions);

        
        // Log folder provider
        
        var logFolderProvider = new LogFolderProvider(loggingOptions.FallbackLogFolder);
        builder.Services.AddSingleton(logFolderProvider);

        
        // Application Services
        
        builder.Services.AddSingleton<DatabaseExecutor>();
        builder.Services.AddSingleton<EmailService>();
        builder.Services.AddSingleton<LegacyImportLogger>();
        builder.Services.AddSingleton<LegacyValidationService>();
        builder.Services.AddSingleton<XmlProcessingService>();
        builder.Services.AddSingleton<ParallelPipelineExample>();
        builder.Services.AddHostedService<Worker>();

        
        // Logging

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddProvider(new FileLoggerProvider(logFolderProvider));

        builder.Services.AddControllers();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Build & run

        var app = builder.Build();

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("=== CadProcessorService Starting ===");
        logger.LogInformation("Base Directory: {BaseDir}", AppContext.BaseDirectory);
        logger.LogInformation("Fallback log folder: {Folder}", logFolderProvider.GetFolder());

        app.UseRouting();
        app.UseCors();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapControllers();

        await app.RunAsync();
    }
}
