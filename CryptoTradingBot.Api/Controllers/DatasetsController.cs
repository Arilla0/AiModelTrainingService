
using Microsoft.AspNetCore.Mvc;
using CryptoTradingBot.Services.Interfaces;

namespace CryptoTradingBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatasetsController : ControllerBase
{
    private readonly IDatasetService _datasetService;

    public DatasetsController(IDatasetService datasetService)
    {
        _datasetService = datasetService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllDatasets()
    {
        var datasets = await _datasetService.GetAllDatasetsAsync();
        return Ok(datasets);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDataset(Guid id)
    {
        var dataset = await _datasetService.GetDatasetByIdAsync(id);
        if (dataset == null)
            return NotFound();

        return Ok(dataset);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDataset([FromBody] CreateDatasetRequest request)
    {
        var dataset = await _datasetService.CreateDatasetAsync(
            request.Name,
            request.Description,
            request.FilePath,
            request.Format,
            request.FileSize,
            request.RecordCount,
            request.CreatedBy);

        return CreatedAtAction(nameof(GetDataset), new { id = dataset.Id }, dataset);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDataset(Guid id, [FromBody] UpdateDatasetRequest request)
    {
        try
        {
            var dataset = await _datasetService.UpdateDatasetAsync(id, request.Name, request.Description);
            return Ok(dataset);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDataset(Guid id)
    {
        try
        {
            await _datasetService.DeleteDatasetAsync(id);
            return NoContent();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpPost("{datasetId}/models/{modelId}")]
    public async Task<IActionResult> AssignDatasetToModel(Guid datasetId, Guid modelId)
    {
        try
        {
            await _datasetService.AssignDatasetToModelAsync(datasetId, modelId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{datasetId}/models/{modelId}")]
    public async Task<IActionResult> RemoveDatasetFromModel(Guid datasetId, Guid modelId)
    {
        try
        {
            await _datasetService.RemoveDatasetFromModelAsync(datasetId, modelId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("models/{modelId}")]
    public async Task<IActionResult> GetModelDatasets(Guid modelId)
    {
        var datasets = await _datasetService.GetModelDatasetsAsync(modelId);
        return Ok(datasets);
    }
}

public record CreateDatasetRequest(string Name, string Description, string FilePath, string Format, long FileSize, int RecordCount, string CreatedBy);
public record UpdateDatasetRequest(string Name, string Description);
