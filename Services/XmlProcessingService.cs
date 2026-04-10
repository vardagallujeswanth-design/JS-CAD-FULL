using CadProcessorService.Helpers;
using CadProcessorService.Infrastructure;
using CadProcessorService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Xml;

namespace CadProcessorService.Services
{
    public class FileProcessResult
    {
        public bool Success { get; set; }
        public bool ShouldRetry { get; set; }
        public bool IsOtherAgency { get; set; }
        public string AgencyCode { get; set; } = "UNKNOWN";
        public int CallId { get; set; } = 0;
    }

    public class XmlProcessingService
    {
        private readonly DatabaseExecutor _db;
        private readonly LegacyValidationService _validator;
        private readonly LegacyImportLogger _legacyLogger;
        private readonly EmailService _email;
        private readonly ILogger<XmlProcessingService> _logger;
        private readonly CadProcessingOptions _options;
        private List<DbCallerRuleCategoryConfig> _callerRuleConfig = new();//-----------------newly
        private DbRetryConfig _retryConfig = new();
        private readonly ConcurrentDictionary<int, List<DbProcedureConfig>> _procCache = new();
        private readonly ConcurrentDictionary<(int, int), List<DbFieldMapping>> _fieldCache = new();
        private readonly ConcurrentDictionary<(int, int), List<DbProviderFieldRule>> _ruleCache = new();
        private DateTime _lastCacheUtc = DateTime.MinValue;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
        private readonly object _cacheLock = new object();

       

        public XmlProcessingService(
            DatabaseExecutor db,
            LegacyValidationService validator,
            LegacyImportLogger legacyLogger,
            EmailService email,
            IOptions<CadProcessingOptions> options,
            ILogger<XmlProcessingService> logger)
        {
            _db = db;
            _validator = validator;
            _legacyLogger = legacyLogger;
            _email = email;
            _options = options.Value;
            _logger = logger;
        }
        private int _applicationId = 0;

        public void SetApplicationId(int applicationId)
        {
            _applicationId = applicationId;
        }

        public void SetRetryConfig(DbRetryConfig retry) => _retryConfig = retry;

        #region Process Single File
        public FileProcessResult ProcessFile(DbProviderConfig provider, DbFolderConfig folder, string filePath)
        {
            var result = new FileProcessResult();
            var fileName = Path.GetFileName(filePath);
            var guid = Guid.NewGuid();
            int systemUserId = _options.SystemUserId;
            int callId = 0;

            XmlDocument doc = new();

            // 103 - XML Invalid / unreadable
            try
            {
                if (!FileHelper.CheckFileHasCopied(filePath))
                    throw new Exception("File not ready");

                doc.Load(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "XML load failed");

                _legacyLogger.LogCadData(fileName, null, 0, "103 - XML File Broken or could not open", systemUserId, provider.ProviderId);
                _legacyLogger.LogServiceImport(fileName, "", 0, 0, "ERROR", "103", "XML File Broken or could not open", guid, systemUserId, provider.ProviderId);
                _email.SendFailure(fileName, "XML File Broken or could not open");

                return result;
            }

            EnsureProviderCacheLoaded(provider.ProviderId);
            EnsureAppConfigLoaded();

            // VALIDATION
            var validation = _validator.ValidateFile(filePath, doc, provider.IdentificationPath, provider.CallNumberNode);

            // Always set AgencyCode even on validation failure
            result.AgencyCode = validation.AgencyIdentifier ?? "UNKNOWN";
            var radioName = validation.AgencyIdentifier ?? string.Empty;

            if (!validation.IsValid)
            {
                var cadId = _legacyLogger.LogCadData(fileName, doc, 0, $"{validation.ErrorCode} - {validation.ErrorDescription}", systemUserId, provider.ProviderId);
                _legacyLogger.LogServiceImport(fileName, radioName, 0, cadId, "ERROR", validation.ErrorCode, validation.ErrorDescription, guid, systemUserId, provider.ProviderId);
                _email.SendFailure(fileName, validation.ErrorDescription);

                result.IsOtherAgency = validation.ErrorCode == "106";
                return result;
            }

            // 106 - AGENCY CHECK
            int agencyId = ResolveAgency(provider.ProviderId, radioName);
            if (agencyId <= 0)
            {
                var cadId = _legacyLogger.LogCadData(fileName, doc, 0, "106 - Agency ID does not exist", systemUserId, provider.ProviderId);
                _legacyLogger.LogServiceImport(fileName, radioName, 0, cadId, "ERROR", "106", "Agency ID does not exist", guid, systemUserId, provider.ProviderId);
                _email.SendFailure(fileName, "Agency ID does not exist");

                result.IsOtherAgency = true;
                return result;
            }

            // 111 / 112 - OVERRIDE CHECK
            var dtOverride = _db.ExecuteStoredProcedureToDataTable(
                "CAD_CheckifCallReportOverridden",
                new() { ["@CallNumber"] = validation.CallNumber, ["@AgencyID"] = agencyId });

            if (dtOverride.Rows.Count > 0)
            {
                bool overridden = Convert.ToBoolean(dtOverride.Rows[0]["IsOverriden"]);
                callId = Convert.ToInt32(dtOverride.Rows[0]["CallID"]);
                result.CallId = callId;

                if (overridden)
                {
                    string code = "112";
                    string desc = "Record overridden";
                    var cadId = _legacyLogger.LogCadData(fileName, doc, callId, $"{code} - {desc}", systemUserId, provider.ProviderId);
                    _legacyLogger.LogServiceImport(fileName, radioName, callId, cadId, "ERROR", code, desc, guid, systemUserId, provider.ProviderId);
                    _email.SendFailure(fileName, desc);
                    result.Success = true;
                    return result;
                }
            }

            // INSERT PIPELINE
            try
            {
                // First, insert Call, Caller, etc. (skip officer insert)
                foreach (var proc in _procCache[provider.ProviderId])
                {
                    // Skip officer insert - we'll do it after we have a CallID 
                    if (proc.ProcedureName == "CAD_CallOfficerDetailInsert" || proc.ProcedureName == "CAD_CallerInsert")//why we are skipping the procedures ?
                        continue;

                    var parameters = BuildParameters(provider.ProviderId, proc.ProcedureId, doc, validation, systemUserId);
                    AddProcedureSpecificParameters(proc.ProcedureName, parameters, validation, doc, callId, provider, proc.ProcedureId);

                    int ret = _db.ExecuteNonQuery(proc.ProcedureName, parameters, "@ReturnValue");
                    if (ret > 0 && callId == 0)
                        callId = ret;
                }

                // NOW insert officers after we have a valid CallID
                if (callId > 0)
                {
                    InsertCallersForCall(callId, doc, provider, validation);
                    InsertOfficersForCall(callId, doc, provider, validation);
                }
                else
                {
                    _logger.LogWarning("CallID was not created, skipping officer insertion");
                }

                var cadDataId = _legacyLogger.LogCadData(fileName, doc, callId, "OK", systemUserId, provider.ProviderId);
                _legacyLogger.LogServiceImport(fileName, radioName, callId, cadDataId, "OK", "", "", guid, systemUserId, provider.ProviderId);
                _email.SendSuccess(fileName, $"Processed successfully (CallID={callId})");

                result.Success = true;
                result.CallId = callId;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "108 - INSERT FAILURE");

                var cadId = _legacyLogger.LogCadData(fileName, doc, callId, "108 - ERROR ON INSERTING CALL TABLE", systemUserId, provider.ProviderId);
                _legacyLogger.LogServiceImport(fileName, radioName, callId, cadId, "ERROR", "108", "ERROR ON INSERTING CALL TABLE", guid, systemUserId, provider.ProviderId);
                _email.SendFailure(fileName, "ERROR ON INSERTING CALL TABLE");

                result.ShouldRetry = _retryConfig.Enabled;
                return result;
            }
        }
        #endregion

