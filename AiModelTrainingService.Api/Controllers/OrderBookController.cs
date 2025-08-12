
using Microsoft.AspNetCore.Mvc;
using AiModelTrainingService.Core.Entities;
using AiModelTrainingService.Core.Interfaces;

namespace AiModelTrainingService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderBookController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataLoader _dataLoader;

    public OrderBookController(IUnitOfWork unitOfWork, IDataLoader dataLoader)
    {
        _unitOfWork = unitOfWork;
        _dataLoader = dataLoader;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderBookData>>> GetOrderBookData()
    {
        var orderBookData = await _unitOfWork.OrderBookData.GetAllAsync();
        return Ok(orderBookData);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderBookData>> GetOrderBookData(Guid id)
    {
        var orderBookData = await _unitOfWork.OrderBookData.GetByIdAsync(id);
        if (orderBookData == null)
        {
            return NotFound();
        }
        return Ok(orderBookData);
    }

    [HttpPost]
    public async Task<ActionResult<OrderBookData>> CreateOrderBookData(OrderBookData orderBookData)
    {
        orderBookData.Id = Guid.NewGuid();
        orderBookData.CreatedAt = DateTime.UtcNow;
        
        await _unitOfWork.OrderBookData.AddAsync(orderBookData);
        await _unitOfWork.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetOrderBookData), new { id = orderBookData.Id }, orderBookData);
    }

    [HttpGet("symbol/{symbol}")]
    public async Task<ActionResult<IEnumerable<OrderBookData>>> GetOrderBookDataBySymbol(
        string symbol, 
        [FromQuery] DateTime? startDate = null, 
        [FromQuery] DateTime? endDate = null)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-1);
        var end = endDate ?? DateTime.UtcNow;
        
        var data = await _dataLoader.LoadOrderBookDataBySymbolAsync(symbol, start, end);
        return Ok(data);
    }
}
