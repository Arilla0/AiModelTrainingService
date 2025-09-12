
using Microsoft.AspNetCore.Mvc;
using CryptoTradingBot.Core.Entities;
using CryptoTradingBot.Core.Interfaces;
using CryptoTradingBot.Api.Models;

namespace CryptoTradingBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeatureEngineeringController : ControllerBase
{
    private readonly IFeatureEngineering _featureEngineering;
    private readonly IUnitOfWork _unitOfWork;

    public FeatureEngineeringController(IFeatureEngineering featureEngineering, IUnitOfWork unitOfWork)
    {
        _featureEngineering = featureEngineering;
        _unitOfWork = unitOfWork;
    }

    [HttpGet("available-features")]
    public async Task<ActionResult<IEnumerable<string>>> GetAvailableFeatures()
    {
        var features = await _featureEngineering.GetAvailableFeaturesAsync();
        return Ok(features);
    }

    [HttpPost("extract")]
    public async Task<ActionResult<IEnumerable<TrainingData>>> ExtractFeatures(
        [FromBody] ExtractFeaturesRequest request)
    {
        var orderBookData = await _unitOfWork.OrderBookData.FindAsync(
            obd => request.OrderBookDataIds.Contains(obd.Id));
        
        var configuration = await _unitOfWork.ModelConfigurations.GetByIdAsync(request.ModelConfigurationId);
        if (configuration == null)
        {
            return BadRequest("Model configuration not found");
        }

        var trainingData = await _featureEngineering.ExtractFeaturesAsync(orderBookData, configuration);
        
        // Save training data
        foreach (var data in trainingData)
        {
            await _unitOfWork.TrainingData.AddAsync(data);
        }
        await _unitOfWork.SaveChangesAsync();

        return Ok(trainingData);
    }

    [HttpPost("validate/{modelConfigurationId}")]
    public async Task<ActionResult<FeatureExtractionResult>> ValidateFeatureExtraction(Guid modelConfigurationId)
    {
        var configuration = await _unitOfWork.ModelConfigurations.GetByIdAsync(modelConfigurationId);
        if (configuration == null)
        {
            return NotFound("Model configuration not found");
        }

        var result = await _featureEngineering.ValidateFeatureExtractionAsync(configuration);
        return Ok(result);
    }

    [HttpPost("importance")]
    public async Task<ActionResult<FeatureImportanceResult>> CalculateFeatureImportance(
        [FromBody] FeatureImportanceRequest request)
    {
        var trainingData = await _unitOfWork.TrainingData.FindAsync(
            td => request.TrainingDataIds.Contains(td.Id));

        var result = await _featureEngineering.CalculateFeatureImportanceAsync(trainingData);
        return Ok(result);
    }

    [HttpPost("select")]
    public async Task<ActionResult<IEnumerable<TrainingData>>> ApplyFeatureSelection(
        [FromBody] FeatureSelectionRequest request)
    {
        var trainingData = await _unitOfWork.TrainingData.FindAsync(
            td => request.TrainingDataIds.Contains(td.Id));

        var result = await _featureEngineering.ApplyFeatureSelectionAsync(trainingData, request.SelectedFeatures);
        return Ok(result);
    }
}


