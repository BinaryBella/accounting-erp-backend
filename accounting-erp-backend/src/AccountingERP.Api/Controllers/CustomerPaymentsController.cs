using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.Api.Controllers;

[ApiController]
[Route("api/customer-payments")]
[Produces("application/json")]
public sealed class CustomerPaymentsController : ControllerBase
{
    private readonly ICustomerPaymentService _payments;

    public CustomerPaymentsController(ICustomerPaymentService payments) => _payments = payments;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerPaymentListItem>), StatusCodes.Status200OK)]
    public Task<PagedResult<CustomerPaymentListItem>> List(
        [FromQuery] int? customerId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => _payments.ListAsync(new CustomerPaymentQuery(customerId, fromDate, toDate, page, pageSize));

    [HttpGet("{id:int}", Name = "GetCustomerPayment")]
    [ProducesResponseType(typeof(CustomerPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<CustomerPaymentResponse> Get(int id) => _payments.GetAsync(id);

    /// <summary>Records a receipt against one or more posted invoices and posts it atomically. Returns payment + journal entry.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PostCustomerPaymentResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PostCustomerPaymentResult>> Create([FromBody] CreateCustomerPaymentRequest request)
    {
        var result = await _payments.CreateAsync(request);
        return CreatedAtRoute("GetCustomerPayment", new { id = result.Payment.PaymentId }, result);
    }
}
