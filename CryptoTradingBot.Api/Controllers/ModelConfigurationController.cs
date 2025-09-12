
using Microsoft.AspNetCore.Mvc;
using CryptoTradingBot.Core.Entities;
using CryptoTradingBot.Core.Interfaces;

namespace CryptoTradingBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelConfigurationController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public ModelConfigurationController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ModelConfiguration>>> GetModelConfigurations()
    {
        var configurations = await _unitOfWork.ModelConfigurations.GetAllAsync();
        return Ok(configurations);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ModelConfiguration>> GetModelConfiguration(Guid id)
    {
        var configuration = await _unitOfWork.ModelConfigurations.GetByIdAsync(id);
        if (configuration == null)
        {
            return NotFound();
        }
        return Ok(configuration);
    }

    [HttpPost]
    public async Task<ActionResult<ModelConfiguration>> CreateModelConfiguration(ModelConfiguration configuration)
    {
        configuration.Id = Guid.NewGuid();
        configuration.CreatedAt = DateTime.UtcNow;
        
        await _unitOfWork.ModelConfigurations.AddAsync(configuration);
        await _unitOfWork.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetModelConfiguration), new { id = configuration.Id }, configuration);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateModelConfiguration(Guid id, ModelConfiguration configuration)
    {
        if (id != configuration.Id)
        {
            return BadRequest();
        }

        var existingConfiguration = await _unitOfWork.ModelConfigurations.GetByIdAsync(id);
        if (existingConfiguration == null)
        {
            return NotFound();
        }

        configuration.LastModifiedAt = DateTime.UtcNow;
        await _unitOfWork.ModelConfigurations.UpdateAsync(configuration);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteModelConfiguration(Guid id)
    {
        var configuration = await _unitOfWork.ModelConfigurations.GetByIdAsync(id);
        if (configuration == null)
        {
            return NotFound();
        }

        await _unitOfWork.ModelConfigurations.DeleteAsync(configuration);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }
}
