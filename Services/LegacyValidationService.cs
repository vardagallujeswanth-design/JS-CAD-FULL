using System.Xml;
using CadProcessorService.Helpers;
using Microsoft.Extensions.Logging;

namespace CadProcessorService.Services;

public class LegacyValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorDescription { get; set; } = string.Empty;
    public string AgencyIdentifier { get; set; } = string.Empty;
    public string CallNumber { get; set; } = string.Empty;
}

public class LegacyValidationService
{
    private readonly ILogger<LegacyValidationService> _logger;

    public LegacyValidationService(ILogger<LegacyValidationService> logger)
    {
        _logger = logger;
    }

    public LegacyValidationResult ValidateFile(
        string filePath,
        XmlDocument? preloadedDoc = null,
        string? agencyIdentificationPath = null,
        string? callNumberPath = null)
    {
        var result = new LegacyValidationResult { IsValid = false };

        // 101 – FILE NOT XML
        if (!".xml".Equals(Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase))
        {
            result.ErrorCode = "101";
            result.ErrorDescription = "File Format is not XML";
            return result;
        }

        XmlDocument doc = preloadedDoc ?? new XmlDocument();
        bool fileBroken = false;

        try
        {
            if (preloadedDoc == null)
                doc.Load(filePath);
        }
        catch (XmlException)
        {
            fileBroken = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading XML {File}", filePath);
            fileBroken = true;
        }

        // 103 – XML BROKEN
        if (fileBroken)
        {
            result.ErrorCode = "103";
            result.ErrorDescription = "XML File Broken or could not open";
            return result;
        }

        // AGENCY IDENTIFICATION

        XmlNode? agencyNode = null;

        if (!string.IsNullOrWhiteSpace(agencyIdentificationPath))
        {
            agencyNode = SelectSingleNodeSmart(doc, agencyIdentificationPath);
        }
        // if agency node is not null use that or else use others
        agencyNode ??=
            SelectSingleNodeSmart(doc, "Unit.RadioName")
            ?? SelectSingleNodeSmart(doc, "Units.Unit.RadioName")
            ?? SelectSingleNodeSmart(doc, "Call.Location.PoliceOri");


        // CALL NUMBER

        XmlNode? callNode = null;

        if (!string.IsNullOrWhiteSpace(callNumberPath))
        {
            callNode = SelectSingleNodeSmart(doc, callNumberPath);
        }

        callNode ??=
            SelectSingleNodeSmart(doc, "IncidentData.MasterIncidentNumber")
            ?? SelectSingleNodeSmart(doc, "Call.CallNumber");

        // 102 – REQUIRED TAGS MISSING
        if (agencyNode == null || callNode == null)
        {
            result.ErrorCode = "102";
            result.ErrorDescription = "XML does not contain required Incident/Unit tags";
            return result;
        }

        var agencyId = agencyNode.InnerText?.Trim() ?? string.Empty;
        var callNumber = callNode.InnerText?.Trim() ?? string.Empty;

        result.AgencyIdentifier = agencyId;
        result.CallNumber = callNumber;

        // 104 – AGENCY ID NULL
        if (string.IsNullOrWhiteSpace(agencyId))
        {
            result.ErrorCode = "104";
            result.ErrorDescription = "AgencyID is null";
            return result;
        }

        // 105 – CALL NUMBER NULL
        if (string.IsNullOrWhiteSpace(callNumber))
        {
            result.ErrorCode = "105";
            result.ErrorDescription = "CallNumber is null";
            return result;
        }

        // 107 – INVALID CALL NUMBER
        if (ValidationHelper.HasSpecialChar(callNumber))
        {
            result.ErrorCode = "107";
            result.ErrorDescription = "Incident number is invalid";
            return result;
        }

        result.IsValid = true;
        return result;
    }

    
    // Namespace-aware XPath helper (works with ANY namespace)
    
    private static XmlNode? SelectSingleNodeSmart(XmlDocument doc, string dotPath)
    {
        if (string.IsNullOrWhiteSpace(dotPath))
            return null;

        var parts = dotPath.Split('.', StringSplitOptions.RemoveEmptyEntries);

        var root = doc.DocumentElement;
        if (root == null)
            return null;

        // If there is NO namespace, use simple XPath
        if (string.IsNullOrWhiteSpace(root.NamespaceURI))
        {
            return doc.SelectSingleNode("//" + string.Join("/", parts));
        }

        // Namespace existsbind root namespace dynamically
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("ns", root.NamespaceURI);

        
        var xpath = "//ns:" + string.Join("/ns:", parts);

        return doc.SelectSingleNode(xpath, nsmgr);
    }
}
