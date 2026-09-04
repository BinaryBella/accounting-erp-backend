using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.Api.Controllers;

[ApiController]
[Route("api/supplier-bills")]
[Produces("application/json")]
public sealed class SupplierBillsController : ControllerBase
{
    private readonly ISupplierBillService _bills;

    public SupplierBillsController(ISupplierBillService bills) => _bills = bills;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SupplierBillListItem>), StatusCodes.Status200OK)]
    public Task<PagedResult<SupplierBillListItem>> List(
        [FromQuery] int? supplierId,
        [FromQuery] byte? status,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => _bills.ListAsync(new SupplierBillQuery(supplierId, status, fromDate, toDate, page, pageSize));

    [HttpGet("{id:int}", Name = "GetSupplierBill")]
    [ProducesResponseType(typeof(SupplierBillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<SupplierBillResponse> Get(int id) => _bills.GetAsync(id);

    /// <summary>Creates a Draft bill. The server computes every line and header amount.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SupplierBillResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierBillResponse>> Create([FromBody] SupplierBillWriteRequest request)
    {
        var created = await _bills.CreateAsync(request);
        return CreatedAtRoute("GetSupplierBill", new { id = created.SupplierBillId }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SupplierBillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<SupplierBillResponse> Update(int id, [FromBody] SupplierBillWriteRequest request)
        => _bills.UpdateAsync(id, request);

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        await _bills.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Posts the bill: creates the balanced journal entry and returns bill + journal entry.</summary>
    [HttpPost("{id:int}/post")]
    [ProducesResponseType(typeof(PostSupplierBillResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<PostSupplierBillResult> Post(int id) => _bills.PostAsync(id);

    /// <summary>Reverses a posted bill with a mirror journal entry. Body: reversalDate + reason. 409 if payments are allocated.</summary>
    [HttpPost("{id:int}/reverse")]
    [ProducesResponseType(typeof(ReverseSupplierBillResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ReverseSupplierBillResult> Reverse(int id, [FromBody] ReverseRequest request)
        => _bills.ReverseAsync(id, request);

    [HttpGet("{id:int}/journal-entry")]
    [ProducesResponseType(typeof(JournalEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<JournalEntryResponse> JournalEntry(int id) => _bills.GetJournalEntryAsync(id);
}
