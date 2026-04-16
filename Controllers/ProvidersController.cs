using CadProcessorService.Infrastructure;
using CadProcessorService.Models;
using Microsoft.AspNetCore.Mvc;

namespace CadProcessorService.Controllers;

[ApiController]
[Route("api")]
public class ProvidersController : ControllerBase
{
    private readonly DatabaseExecutor _databaseExecutor;

    public ProvidersController(DatabaseExecutor databaseExecutor)
    {
        _databaseExecutor = databaseExecutor;
    }

    [HttpPost("providers")]
    public IActionResult CreateProvider(DbProviderConfig provider)
    {
        var id = _databaseExecutor.SaveProvider(provider);
        provider.ProviderId = id;
        return Ok(provider);
    }

    [HttpPut("providers")]
    public IActionResult UpdateProvider(DbProviderConfig provider)
    {
        _databaseExecutor.SaveProvider(provider);
        return NoContent();
    }

    [HttpDelete("providers/{id:int}")]
    public IActionResult DeleteProvider(int id)
    {
        _databaseExecutor.DeleteProvider(id);
        return NoContent();
    }

    [HttpGet("providers/{providerId:int}/folders")]
    public IActionResult GetProviderFolders(int providerId)
    {
        return Ok(_databaseExecutor.GetProviderFolders(providerId));
    }

    [HttpPut("providers/{providerId:int}/folders")]
    public IActionResult SaveProviderFolders(int providerId, DbFolderConfig folderConfig)
    {
        folderConfig.ProviderId = providerId;
        _databaseExecutor.SaveProviderFolders(providerId, folderConfig);
        return NoContent();
    }

    [HttpGet("providers/{providerId:int}/procedures")]
    public IActionResult GetProcedures(int providerId)
    {
        return Ok(_databaseExecutor.GetProcedures(providerId));
    }

    [HttpPost("providers/{providerId:int}/procedures")]
    public IActionResult CreateProcedure(int providerId, DbProcedureConfig procedure)
    {
        var id = _databaseExecutor.SaveProcedure(providerId, procedure);
        procedure.ProcedureId = id;
        return Ok(procedure);
    }

    [HttpPut("providers/{providerId:int}/procedures")]
    public IActionResult UpdateProcedure(int providerId, DbProcedureConfig procedure)
    {
        _databaseExecutor.SaveProcedure(providerId, procedure);
        return NoContent();
    }

    [HttpDelete("providers/{providerId:int}/procedures/{procedureId:int}")]
    public IActionResult DeleteProcedure(int providerId, int procedureId)
    {
        _databaseExecutor.DeleteProcedure(procedureId);
        return NoContent();
    }

    [HttpGet("providers/{providerId:int}/field-mappings")]
    public IActionResult GetFieldMappings(int providerId)
    {
        return Ok(_databaseExecutor.GetFieldMappings(providerId));
    }

    [HttpPost("providers/{providerId:int}/field-mappings")]
    public IActionResult CreateFieldMapping(int providerId, DbFieldMapping mapping)
    {
        var id = _databaseExecutor.SaveFieldMapping(providerId, mapping.ProcedureId, mapping);
        mapping.MappingId = id;
        return Ok(mapping);
    }

    [HttpPut("providers/{providerId:int}/field-mappings")]
    public IActionResult UpdateFieldMapping(int providerId, DbFieldMapping mapping)
    {
        _databaseExecutor.SaveFieldMapping(providerId, mapping.ProcedureId, mapping);
        return NoContent();
    }

    [HttpDelete("providers/{providerId:int}/field-mappings/{mappingId:int}")]
    public IActionResult DeleteFieldMapping(int providerId, int mappingId)
    {
        _databaseExecutor.DeleteFieldMapping(mappingId);
        return NoContent();
    }

    [HttpGet("providers/{providerId:int}/rules")]
    public IActionResult GetRules(int providerId)
    {
        return Ok(_databaseExecutor.GetProviderFieldRules(providerId));
    }