        #region Helpers

        private class OfficerData
        {
            public string FirstName { get; set; } = "";
            public string LastName { get; set; } = "";
            public string FullName { get; set; } = "";
            public bool? IsPrimaryFromAttribute { get; set; } = null;
        }


        #region BuildParameters
        private Dictionary<string, object?> BuildParameters(int providerId, int procId, XmlDocument doc, LegacyValidationResult validation, int systemUserId)
        {
            if (!_fieldCache.TryGetValue((providerId, procId), out var mappings))
                throw new InvalidOperationException($"Missing field mappings for ProviderId={providerId}, ProcedureId={procId}");

            if (!_ruleCache.TryGetValue((providerId, procId), out var rules))
                rules = new List<DbProviderFieldRule>();

            var p = new Dictionary<string, object?>();

            foreach (var map in mappings)
            {
                var node = SelectChildNodeSmart(doc, map.XmlPath);
                string? value = string.IsNullOrWhiteSpace(node?.InnerText) ? map.DefaultValue : node.InnerText.Trim();

                // Apply provider rules
                foreach (var rule in rules)
                {
                    if (rule.ParameterName != map.ParameterName || !rule.IsActive)
                        continue;

                    value = ApplyRule(value, doc, rule);
                }

                p[map.ParameterName] = value;
            }

            Normalize(p);
            return p;
        }
        #endregion BuildParameters

