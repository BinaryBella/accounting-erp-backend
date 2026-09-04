using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Produces("application/json")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _customers;

    public CustomersController(ICustomerService customers) => _customers = customers;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerResponse>), StatusCodes.Status200OK)]
    public Task<PagedResult<CustomerResponse>> List(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => _customers.ListAsync(new PartyQuery(search, isActive, page, pageSize));

    [HttpGet("{id:int}", Name = "GetCustomer")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<CustomerResponse> Get(int id) => _customers.GetAsync(id);

    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> Create([FromBody] CreateCustomerRequest request)
    {
        var created = await _customers.CreateAsync(request);
        return CreatedAtRoute("GetCustomer", new { id = created.CustomerId }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<CustomerResponse> Update(int id, [FromBody] UpdateCustomerRequest request)
        => _customers.UpdateAsync(id, request);

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        await _customers.DeleteAsync(id);
        return NoContent();
    }
}