    [HttpPost("providers/{providerId:int}/rules")]
    public IActionResult CreateRule(int providerId, DbProviderFieldRule rule)
    {
        var id = _databaseExecutor.SaveProviderFieldRule(providerId, rule);
        rule.RuleId = id;
        return Ok(rule);
    }

    [HttpPut("providers/{providerId:int}/rules")]
    public IActionResult UpdateRule(int providerId, DbProviderFieldRule rule)
    {
        _databaseExecutor.SaveProviderFieldRule(providerId, rule);
        return NoContent();
    }

    [HttpDelete("providers/{providerId:int}/rules/{ruleId:int}")]
    public IActionResult DeleteRule(int providerId, int ruleId)
    {
        _databaseExecutor.DeleteProviderFieldRule(ruleId);
        return NoContent();
    }

    [HttpGet("providers/{providerId:int}/metadata")]
    public IActionResult GetProviderMetadata(int providerId)
    {
        return Ok(_databaseExecutor.GetProviderServiceMetaData(providerId));
    }


[HttpDelete("providers/{providerId:int}/metadata/{metadataid:int}")]
public IActionResult Deletemetadata(int providerId, int metadataid)
{
    _databaseExecutor.DeleteProviderServiceMetaData(metadataid);
    return NoContent();
}







    [HttpPut("providers/{providerId:int}/metadata")]
    public IActionResult SaveProviderMetadata(int providerId, ServiceMetaDataDto metadata)
    {
        _databaseExecutor.SaveProviderServiceMetaData(providerId, metadata);
        return NoContent();
    }

    [HttpGet("providers/{providerId:int}/service-metadata")]
    public IActionResult GetProviderServiceMetaDataRows(int providerId)
    {
        return Ok(_databaseExecutor.GetProviderServiceMetaDataRows(providerId));
    }

    [HttpPost("providers/{providerId:int}/service-metadata")]
    public IActionResult CreateProviderServiceMetaData(int providerId, DbServiceMetaData metadata)
    {
        var id = _databaseExecutor.SaveProviderServiceMetaDataRow(providerId, metadata);
        metadata.CdServiceMetaDataId = id;
        return Ok(metadata);
    }

    [HttpPut("providers/{providerId:int}/service-metadata")]
    public IActionResult UpdateProviderServiceMetaData(int providerId, DbServiceMetaData metadata)
    {
        _databaseExecutor.SaveProviderServiceMetaDataRow(providerId, metadata);
        return Ok(metadata);
    }

    [HttpDelete("providers/{providerId:int}/service-metadata/{id:int}")]
    public IActionResult DeleteProviderServiceMetaData(int providerId, int id)
    {
        _databaseExecutor.DeleteProviderServiceMetaData(id);
        return NoContent();
    }

    [HttpGet("providers/{providerId:int}/retry-settings")]
    public IActionResult GetProviderRetrySettings(int providerId)
    {
        return Ok(_databaseExecutor.GetProviderRetrySettings(providerId));
    }

    [HttpPut("providers/{providerId:int}/retry-settings")]
    public IActionResult SaveProviderRetrySettings(int providerId, DbRetryConfig settings)
    {
        settings.ProviderId = providerId;
        _databaseExecutor.SaveProviderRetrySettings(providerId, settings);
        return NoContent();
    }

    [HttpGet("providers/{providerId:int}/email-settings")]
    public IActionResult GetProviderEmailSettings(int providerId)
    {
        return Ok(_databaseExecutor.GetProviderEmailSettings(providerId));
    }

    [HttpPut("providers/{providerId:int}/email-settings")]
    public IActionResult SaveProviderEmailSettings(int providerId, DbEmailConfig settings)
    {
        settings.ProviderId = providerId;
        _databaseExecutor.SaveProviderEmailSettings(providerId, settings);
        return NoContent();
    }
}
