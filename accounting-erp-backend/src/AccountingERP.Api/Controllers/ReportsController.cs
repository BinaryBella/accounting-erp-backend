using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AccountingERP.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    /// <summary>Account-wise debit/credit balances as of a date. Response carries <c>isBalanced</c> (demo §10 step 9).</summary>
    [HttpGet("trial-balance")]
    [ProducesResponseType(typeof(TrialBalanceReport), StatusCodes.Status200OK)]
    public Task<TrialBalanceReport> TrialBalance([FromQuery] DateOnly? asOfDate)
        => _reports.TrialBalanceAsync(asOfDate ?? Today);

    /// <summary>Revenue, Expenses and Net Profit for a period. Sections are driven by AccountType, not account codes.</summary>
    [HttpGet("profit-and-loss")]
    [ProducesResponseType(typeof(ProfitAndLossReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ProfitAndLossReport> ProfitAndLoss([FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate)
        => _reports.ProfitAndLossAsync(
            fromDate ?? throw Missing(nameof(fromDate)),
            toDate ?? throw Missing(nameof(toDate)));

    /// <summary>One account's movements over a date range, with opening and running balance.</summary>
    [HttpGet("general-ledger")]
    [ProducesResponseType(typeof(GeneralLedgerReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<GeneralLedgerReport> GeneralLedger(
        [FromQuery] int accountId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate)
        => _reports.GeneralLedgerAsync(accountId, fromDate ?? new DateOnly(2000, 1, 1), toDate ?? Today);

    /// <summary>Open sales invoices with ageing buckets. Balance is derived from payment allocations.</summary>
    [HttpGet("customer-outstanding")]
    [ProducesResponseType(typeof(OutstandingReport), StatusCodes.Status200OK)]
    public Task<OutstandingReport> CustomerOutstanding([FromQuery] int? customerId, [FromQuery] DateOnly? asOfDate)
        => _reports.CustomerOutstandingAsync(customerId, asOfDate ?? Today);

    /// <summary>Open supplier bills with ageing buckets. Balance is derived from payment allocations.</summary>
    [HttpGet("supplier-outstanding")]
    [ProducesResponseType(typeof(OutstandingReport), StatusCodes.Status200OK)]
    public Task<OutstandingReport> SupplierOutstanding([FromQuery] int? supplierId, [FromQuery] DateOnly? asOfDate)
        => _reports.SupplierOutstandingAsync(supplierId, asOfDate ?? Today);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static ValidationException Missing(string field) => new(field, $"{field} is required.");
}
