using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.Api.Controllers;

[ApiController]
[Route("api/journal-entries")]
[Produces("application/json")]
public sealed class JournalEntriesController : ControllerBase
{
    private readonly IJournalService _journal;

    public JournalEntriesController(IJournalService journal) => _journal = journal;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<JournalEntryResponse>), StatusCodes.Status200OK)]
    public Task<PagedResult<JournalEntryResponse>> List(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] byte? sourceType,
        [FromQuery] int? accountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => _journal.ListAsync(new JournalEntryQuery(fromDate, toDate, sourceType, accountId, page, pageSize));

    [HttpGet("{id:int}", Name = "GetJournalEntry")]
    [ProducesResponseType(typeof(JournalEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<JournalEntryResponse> Get(int id) => _journal.GetAsync(id);

    /// <summary>Manual journal entry — must have ≥ 2 lines and balance (unbalanced → 422).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(JournalEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JournalEntryResponse>> CreateManual([FromBody] CreateJournalEntryRequest request)
    {
        var created = await _journal.CreateManualEntryAsync(request);
        return CreatedAtRoute("GetJournalEntry", new { id = created.JournalEntryId }, created);
    }

    /// <summary>Reverses a Manual or Opening journal entry with a mirror entry. Document-sourced entries must be reversed via their own endpoint (409).</summary>
    [HttpPost("{id:int}/reverse")]
    [ProducesResponseType(typeof(JournalEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<JournalEntryResponse> Reverse(int id, [FromBody] ReverseRequest request)
        => _journal.ReverseManualAsync(id, request);
}
