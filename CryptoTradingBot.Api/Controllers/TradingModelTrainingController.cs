
using Microsoft.AspNetCore.Mvc;
using CryptoTradingBot.Core.Entities;
using CryptoTradingBot.Core.Interfaces;
using CryptoTradingBot.Api.Models;

namespace CryptoTradingBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TradingModelTrainingController : ControllerBase
{
    private readonly IModelTrainingService _modelTrainingService;
    private readonly IModelEvaluator _modelEvaluator;
    private readonly IUnitOfWork _unitOfWork;

    public TradingModelTrainingController(
        IModelTrainingService modelTrainingService,
        IModelEvaluator modelEvaluator,
        IUnitOfWork unitOfWork)
    {
        _modelTrainingService = modelTrainingService;
        _modelEvaluator = modelEvaluator;
        _unitOfWork = unitOfWork;
    }

    [HttpPost("start")]
    public async Task<ActionResult<TrainingResult>> StartTraining(
        [FromBody] StartTradingModelTrainingRequest request)
    {
        try
        {
            var result = await _modelTrainingService.StartTrainingAsync(
                request.ModelConfigurationId, 
                request.DatasetId);
            
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{trainingResultId}/stop")]
    public async Task<ActionResult> StopTraining(Guid trainingResultId)
    {
        var success = await _modelTrainingService.StopTrainingAsync(trainingResultId);
        if (!success)
        {
            return NotFound();
        }
        
        return Ok(new { message = "Training stopped successfully" });
    }

    [HttpPost("{trainingResultId}/resume")]
    public async Task<ActionResult<TrainingResult>> ResumeTraining(Guid trainingResultId)
    {
        try
        {
            var result = await _modelTrainingService.ResumeTrainingAsync(trainingResultId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{trainingResultId}")]
    public async Task<ActionResult<TrainingResult>> GetTrainingResult(Guid trainingResultId)
    {
        var result = await _modelTrainingService.GetTrainingResultAsync(trainingResultId);
        if (result == null)
        {
            return NotFound();
        }
        
        return Ok(result);
    }

    [HttpGet("history/{modelConfigurationId}")]
    public async Task<ActionResult<IEnumerable<TrainingResult>>> GetTrainingHistory(Guid modelConfigurationId)
    {
        var history = await _modelTrainingService.GetTrainingHistoryAsync(modelConfigurationId);
        return Ok(history);
    }

    [HttpGet("{trainingResultId}/performance")]
    public async Task<ActionResult<ModelPerformanceReport>> GetPerformanceReport(Guid trainingResultId)
    {
        var trainingResult = await _unitOfWork.TrainingResults.GetByIdAsync(trainingResultId);
        if (trainingResult == null)
        {
            return NotFound();
        }

        var report = await _modelEvaluator.GeneratePerformanceReportAsync(trainingResult);
        return Ok(report);
    }

    [HttpPost("{trainingResultId}/export")]
    public async Task<ActionResult> ExportModel(Guid trainingResultId, [FromBody] ExportModelRequest request)
    {
        try
        {
            var exportPath = await _modelTrainingService.ExportModelAsync(trainingResultId, request.Format);
            return Ok(new { exportPath });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{trainingResultId}")]
    public async Task<ActionResult> DeleteTrainingResult(Guid trainingResultId)
    {
        var success = await _modelTrainingService.DeleteTrainingResultAsync(trainingResultId);
        if (!success)
        {
            return NotFound();
        }
        
        return NoContent();
    }
}


