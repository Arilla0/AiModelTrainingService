
using Microsoft.AspNetCore.Mvc;
using AiModelTrainingService.Core.Enums;
using AiModelTrainingService.Services.Interfaces;

namespace AiModelTrainingService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelsController : ControllerBase
{
    private readonly IModelTrainerService _modelTrainerService;

    public ModelsController(IModelTrainerService modelTrainerService)
    {
        _modelTrainerService = modelTrainerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllModels()
    {
        var models = await _modelTrainerService.GetAllModelsAsync();
        return Ok(models);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetModel(Guid id)
    {
        var model = await _modelTrainerService.GetModelByIdAsync(id);
        if (model == null)
            return NotFound();

        return Ok(model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateModel([FromBody] CreateModelRequest request)
    {
        var model = await _modelTrainerService.CreateModelAsync(
            request.Name,
            request.Description,
            request.Type,
            request.Configuration,
            request.CreatedBy);

        return CreatedAtAction(nameof(GetModel), new { id = model.Id }, model);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateModel(Guid id, [FromBody] UpdateModelRequest request)
    {
        try
        {
            var model = await _modelTrainerService.UpdateModelAsync(id, request.Name, request.Description, request.Configuration);
            return Ok(model);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteModel(Guid id)
    {
        try
        {
            await _modelTrainerService.DeleteModelAsync(id);
            return NoContent();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/training")]
    public async Task<IActionResult> StartTraining(Guid id, [FromBody] StartTrainingRequest request)
    {
        try
        {
            var job = await _modelTrainerService.StartTrainingAsync(id, request.JobName, request.Parameters, request.Epochs);
            return Ok(job);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id}/training")]
    public async Task<IActionResult> GetModelTrainingJobs(Guid id)
    {
        var jobs = await _modelTrainerService.GetModelTrainingJobsAsync(id);
        return Ok(jobs);
    }
}

public record CreateModelRequest(string Name, string Description, ModelType Type, string Configuration, string CreatedBy);
public record UpdateModelRequest(string Name, string Description, string Configuration);
public record StartTrainingRequest(string JobName, string Parameters, int Epochs);
