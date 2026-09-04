using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.Api.Controllers;

[ApiController]
[Route("api/supplier-payments")]
[Produces("application/json")]
public sealed class SupplierPaymentsController : ControllerBase
{
    private readonly ISupplierPaymentService _payments;

    public SupplierPaymentsController(ISupplierPaymentService payments) => _payments = payments;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SupplierPaymentListItem>), StatusCodes.Status200OK)]
    public Task<PagedResult<SupplierPaymentListItem>> List(
        [FromQuery] int? supplierId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => _payments.ListAsync(new SupplierPaymentQuery(supplierId, fromDate, toDate, page, pageSize));

    [HttpGet("{id:int}", Name = "GetSupplierPayment")]
    [ProducesResponseType(typeof(SupplierPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<SupplierPaymentResponse> Get(int id) => _payments.GetAsync(id);

    /// <summary>Records a payment against one or more posted bills and posts it atomically. Returns payment + journal entry.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PostSupplierPaymentResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PostSupplierPaymentResult>> Create([FromBody] CreateSupplierPaymentRequest request)
    {
        var result = await _payments.CreateAsync(request);
        return CreatedAtRoute("GetSupplierPayment", new { id = result.Payment.PaymentId }, result);
    }

    /// <summary>Reverses a posted payment: mirror journal entry and the allocated bills' outstanding balances restored.</summary>
    [HttpPost("{id:int}/reverse")]
    [ProducesResponseType(typeof(ReverseSupplierPaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ReverseSupplierPaymentResult> Reverse(int id, [FromBody] ReverseRequest request)
        => _payments.ReverseAsync(id, request);
}
