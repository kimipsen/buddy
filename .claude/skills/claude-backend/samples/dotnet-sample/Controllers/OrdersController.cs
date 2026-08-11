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
}
