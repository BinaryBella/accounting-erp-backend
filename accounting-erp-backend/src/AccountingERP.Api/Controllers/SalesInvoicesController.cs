using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.Api.Controllers;

[ApiController]
[Route("api/sales-invoices")]
[Produces("application/json")]
public sealed class SalesInvoicesController : ControllerBase
{
    private readonly ISalesInvoiceService _invoices;

    public SalesInvoicesController(ISalesInvoiceService invoices) => _invoices = invoices;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SalesInvoiceListItem>), StatusCodes.Status200OK)]
    public Task<PagedResult<SalesInvoiceListItem>> List(
        [FromQuery] int? customerId,
        [FromQuery] byte? status,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => _invoices.ListAsync(new SalesInvoiceQuery(customerId, status, fromDate, toDate, page, pageSize));

    [HttpGet("{id:int}", Name = "GetSalesInvoice")]
    [ProducesResponseType(typeof(SalesInvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<SalesInvoiceResponse> Get(int id) => _invoices.GetAsync(id);

    /// <summary>Creates a Draft invoice. The server computes every line and header amount.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SalesInvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalesInvoiceResponse>> Create([FromBody] SalesInvoiceWriteRequest request)
    {
        var created = await _invoices.CreateAsync(request);
        return CreatedAtRoute("GetSalesInvoice", new { id = created.SalesInvoiceId }, created);
    }

    /// <summary>Edits a Draft invoice. A posted invoice returns 409 — reverse it instead.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SalesInvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<SalesInvoiceResponse> Update(int id, [FromBody] SalesInvoiceWriteRequest request)
        => _invoices.UpdateAsync(id, request);

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        await _invoices.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Posts the invoice: creates the balanced journal entry and returns invoice + journal entry.</summary>
    [HttpPost("{id:int}/post")]
    [ProducesResponseType(typeof(PostSalesInvoiceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<PostSalesInvoiceResult> Post(int id) => _invoices.PostAsync(id);

    [HttpGet("{id:int}/journal-entry")]
    [ProducesResponseType(typeof(JournalEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<JournalEntryResponse> JournalEntry(int id) => _invoices.GetJournalEntryAsync(id);
}
