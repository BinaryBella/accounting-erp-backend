using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.Api.Controllers;

[ApiController]
[Route("api/payment-methods")]
[Produces("application/json")]
public sealed class PaymentMethodsController : ControllerBase
{
    private readonly IPaymentMethodRepository _paymentMethods;

    public PaymentMethodsController(IPaymentMethodRepository paymentMethods) => _paymentMethods = paymentMethods;

    /// <summary>Cash and Bank (and any others seeded), each with its Cash/Bank ledger account.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentMethodResponse>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<PaymentMethodResponse>> List() => _paymentMethods.ListAsync();
}
