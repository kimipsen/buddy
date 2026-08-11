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
        await _store.AppendEventsAsync($"order-{id}", order.GetChanges());
        return Ok(new { id = id.ToString() });
    }
}
