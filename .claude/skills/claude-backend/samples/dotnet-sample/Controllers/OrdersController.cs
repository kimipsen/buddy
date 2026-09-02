using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;
using Project.Domain.Orders;
using Project.Infrastructure.EventSourcing;

namespace Project.Web.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IEventStore _store;

    public OrdersController(IEventStore store) => _store = store;

    [HttpPost]
    public async Task<IActionResult> Create()
    {
        var id = OrderId.New();
        var order = Order.Create(id);
        // Persist the case records, not the union values: the store derives its event_type
        // discriminator from GetType().Name, and a union is a value type whose boxed name is
        // always "OrderEvent".
        await _store.AppendEventsAsync($"order-{id}", order.GetChanges().Select(e => e.Payload));
        return Ok(new { id = id.ToString() });
    }

    [HttpPost("{id}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddItemRequest request)
    {
        var orderId = OrderId.From(id);
        var order = new Order();
        order.LoadsFrom(await _store.LoadEventsAsync($"order-{orderId}"));

        // AddItem returns a Result instead of throwing for the expected "already completed"
        // business-rule failure, so it's mapped to a status code here rather than caught.
        var result = order.AddItem(request.ItemId, request.Quantity);
        if (!result.IsSuccess) return Conflict(new { error = result.Error });

        await _store.AppendEventsAsync($"order-{orderId}", order.GetChanges().Select(e => e.Payload));
        return NoContent();
    }
}

public record AddItemRequest(Guid ItemId, [property: Required] int Quantity);