        #region Rule Application
        private string? ApplyRule(string? value, XmlDocument doc, DbProviderFieldRule rule)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.RuleType))
                return value;

            switch (rule.RuleType)
            {
                case "RemoveUtcOffset":
                    if (!string.IsNullOrWhiteSpace(value))
                        value = Regex.Replace(value, @"Z$", "");
                    break;

                case "ConvertFromUtc":
                    if (!string.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, out var dt))
                    {
                        try
                        {
                            var tz = TimeZoneInfo.FindSystemTimeZoneById(rule.RuleValue ?? "Eastern Standard Time");
                            value = TimeZoneInfo.ConvertTimeFromUtc(dt, tz).ToString("yyyy-MM-dd HH:mm:ss");
                        }
                        catch { }
                    }
                    break;

                case "ConcatenateChildNodes":
                    if (!string.IsNullOrWhiteSpace(rule.RuleValue))
                        value = ConcatenateNodes(doc, rule.RuleValue);
                    break;
            }

            return value;
        }
        #endregion Rule Application


        #region ConcatenateNodes
        private string ConcatenateNodes(XmlDocument doc, string dotPath)
        {
            if (string.IsNullOrWhiteSpace(dotPath))
                return "";

            var parts = dotPath.Split('.', StringSplitOptions.RemoveEmptyEntries);

            var root = doc.DocumentElement;
            if (root == null) return "";

            XmlNodeList? nodes;

            if (string.IsNullOrWhiteSpace(root.NamespaceURI))
            {
                // No namespace
                var xpath = "//" + string.Join("/", parts);
                nodes = doc.SelectNodes(xpath);
            }
            else
            {
                // With namespace
                var nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("ns", root.NamespaceURI);
                var xpath = "//ns:" + string.Join("/ns:", parts);
                nodes = doc.SelectNodes(xpath, nsmgr);
            }

            if (nodes == null) return "";

            int index = 1;
            var lines = new List<string>();
            foreach (XmlNode node in nodes)
            {
                if (!string.IsNullOrWhiteSpace(node.InnerText))
                {
                    lines.Add($"{index}. {node.InnerText.Trim()}");
                    index++;
                }
            }

            return string.Join(Environment.NewLine, lines);

        }
        #endregion ConcatenateNodes


        #region AddProcedureSpecificParameters
        private void AddProcedureSpecificParameters(string procName, Dictionary<string, object?> parameters, LegacyValidationResult validation, XmlDocument doc, int callId, DbProviderConfig provider, int procedureId)
        {
            switch (procName)
            {
                case "CAD_CallInsert":
                    parameters["@RadioName"] = validation.AgencyIdentifier;
                    parameters["@callnumber"] = validation.CallNumber;
                    parameters["@IsMigrated"] = 1;
                    parameters["@IsLocked"] = 0;
                    parameters["@IsCreatedByCAD"] = 0;
                    parameters["@IsCreateIncidentByCAD"] = 1;
                    parameters["@createduserid"] = 0;
                    parameters["@ProviderId"] = provider.ProviderId;
                    break;

                    //case "CAD_CallerInsert":
                    //    var (firstName, lastName, middleName) = GetCallerNames(doc, provider);
                    //    parameters["@RadioName"] = validation.AgencyIdentifier;
                    //    parameters["@CreateUID"] = 0;
                    //    parameters["@createduserid"] = 0;
                    //    parameters["@CallID"] = callId;
                    //    parameters["@FirstName"] = firstName;
                    //    parameters["@LastName"] = lastName;
                    //    parameters["@MiddleName"] = middleName;
                    //    parameters["@ProviderId"] = provider.ProviderId;
                    //    break;

            }
        }
        #endregion AddProcedureSpecificParameters


        #region Caller Insertion
        private void InsertCallersForCall(int callId, XmlDocument doc, DbProviderConfig provider, LegacyValidationResult validation)
        {
           
            var callerProc = _procCache[provider.ProviderId] .FirstOrDefault(p => p.ProcedureName == "CAD_CallerInsert");
            if (callerProc == null)
            {
                _logger.LogWarning("CAD_CallerInsert not found for ProviderId={Id}", provider.ProviderId);
                return;
            }

           
            var rules = callerProc.FieldRules .Where(r => r.IsActive && r.RuleType == "XPath").ToList();

            var ruleMap = rules.Where(r => !string.IsNullOrWhiteSpace(r.RuleCategory)).ToDictionary(r => r.RuleCategory!,r => (r.RuleValue ?? "")
                         .Split('|', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).ToList() );

            _logger.LogInformation("Loaded {Count} caller rule categories: [{Keys}]",ruleMap.Count, string.Join(", ", ruleMap.Keys));

          
            if (_callerRuleConfig.Count == 0)
            {
                _logger.LogWarning("No caller rule category config found in DB. Cannot process callers.");
                return;
            }

            // Check required categories from DB config
            var requiredCategories = _callerRuleConfig.Where(c => c.IsRequired).ToList();
            foreach (var config in requiredCategories)
            {
                if (!ruleMap.ContainsKey(config.CategoryName) || ruleMap[config.CategoryName].Count == 0)
                {
                    _logger.LogWarning(
                        "Required rule category '{Category}' (Role={Role}) missing for ProviderId={Id}",
                        config.CategoryName, config.CategoryRole, provider.ProviderId);
                    return;
                }
            }

            // Extract config objects by category role

            // stores category role ,category name ,is active
            var fullNameConfig = _callerRuleConfig.FirstOrDefault(c => c.CategoryRole == "FullName");
            var firstNameConfig = _callerRuleConfig.FirstOrDefault(c => c.CategoryRole == "FirstName");
            var lastNameConfig = _callerRuleConfig.FirstOrDefault(c => c.CategoryRole == "LastName");
            var nodeConfig = _callerRuleConfig.FirstOrDefault(c => c.CategoryRole == "NodeLocator");
            var phoneConfig = _callerRuleConfig.FirstOrDefault(c => c.CategoryRole == "Phone");

            if (fullNameConfig == null && firstNameConfig == null)
            {
                _logger.LogWarning("No FullName or FirstName role configured in DB for caller rules.");
                return;
            }


            // Name tags — driven by DB roles// = ["Name", "name", "callername", "CallerName"]
            var fullNameTags = fullNameConfig != null && ruleMap.ContainsKey(fullNameConfig.CategoryName) ? ruleMap[fullNameConfig.CategoryName] : new List<string>();

            var firstNameTags = firstNameConfig != null && ruleMap.ContainsKey(firstNameConfig.CategoryName) ? ruleMap[firstNameConfig.CategoryName] : new List<string>();

            var lastNameTags = lastNameConfig != null && ruleMap.ContainsKey(lastNameConfig.CategoryName) ? ruleMap[lastNameConfig.CategoryName] : new List<string>();


          

            string? phoneChildNode = phoneConfig != null && ruleMap.ContainsKey(phoneConfig.CategoryName)? ruleMap[phoneConfig.CategoryName].FirstOrDefault(): null;

            if (string.IsNullOrWhiteSpace(phoneChildNode))
            {
                _logger.LogWarning("CallerPhone tag could not be resolved from DB config.");
                return;
            }

         
       

            var callerNodeXPaths = nodeConfig != null && ruleMap.ContainsKey(nodeConfig.CategoryName)? ruleMap[nodeConfig.CategoryName]: new List<string>();

            if (callerNodeXPaths.Count == 0)
            {
                _logger.LogWarning("CallerNode XPath could not be resolved from DB config.");
                return;
            }

            // Collect nodes from ALL matching XPaths
            var allCallerNodes = new List<XmlNode>();
            foreach (var xpath in callerNodeXPaths)
            {
                var nodes = SelectNodesWithXPath(doc, xpath);
                if (nodes != null && nodes.Count > 0)
                {
                    foreach (XmlNode n in nodes)
                        allCallerNodes.Add(n);

                    _logger.LogInformation("Found {Count} caller node(s) using XPath: {XPath}",nodes.Count, xpath);
                }
                else
                {
                    _logger.LogWarning("No caller nodes found for XPath: {XPath}", xpath);
                }
            }

            if (allCallerNodes.Count == 0)
            {
                _logger.LogWarning("No caller nodes found for any XPath.");
                return;
            }

            _logger.LogInformation("Total caller nodes found: {Count}", allCallerNodes.Count);
          
            var insertedPhones = new HashSet<string>();

            foreach (XmlNode callerNode in allCallerNodes)
            {
                                                      //tags in fullname find all tags untill reached
                string fullName = fullNameTags.Select(t => SelectChildNodeSmart(callerNode, t)?.InnerText?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    string fname = firstNameTags.Select(t => SelectChildNodeSmart(callerNode, t)?.InnerText?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

                    string lname = lastNameTags.Select(t => SelectChildNodeSmart(callerNode, t)?.InnerText?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

                    fullName = $"{fname} {lname}".Trim();
                }

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    _logger.LogWarning("Skipping caller — no name found. Tried: [{Tags}]",
                        string.Join(",", fullNameTags.Concat(firstNameTags).Concat(lastNameTags)));
                    continue;
                }

                string phoneNumber = SelectChildNodeSmart(callerNode, phoneChildNode)?.InnerText?.Trim() ?? "";
                   
                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    _logger.LogWarning("Skipping caller with no phone: {Name}", fullName);
                    continue;
                }

                phoneNumber = new string(phoneNumber.Where(char.IsDigit).ToArray());

                
                if (!insertedPhones.Add(phoneNumber))
                {
                    _logger.LogWarning("Skipping duplicate caller by phone: {Phone}", phoneNumber);
                    continue;
                }

            
                var (firstName, lastName, middleName) = SplitCallerName(fullName);

                XmlDocument callerDoc = new XmlDocument();
                callerDoc.LoadXml(callerNode.OuterXml);

                var baseParams = BuildParameters(provider.ProviderId, callerProc.ProcedureId,callerDoc, validation, _options.SystemUserId);

                var parameters = new Dictionary<string, object?>(baseParams)
                {
                    ["@FirstName"] = firstName,
                    ["@LastName"] = lastName,
                    ["@MiddleName"] = middleName,
                    ["@CallID"] = callId,
                    ["@RadioName"] = validation.AgencyIdentifier,
                    ["@CreateUID"] = _options.SystemUserId,
                    ["@createduserid"] = _options.SystemUserId,
                    ["@ProviderId"] = provider.ProviderId
                };

                _logger.LogInformation(
                    "Inserting caller — First: {First} | Last: {Last} | Full: {Full} | Phone: {Phone}",
                    firstName, lastName, fullName, phoneNumber);

                _db.ExecuteNonQuery("CAD_CallerInsert", parameters, "@ReturnValue");
            }
        }
        #endregion Caller Insertion
        private (string FirstName, string LastName, string MiddleName) SplitCallerName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return (string.Empty, string.Empty, string.Empty);

            // Remove commas completely
            fullName = fullName.Replace(",", " ").Trim();

            var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            string first = words.Length >= 1 ? words[0] : "";
            string last = words.Length >= 2 ? words[words.Length - 1] : "";
            string middle = words.Length >= 3
                ? string.Join(" ", words.Skip(1).Take(words.Length - 2))
                : "";

            return (first, last, middle);
        }


        #region Officer Insertion
        private void InsertOfficersForCall(int callId, XmlDocument doc, DbProviderConfig provider, LegacyValidationResult validation)
        {
            if (callId <= 0)
            {
                _logger.LogWarning("Cannot insert officers: CallID is 0 or negative");
                return;
            }

            _logger.LogInformation($"STARTING OFFICER INSERTION FOR CallID={callId}");

            if (!_procCache.TryGetValue(provider.ProviderId, out var procedures))
            {
                _logger.LogWarning($"No procedures found for ProviderId={provider.ProviderId}");
                return;
            }

            var officerProc = procedures.FirstOrDefault(p => p.ProcedureName == "CAD_CallOfficerDetailInsert");
            if (officerProc == null)
            {
                _logger.LogWarning($"CAD_CallOfficerDetailInsert procedure not found for ProviderId={provider.ProviderId}");
                return;
            }

            _logger.LogInformation($" Found officer procedure: ProcedureId={officerProc.ProcedureId}, ProcedureName={officerProc.ProcedureName}");

            if (!_ruleCache.TryGetValue((provider.ProviderId, officerProc.ProcedureId), out var officerRules))
            {
                _logger.LogWarning($"No officer rules found for ProviderId={provider.ProviderId}, ProcedureId={officerProc.ProcedureId}");
                return;
            }

            _logger.LogInformation($"Total rules in cache for this procedure: {officerRules.Count}");

            var activeOfficerRules = officerRules.Where(r => r.IsActive &&
                           (r.RuleType == "XPath" || r.RuleType == "XPathConditional") &&
                           (r.ParameterName == "PrimaryOfficer" || r.ParameterName == "OtherOfficer")).OrderBy(r => r.RuleOrder) .ToList();

            _logger.LogInformation($" Active officer rules found: {activeOfficerRules.Count}");

            if (activeOfficerRules.Count == 0)
            {
                _logger.LogWarning($" No active officer rules for ProviderId={provider.ProviderId}, ProcedureId={officerProc.ProcedureId}");
                return;
            }

            foreach (var rule in activeOfficerRules)
            {
                _logger.LogInformation($"Rule #{rule.RuleOrder}: RuleId={rule.RuleId}, Parameter={rule.ParameterName}, Type={rule.RuleType}, XPath={rule.RuleValue}");
            }

           
            string attrName = officerRules
                .FirstOrDefault(r => r.IsActive && r.ParameterName == "OfficerIsPrimaryAttribute")
                ?.RuleValue ?? "IsPrimary";

            bool xmlHasAttributePrimary = HasOfficerWithPrimaryAttribute(doc, attrName);

            _logger.LogInformation(
                "Pre-scan: isPrimary attribute check — attrName='{Attr}', found={Found}",
                attrName, xmlHasAttributePrimary);

            int totalOfficersInserted = 0;
            var insertedOfficers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool primaryAlreadyInserted = false;

            foreach (var rule in activeOfficerRules)
            {
                bool isPrimary = rule.ParameterName == "PrimaryOfficer";

                // Auto promotion block
                if (rule.RuleType == "XPathConditional")
                {
                    // SKIP if any Officer node already has isPrimary attribute in XML
                    if (xmlHasAttributePrimary)
                    {
                        _logger.LogInformation(
                            "Skipping XPathConditional rule {RuleId} — isPrimary attribute present in XML, attribute-based detection will handle primary",
                            rule.RuleId);
                        continue;
                    }

                    var officers = ExtractOfficersFromRule(doc, rule, attrName);

                    if (officers.Count == 0)
                    {
                        _logger.LogWarning("No primary officer found from XPathConditional rule.");
                        continue;
                    }

                    // Check IsPrimary attribute first, else use document order
                    var firstOfficer = officers.FirstOrDefault(o => o.IsPrimaryFromAttribute == true)
                                       ?? officers.First();

                    _logger.LogInformation($" Officer: FirstName='{firstOfficer.FirstName}', LastName='{firstOfficer.LastName}', FullName='{firstOfficer.FullName}'");

                    if (!insertedOfficers.Add(firstOfficer.FullName.Trim()))
                    {
                        _logger.LogWarning("Skipping duplicate officer: {Name}", firstOfficer.FullName);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(firstOfficer.FullName))
                    {
                        try
                        {
                            InsertOfficer(callId, firstOfficer.FullName, true, validation.AgencyIdentifier, provider.ProviderId);
                            totalOfficersInserted++;
                            primaryAlreadyInserted = true;
                            _logger.LogInformation($" Auto-promoted as Primary: {firstOfficer.FullName}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $" Failed to insert officer: {firstOfficer.FullName}");
                            return;
                        }
                    }

                    continue;
                }

                // Normal XPath rule
                var normalOfficers = ExtractOfficersFromRule(doc, rule, attrName);

                foreach (var officer in normalOfficers)
                {
                    _logger.LogInformation($" Officer: FirstName='{officer.FirstName}', LastName='{officer.LastName}', FullName='{officer.FullName}'");

                    if (!insertedOfficers.Add(officer.FullName.Trim()))
                    {
                        _logger.LogWarning("Skipping duplicate officer: {Name}", officer.FullName);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(officer.FullName))
                    {
                        try
                        {
                            bool finalIsPrimary;

                            // IsPrimary attribute logic
                            if (officer.IsPrimaryFromAttribute.HasValue && !primaryAlreadyInserted)
                            {
                                // Attribute found AND no primary yet — trust attribute
                                finalIsPrimary = officer.IsPrimaryFromAttribute.Value;
                                _logger.LogInformation($" IsPrimary from attribute: {finalIsPrimary}");
                            }
                            else if (officer.IsPrimaryFromAttribute.HasValue && primaryAlreadyInserted)
                            {
                                // Attribute found BUT primary already inserted — insert as Other
                                finalIsPrimary = false;
                                _logger.LogInformation($" IsPrimary attribute ignored — primary already inserted. Inserting as Other.");
                            }
                            else
                            {
                                // No attribute — use rule flag
                                finalIsPrimary = isPrimary;
                            }

                            InsertOfficer(callId, officer.FullName, finalIsPrimary, validation.AgencyIdentifier, provider.ProviderId);
                            totalOfficersInserted++;

                            if (finalIsPrimary)
                                primaryAlreadyInserted = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $" Failed to insert officer: {officer.FullName}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"  Skipping officer with empty FullName (FirstName='{officer.FirstName}', LastName='{officer.LastName}')");
                    }
                }
            }

            _logger.LogInformation($"OFFICER INSERTION COMPLETE");
            _logger.LogInformation($"Total officers inserted: {totalOfficersInserted}");
        }
        #endregion Officer Insertion
        private bool HasOfficerWithPrimaryAttribute(XmlDocument doc, string attrName)
        {
            var nodes = SelectNodesWithXPath(doc, "//Officer");
            if (nodes == null || nodes.Count == 0)
                return false;

            foreach (XmlNode node in nodes)
            {
                var attr = node.Attributes?
                    .OfType<XmlAttribute>()
                    .FirstOrDefault(a => string.Equals(a.Name, attrName, StringComparison.OrdinalIgnoreCase));

                if (attr == null) continue;

                var val = attr.Value.Trim().ToLower();
                if (val is "true" or "1" or "yes")
                {
                    _logger.LogInformation(
                        "Pre-scan found officer with {Attr}='{Val}'", attrName, attr.Value.Trim());
                    return true;
                }
            }

            return false;
        }










        private List<OfficerData> ExtractOfficersFromRule(XmlDocument doc, DbProviderFieldRule rule, string attrName )
        {
            var officers = new List<OfficerData>();

            if (string.IsNullOrWhiteSpace(rule.RuleValue))
            {
                _logger.LogWarning($"Rule {rule.RuleId} has empty RuleValue");
                return officers;
            }

            try
            {
                switch (rule.RuleType)
                {
                    case "XPath":
                        // Provider 1: Single name field that needs splitting
                        // Example: DOC/IncidentData/PrimaryOfficer or DOC/Units/Unit/Officers/Officer
                        officers = ExtractOfficersFromSimpleXPath(doc, rule.RuleValue, attrName);
                        break;

                    case "XPathConditional":
                        // Provider 3: Separate FirstName and LastName fields with conditions
                        officers = ExtractOfficersFromConditionalXPath(doc, rule.RuleValue);
                        break;

                    default:
                        _logger.LogWarning($"Unknown rule type: {rule.RuleType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting officers from rule {rule.RuleId}: {rule.RuleValue}");
            }

            return officers;
        }


        private List<OfficerData> ExtractOfficersFromSimpleXPath(XmlDocument doc, string xPath, string attrName )
        {
            var officers = new List<OfficerData>();
            _logger.LogDebug($"ExtractOfficersFromSimpleXPath: {xPath}");

            var xpaths = xPath.Split('|', StringSplitOptions.RemoveEmptyEntries)
                              .Select(x => x.Trim())
                              .Where(x => !string.IsNullOrWhiteSpace(x))
                              .ToList();

            foreach (var singleXPath in xpaths)
            {
                var nodes = SelectNodesWithXPath(doc, singleXPath);

                if (nodes == null || nodes.Count == 0)
                {
                    _logger.LogWarning($"No nodes found for XPath: {singleXPath}");
                    continue;
                }

                _logger.LogInformation($"Found {nodes.Count} officer node(s)");

                foreach (XmlNode node in nodes)
                {
                    string fullName = ExtractFullName(node) ?? "";
                    _logger.LogDebug($"Node InnerText: '{fullName}'");

                    if (!string.IsNullOrWhiteSpace(fullName))
                    {
                        var (firstName, lastName) = SplitOfficerName(fullName);

                        if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
                        {
                            // Use configured attrName from DB (rules 3014/3015), case-insensitive
                            var attr = node.Attributes?
                                .OfType<XmlAttribute>()
                                .FirstOrDefault(a => string.Equals(
                                    a.Name, attrName, StringComparison.OrdinalIgnoreCase));

                            bool? isPrimaryFromAttribute = null;

                            if (attr != null)
                            {
                                var val = attr.Value.Trim().ToLower();
                                isPrimaryFromAttribute = val is "true" or "1" or "yes";
                                _logger.LogInformation(
                                    "Officer '{Name}' has {Attr} attribute = '{Val}'  {Result}",
                                    fullName, attrName, attr.Value.Trim(), isPrimaryFromAttribute);
                            }

                            officers.Add(new OfficerData
                            {
                                FirstName = firstName,
                                LastName = lastName,
                                FullName = fullName.Trim(),
                                IsPrimaryFromAttribute = isPrimaryFromAttribute
                            });

                            _logger.LogDebug($"Extracted: '{fullName}' First:'{firstName}', Last:'{lastName}'");
                        }
                        else
                        {
                            _logger.LogWarning($"Could not split name: '{fullName}' First:'{firstName}', Last:'{lastName}'");
                        }
                    }
                }
            }

            return officers;
        }



        private List<OfficerData> ExtractOfficersFromConditionalXPath(XmlDocument doc, string xPath)
        {
            var officers = new List<OfficerData>();
            _logger.LogDebug($"ExtractOfficersFromConditionalXPath: {xPath}");

         
            var xpaths = xPath.Split('|', StringSplitOptions.RemoveEmptyEntries)
                              .Select(x => x.Trim())
                              .Where(x => !string.IsNullOrWhiteSpace(x))
                              .ToList();

            foreach (var singleXPath in xpaths)
            {
                var personnelNodes = SelectNodesWithXPath(doc, singleXPath);

                if (personnelNodes == null || personnelNodes.Count == 0)
                {
                    _logger.LogWarning($"No personnel nodes found for XPath: {singleXPath}");
                    continue;
                }

                _logger.LogInformation($"Found {personnelNodes.Count} personnel node(s)");

                foreach (XmlNode personnelNode in personnelNodes)
                {
                    string fullName = ExtractFullName(personnelNode);

                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        _logger.LogWarning("Empty officer name.");
                        continue;
                    }

                    var (firstName, lastName) = ParseFullName(fullName);

                    if (!string.IsNullOrWhiteSpace(firstName) &&
                        !string.IsNullOrWhiteSpace(lastName))
                    {
                        officers.Add(new OfficerData
                        {
                            FirstName = firstName,
                            LastName = lastName,
                            FullName = fullName.Trim()
                        });
                        _logger.LogDebug($"Extracted officer: {firstName} {lastName}");
                    }
                    else
                    {
                        _logger.LogWarning($"Could not parse officer name: {fullName}");
                    }
                }
            }

            return officers;
        }



        //----------------------------------------------------------------------------------------------------------------------------

        private string ExtractFullName(XmlNode personnelNode)
        {
            // If node has child elements, combine their text
            if (personnelNode.HasChildNodes &&personnelNode.ChildNodes.OfType<XmlNode>()
                .Any(n => n.NodeType == XmlNodeType.Element))
            {
                return string.Join(" ", personnelNode.ChildNodes
                    .OfType<XmlNode>()
                    .Where(n => n.NodeType == XmlNodeType.Element)
                    .Select(n => n.InnerText?.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t)));
            }
            // Otherwise use inner text
            return personnelNode.InnerText?.Trim() ?? string.Empty;
        }







        private (string FirstName, string LastName) ParseFullName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return (string.Empty, string.Empty);

            // Case 1: Comma format (Last , First)
            if (fullName.Contains(","))
            {
                var parts = fullName.Split(',');

                if (parts.Length >= 2)
                {
                    var last = parts[0].Trim();
                    var first = parts[1].Trim();
                    return (first, last);
                }
            }

            // Case 2: Space format (First Last)
            var spaceParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (spaceParts.Length >= 2)
            {
                return (spaceParts[0].Trim(), spaceParts[1].Trim());
            }

            return (string.Empty, string.Empty);
        }








        //----------------------------------------------------------------------------------------------------------------------------

        private (string FirstName, string LastName)                SplitOfficerName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return (string.Empty, string.Empty);

            // Remove all extra whitespace
            fullName = Regex.Replace(fullName.Trim(), @"\s+", " ");

            // Check if comma-separated
            if (fullName.Contains(','))
            {
                // Split by comma and trim spaces
                var parts = fullName.Split(',') .Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();

                if (parts.Length >= 2)
                {
                    // XML format is "LastName, FirstName" 
                    // So parts[0] = LastName, parts[1] = FirstName
                    // Return as (FirstName, LastName)
                    string lastName = parts[0];
                    string firstName = parts[1];
                    return (firstName, lastName);
                }
                else if (parts.Length == 1)
                {
                    return (string.Empty, parts[0]);
                }
            }

            // No comma - space-separated format
            var words = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
                return (string.Empty, string.Empty);

            if (words.Length == 1)
                return (words[0], string.Empty);

            if (words.Length == 2)
                return (words[0], words[1]);

            // 3+ words: First word is FirstName, rest is LastName
            return (words[0], string.Join(" ", words.Skip(1)));
        }



        private XmlNodeList? SelectNodesWithXPath(XmlDocument doc, string xpath)
        {
            if (string.IsNullOrWhiteSpace(xpath))
                return null;

            var root = doc.DocumentElement;
            if (root == null)
                return null;

            try
            {
                if (string.IsNullOrWhiteSpace(root.NamespaceURI))
                {
                    // No namespace - use XPath as-is
                    _logger.LogDebug($"Executing XPath (no namespace): {xpath}");
                    return doc.SelectNodes(xpath);
                }
                else
                {
                    // If XPath uses local-name() — skip namespace conversion
                    if (xpath.Contains("local-name()"))
                    {
                        _logger.LogDebug($"Executing XPath (local-name, skip namespace): {xpath}");
                        return doc.SelectNodes(xpath);
                    }

                    // With namespace - convert to namespaced XPath
                    var nsmgr = new XmlNamespaceManager(doc.NameTable);
                    nsmgr.AddNamespace("ns", root.NamespaceURI);

                    var namespacedXPath = ConvertXPathToNamespaced(xpath);
                    _logger.LogDebug($"Executing XPath (with namespace): {namespacedXPath}");
                    return doc.SelectNodes(namespacedXPath, nsmgr);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing XPath: {xpath}");
                return null;
            }
        }

        //private string ConvertXPathToNamespaced(string xpath)
        //{
        //    var result = Regex.Replace(xpath, @"(?<=[/]|^)([a-zA-Z][a-zA-Z0-9_\-]*)", match =>
        //    {
        //        string[] functions = { "not", "and", "or", "position", "last", "count",
        //                        "local-name", "name", "text", "node", "true", "false", "string" };
        //        if (functions.Contains(match.Value))
        //            return match.Value;
        //        return "ns:" + match.Value;
        //    });

        //    _logger.LogDebug($"Converted XPath: {xpath}{result}");
        //    return result;
        //}

        private string ConvertXPathToNamespaced(string xpath)
        {
            var parts = new List<string>();
            var currentPart = "";
            bool insideBracket = false;

            foreach (char c in xpath)
            {
                if (c == '[') { insideBracket = true; currentPart += c; }
                else if (c == ']') { insideBracket = false; currentPart += c; }
                else if (c == '/' && !insideBracket)
                {
                    if (!string.IsNullOrWhiteSpace(currentPart))
                        parts.Add(currentPart);
                    currentPart = "";
                }
                else { currentPart += c; }
            }

            if (!string.IsNullOrWhiteSpace(currentPart))
                parts.Add(currentPart);

            var namespacedParts = new List<string>();
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                namespacedParts.Add(part.Contains("[")
                    ? AddNamespaceToPredicate(part)
                    : $"ns:{part}");
            }

            var result = "//" + string.Join("/", namespacedParts);
            _logger.LogDebug($"Converted XPath: {xpath}  {result}");
            return result;
        }

        private string AddNamespaceToPredicate(string part)
        {
            var match = Regex.Match(part, @"^(\w+)(\[(.+)\])+$");
            if (!match.Success)
                return $"ns:{part}";

            string elementName = match.Groups[1].Value;
            string predicateContent = match.Groups[3].Value;

            string namespacedElement = $"ns:{elementName}";
            string namespacedPredicate = AddNamespaceToPredicateContent(predicateContent);

            return $"{namespacedElement}[{namespacedPredicate}]";
        }

        private string AddNamespaceToPredicateContent(string predicate)
        {
            // Skip attributes
            if (predicate.TrimStart().StartsWith("@"))
                return predicate;

            // If predicate contains //* or //  it's an absolute path inside predicate
            // These should NOT be namespaced - they use local-name() function
            if (predicate.Contains("//*") || predicate.Contains("//"))
                return predicate;

            // Skip XPath functions
            string[] functions = { "not", "and", "or", "contains", "starts-with",
                           "normalize-space", "true", "false", "string",
                           "position", "last", "count", "local-name" };
            foreach (var func in functions)
            {
                if (predicate.TrimStart().StartsWith(func + "("))
                    return predicate;
            }

            // Simple element predicate ElementName="value"
            var match = Regex.Match(predicate, @"^(\w+)(=.+)$");
            if (match.Success)
                return $"ns:{match.Groups[1].Value}{match.Groups[2].Value}";

            _logger.LogWarning($"Unexpected predicate format: {predicate}");
            return predicate;
        }


        //method for inserting the officer
        private void InsertOfficer(int callId, string officerName, bool isPrimary, string radioName, int providerId)
        {
            if (callId <= 0 || string.IsNullOrWhiteSpace(officerName))
            {
                _logger.LogWarning($" Cannot insert officer: CallID={callId}, OfficerName='{officerName}'");
                return;
            }

            // Clean the officer name - remove extra spaces
            officerName = Regex.Replace(officerName.Trim(), @"\s+", " ");

            // Normalize name to "LastName, FirstName" format for SP lookup
            if (!officerName.Contains(","))
            {
                var parts = officerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 2)
                {
                    var first = parts[0].Trim();
                    var last = parts[1].Trim();

                    officerName = $"{first}, {last}";
                    _logger.LogInformation($"Normalized to: '{officerName}'");
                }
            }

            _logger.LogInformation($"Inserting: '{officerName}' (IsPrimary={isPrimary})");

            int officerId = 0;

            try
            {
                // Use the existing stored procedure - it already does the lookup
                var dt = _db.ExecuteStoredProcedureToDataTable(
                    "CAD_CheckPrimaryOfficerName",
                    new Dictionary<string, object?>
                    {
                        ["@PrimaryOfficerName"] = officerName,
                        ["@RadioName"] = radioName,
                        ["@ProviderId"] = providerId
                    });

                if (dt.Rows.Count > 0)
                {
                    officerId = Convert.ToInt32(dt.Rows[0]["OTIUserID"]);
                    _logger.LogInformation($" Found in OTIUser: OfficerID={officerId}");
                }
                else
                {
                    _logger.LogError($" Officer NOT FOUND in OTIUser table");
                    _logger.LogError($" Name: '{officerName}'");
                    _logger.LogError($" RadioName: '{radioName}'");
                    _logger.LogError($" ProviderId: {providerId}");

                   
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"     Error looking up officer: '{officerName}'");
            }

            var parameters = new Dictionary<string, object?>
            {
                ["@CallId"] = callId,
                ["@OfficerID"] = officerId,
                ["@IsPrimary"] = isPrimary ? 1 : 0,
                ["@CreateUID"] = _options.SystemUserId,
                ["@ProviderId"] = providerId
            };

            try
            {
                int returnValue = _db.ExecuteNonQuery("CAD_CallOfficerDetailInsert", parameters, "@ReturnValue");

                if (officerId > 0)
                {
                    _logger.LogInformation($" Inserted successfully: OfficerID={officerId}, IsPrimary={isPrimary}, ReturnValue={returnValue}");
                }
                else
                {
                    _logger.LogWarning($" Inserted with OfficerID=0: IsPrimary={isPrimary}, ReturnValue={returnValue}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error inserting CallOfficerDetail: '{officerName}'");
                throw;
            }
        }




        //Method to split a name to first,middle ,last
        private (string FirstName, string LastName, string MiddleName) GetCallerNames(XmlDocument doc, DbProviderConfig provider)
        {
            var callerNode = SelectChildNodeSmart(doc, provider.CallerNameNode);
            string callerName = callerNode?.InnerText?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(callerName))
                return (string.Empty, string.Empty, string.Empty);

            char[] separators = { ' ', ',' };
            var words = callerName.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            string first = words.Length >= 1 ? words[0] : string.Empty;
            string last = words.Length >= 2 ? words[1] : string.Empty;
            string middle = words.Length >= 3 ? words[2] : string.Empty;

            return (first, last, middle);
        }



        private static void Normalize(Dictionary<string, object?> p)
        {
            if (p.TryGetValue("@zip", out var zip))
                p["@zip"] = ValidationHelper.RemoveNonDigits(zip?.ToString());

            if (p.TryGetValue("@PhoneNumber", out var phone))
                p["@PhoneNumber"] = ValidationHelper.RemoveNonDigits(phone?.ToString());

            if (p.TryGetValue("@additionalphonenumber", out var add))
                p["@additionalphonenumber"] = ValidationHelper.RemoveNonDigits(add?.ToString());

            if (p.TryGetValue("@callcompleted", out var cc))
                p["@callcompleted"] = string.IsNullOrWhiteSpace(cc?.ToString()) ? 0 : 1;
        }



        private int ResolveAgency(int providerId, string radioName)
        {
            var dt = _db.ExecuteStoredProcedureToDataTable(
                "CAD_CheckServiceMetaData",
                new Dictionary<string, object?> { ["@ProviderId"] = providerId, ["@RadioName"] = radioName });

            return dt.Rows.Count == 0 ? 0 : Convert.ToInt32(dt.Rows[0]["AgencyID"]);
        }



        //It’s a helper method that simplifies XPath queries and makes them work correctly whether the XML uses namespaces or not.
        private static XmlNode? SelectChildNodeSmart(XmlNode parentNode, string childName)
        {
            if (string.IsNullOrWhiteSpace(childName) || parentNode == null)
                return null;
            var doc = parentNode.OwnerDocument ?? parentNode as XmlDocument;
            if (doc?.DocumentElement == null)
                return null;
            var root = doc.DocumentElement;
            try
            {
                if (string.IsNullOrWhiteSpace(root.NamespaceURI))
                {
                    // No namespace - try direct child first, then descendants
                    var directChild = parentNode.SelectSingleNode(childName);
                    if (directChild != null) return directChild;
                    var descendant = parentNode.SelectSingleNode($".//{childName}");
                    if (descendant != null) return descendant;
                }
                else
                {
                    // With namespace
                    var nsmgr = new XmlNamespaceManager(doc.NameTable);
                    nsmgr.AddNamespace("ns", root.NamespaceURI);
                    var directChild = parentNode.SelectSingleNode($"ns:{childName}", nsmgr);
                    if (directChild != null) return directChild;
                    var descendant = parentNode.SelectSingleNode($".//ns:{childName}", nsmgr);
                    if (descendant != null) return descendant;
                }

                // Case-insensitive fallback 
                return FindChildCaseInsensitive(parentNode, childName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static XmlNode? FindChildCaseInsensitive(XmlNode parent, string childName)
        {
            foreach (XmlNode child in parent.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element &&
                    string.Equals(child.LocalName, childName, StringComparison.OrdinalIgnoreCase))
                    return child;

                // Recurse into descendants
                var found = FindChildCaseInsensitive(child, childName);
                if (found != null) return found;
            }
            return null;
        }



        private void ReloadCacheIfNeeded(DbProviderConfig provider)
        {
                if ((DateTime.UtcNow - _lastCacheUtc) < CacheTtl) return;

                if (!_procCache.ContainsKey(provider.ProviderId))
                    _procCache[provider.ProviderId] = _db.GetProcedures(provider.ProviderId);

                _fieldCache.Clear();
                _ruleCache.Clear();

                foreach (var proc in _procCache[provider.ProviderId])
                {
                    _fieldCache[(provider.ProviderId, proc.ProcedureId)] = _db.GetFieldMappings(provider.ProviderId, proc.ProcedureId);
                    _ruleCache[(provider.ProviderId, proc.ProcedureId)] = _db.GetProviderFieldRules(provider.ProviderId, proc.ProcedureId);

                 
                }

                _lastCacheUtc = DateTime.UtcNow;
            }



        private void EnsureProviderCacheLoaded(int providerId)
        {
            if (_procCache.ContainsKey(providerId))
                return;

            var procs = _db.GetProcedures(providerId);

            if (!_procCache.TryAdd(providerId, procs))
                return; // Another thread already added it

            foreach (var proc in procs)
            {
                var mappings = _db.GetFieldMappings(providerId, proc.ProcedureId);
                _fieldCache.TryAdd((providerId, proc.ProcedureId), mappings);

                var rules = _db.GetProviderFieldRules(providerId, proc.ProcedureId);
                _ruleCache.TryAdd((providerId, proc.ProcedureId), rules);

                proc.FieldRules = rules;
            }


            _logger.LogInformation(
                "Loaded cache for ProviderId={ProviderId}, Procedures={Count}",
                providerId, procs.Count);

        }

        #endregion


        private void EnsureAppConfigLoaded()
        {
            if (_callerRuleConfig.Count > 0)
                return; // Already loaded

            _callerRuleConfig = _db.GetCallerRuleCategoryConfig(_applicationId);//all content in form of list

            _logger.LogInformation(
                "Loaded {Count} caller rule category configs: [{Roles}]",
                _callerRuleConfig.Count,
                string.Join(", ", _callerRuleConfig.Select(c => c.CategoryRole)));
        }


        private string GetNodeValue(XmlDocument doc, string slashPath)
        {
            var node = doc.SelectSingleNode("//" + slashPath.Replace(".", "/"));
            return node?.InnerText?.Trim() ?? "";
        }

        
    //"CallerNode"  - ["//Caller", "//CallerDetails"],
    //"FullName"    - ["Name", "name", "callername", "CallerName"],
    //"FirstName"   - ["fname", "fName", "firstname", "FirstName"],
    //"LastName"    - ["lname", "lastname", "LastName"],
    //"CallerPhone" =- ["CallerPhone"]
    
}
}
