
using Microsoft.AspNetCore.Mvc;
using CryptoTradingBot.Services.Interfaces;

namespace CryptoTradingBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrainingJobsController : ControllerBase
{
    private readonly IModelTrainerService _modelTrainerService;

    public TrainingJobsController(IModelTrainerService modelTrainerService)
    {
        _modelTrainerService = modelTrainerService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTrainingJob(Guid id)
    {
        var job = await _modelTrainerService.GetTrainingJobAsync(id);
        if (job == null)
            return NotFound();

        return Ok(job);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelTraining(Guid id)
    {
        try
        {
            await _modelTrainerService.CancelTrainingAsync(id);
            return NoContent();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id}/metrics")]
    public async Task<IActionResult> GetTrainingMetrics(Guid id)
    {
        var metrics = await _modelTrainerService.GetTrainingMetricsAsync(id);
        return Ok(metrics);
    }
}
