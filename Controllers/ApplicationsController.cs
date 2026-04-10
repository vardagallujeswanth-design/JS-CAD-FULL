using CadProcessorService.Infrastructure;
using CadProcessorService.Models;
using Microsoft.AspNetCore.Mvc;

namespace CadProcessorService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly DatabaseExecutor _databaseExecutor;

    public ApplicationsController(DatabaseExecutor databaseExecutor)
    {
        _databaseExecutor = databaseExecutor;
    }

    [HttpGet]
    public IActionResult GetApplications()
    {
        return Ok(_databaseExecutor.GetApplications());
    }

    [HttpPost]
    public IActionResult CreateApplication(DbApplication application)
    {
        var id = _databaseExecutor.SaveApplication(application);
        application.ApplicationId = id;
        return Ok(application);
    }

    [HttpPut]
    public IActionResult UpdateApplication(DbApplication application)
    {
        _databaseExecutor.SaveApplication(application);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public IActionResult DeleteApplication(int id)
    {
        _databaseExecutor.DeleteApplication(id);
        return NoContent();
    }

    [HttpGet("{applicationId:int}/settings")]
    public IActionResult GetSettings(int applicationId)
    {
        return Ok(_databaseExecutor.GetApplicationSettings(applicationId));
    }

    [HttpPut("{applicationId:int}/settings")]
    public IActionResult SaveSettings(int applicationId, DbApplicationSettings settings)
    {
        settings.ApplicationId = applicationId;
        _databaseExecutor.SaveApplicationSettings(settings);
        return NoContent();
    }

    [HttpGet("{applicationId:int}/retry-settings")]
    public IActionResult GetRetrySettings(int applicationId)
    {
        return Ok(_databaseExecutor.GetRetrySettings(applicationId));
    }

    [HttpPut("{applicationId:int}/retry-settings")]
    public IActionResult SaveRetrySettings(int applicationId, DbRetryConfig settings)
    {
        _databaseExecutor.SaveRetrySettings(applicationId, settings);
        return NoContent();
    }

    [HttpGet("{applicationId:int}/email-settings")]
    public IActionResult GetEmailSettings(int applicationId)
    {
        return Ok(_databaseExecutor.GetEmailSettings(applicationId));
    }

    [HttpPut("{applicationId:int}/email-settings")]
    public IActionResult SaveEmailSettings(int applicationId, DbEmailConfig settings)
    {
        _databaseExecutor.SaveEmailSettings(applicationId, settings);
        return NoContent();
    }

    [HttpGet("{applicationId:int}/metadata")]
    public IActionResult GetMetadata(int applicationId)
    {
        return Ok(_databaseExecutor.GetServiceMetaData(applicationId));
    }

    [HttpPut("{applicationId:int}/metadata")]
    public IActionResult SaveMetadata(int applicationId, ServiceMetaDataDto metadata)
    {
        _databaseExecutor.SaveServiceMetaData(applicationId, metadata);
        return NoContent();
    }

    [HttpGet("{applicationId:int}/providers")]
    public IActionResult GetProviders(int applicationId)
    {
        return Ok(_databaseExecutor.GetProviders(applicationId));
    }
}
