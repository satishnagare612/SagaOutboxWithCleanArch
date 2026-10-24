using Microsoft.AspNetCore.Mvc;
using SagaOutBoxCleanArch.Api.Requests;
using SagaOutBoxCleanArch.Application.Orders;

namespace SagaOutBoxCleanArch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrderController(IOrderService order) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var orderDto = await order.CreateAsync(
            new CreateOrderInput(request.ProductId, request.Quantity, request.UnitPrice), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = orderDto.Id }, orderDto);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var orderDto = await order.GetAsync(id, cancellationToken);
        return orderDto is null ? NotFound() : Ok(orderDto);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await order.GetRecentAsync(cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var orderDto = await order.CancelAsync(id, cancellationToken);
        return orderDto is null ? NotFound() : Ok(orderDto);
    }
}
