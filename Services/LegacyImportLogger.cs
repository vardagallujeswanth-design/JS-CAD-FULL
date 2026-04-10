using System.Xml;
using CadProcessorService.Infrastructure;
using Microsoft.Extensions.Logging;

namespace CadProcessorService.Services;

public class LegacyImportLogger
{
    private readonly DatabaseExecutor _db;
    private readonly ILogger<LegacyImportLogger> _logger;

    public LegacyImportLogger(DatabaseExecutor db, ILogger<LegacyImportLogger> logger)
    {
        _db = db;
        _logger = logger;
    }

    private (string AckId, string TimeFirstKeystroke, string XmlData) ExtractMeta(XmlDocument doc)
    {
        try
        {
            var ackId =
                doc.SelectSingleNode("//IncidentData/AckID")
                ?? doc.SelectSingleNode("//Call/AckID");

            var time =
                doc.SelectSingleNode("//IncidentData/TimeFirstKeystroke")
                ?? doc.SelectSingleNode("//Call/TimeFirstKeystroke");

            return (
                ackId?.InnerText?.Trim() ?? "0",
                time?.InnerText?.Trim() ?? string.Empty,
                doc.InnerXml
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed extracting XML meta");
            return ("0", string.Empty, doc.InnerXml);
        }
    }

    public int LogCadData(
        string fileName,
        XmlDocument? doc,
        int callId,
        string responseMessage,
        int createUid,
        int ProviderId)
    {
        try
        {
            string eventNumber = "0";
            string eventDate = string.Empty;
            string eventData = fileName;

            if (doc != null)
            {
                var meta = ExtractMeta(doc);
                eventNumber = meta.AckId;
                eventDate = meta.TimeFirstKeystroke;
                eventData = meta.XmlData;
            }

            var parameters = new Dictionary<string, object?>
            {
                ["@EventNumber"] = eventNumber,
                ["@EventData"] = eventData,
                ["@EventReceivedDate"] = eventDate,
                ["@CallID"] = callId,
                ["@IncidentID"] = 0,
                ["@Response"] = responseMessage,
                ["@CreateUID"] = createUid,
                ["@ProviderId"] = ProviderId
            };

            return _db.ExecuteNonQuery(
                "CAD_CadDataInsert",
                parameters,
                "@Return");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CAD_CadDataInsert failed for {File}", fileName);
            return 0;
        }
    }

    public void LogServiceImport(
        string fileName,
        string radioName,
        int callId,
        int cadDataId,
        string statusCode,
        string errorCode,
        string errorDesc,
        Guid guid,
        int createUid,
        int providerId)
    {
        try
        {
            if (cadDataId <= 0)
                return;

            var parameters = new Dictionary<string, object?>
            {
                ["@CallID"] = callId,
                ["@CADDataID"] = cadDataId,
                ["@StatusCode"] = statusCode,
                ["@FileName"] = fileName,
                ["@RadioName"] = radioName,
                ["@CreateUID"] = createUid,
                ["@ErrorCode"] = int.TryParse(errorCode, out var ec) ? ec : 0,
                ["@ErrorDesc"] = errorDesc,
                ["@GUID"] = guid,
                ["@ProviderId"] = providerId

            };

            _db.ExecuteNonQuery("CAD_ServiceImportLogInsert", parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CAD_ServiceImportLogInsert failed for {File}", fileName);
        }
    }
}
