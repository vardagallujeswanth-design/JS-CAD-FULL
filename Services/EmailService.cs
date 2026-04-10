using CadProcessorService.Infrastructure;
using CadProcessorService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;


namespace CadProcessorService.Services;

public class EmailService
{
    private readonly DatabaseExecutor _db;
    private readonly ILogger<EmailService> _logger;
    private DbEmailConfig? _config;
    private readonly CadProcessingOptions _options;  

public EmailService(DatabaseExecutor db, ILogger<EmailService> logger, IOptions<CadProcessingOptions> options)  
{
    _db = db;
    _logger = logger;
    _options = options.Value; 
}


    private DbEmailConfig GetConfig()
    {
        if (_config == null)
        {
            int appId = _db.GetApplicationId(_options.ApplicationId);
            _config = _db.GetEmailSettings(appId);
        }
        return _config;
    }

    private DbEmailConfig GetConfigForProvider(int providerId)
    {
        return _db.GetProviderEmailSettings(providerId);
    }

    public void SendFailure(string file, string message)
    {  
        var cfg = GetConfig();
        if (!cfg.Enabled || !cfg.SendOnFailure) return;
        Send($"CAD File Failed: {file}", message);
    }

    public void SendFailure(int providerId, string file, string message)
    {
        var cfg = GetConfigForProvider(providerId);
        if (!cfg.Enabled || !cfg.SendOnFailure) return;
        Send($"CAD File Failed: {file}", message, cfg);
    }

    public void SendSuccess(string file, string message)
    {
        var cfg = GetConfig();
        if (!cfg.Enabled || !cfg.SendOnSuccess) return;
        Send($"CAD File Processed: {file}", message);
    }

    public void SendSuccess(int providerId, string file, string message)
    {
        var cfg = GetConfigForProvider(providerId);
        if (!cfg.Enabled || !cfg.SendOnSuccess) return;
        Send($"CAD File Processed: {file}", message, cfg);
    }

    private void Send(string subject, string body)
    {
        try
        {
            var cfg = GetConfig();
            SendWithConfig(subject, body, cfg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed");
        }
    }

    private void Send(string subject, string body, DbEmailConfig cfg)
    {
        try
        {
            SendWithConfig(subject, body, cfg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed");
        }
    }

    private void SendWithConfig(string subject, string body, DbEmailConfig cfg)
    {
        using var client = new SmtpClient(cfg.Host, cfg.Port)
        {
            EnableSsl = cfg.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(cfg.UserName)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(cfg.UserName, cfg.Password)
        };

        client.Send(new MailMessage(cfg.FromEmail, cfg.ToEmail, subject, body));
    }
}






