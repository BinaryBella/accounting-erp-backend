using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Produces("application/json")]
public sealed class SuppliersController : ControllerBase
{
    private readonly ISupplierService _suppliers;

    public SuppliersController(ISupplierService suppliers) => _suppliers = suppliers;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SupplierResponse>), StatusCodes.Status200OK)]
    public Task<PagedResult<SupplierResponse>> List(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => _suppliers.ListAsync(new PartyQuery(search, isActive, page, pageSize));

    [HttpGet("{id:int}", Name = "GetSupplier")]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<SupplierResponse> Get(int id) => _suppliers.GetAsync(id);

    [HttpPost]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupplierResponse>> Create([FromBody] CreateSupplierRequest request)
    {
        var created = await _suppliers.CreateAsync(request);
        return CreatedAtRoute("GetSupplier", new { id = created.SupplierId }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<SupplierResponse> Update(int id, [FromBody] UpdateSupplierRequest request)
        => _suppliers.UpdateAsync(id, request);

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        await _suppliers.DeleteAsync(id);
        return NoContent();
    }
}
