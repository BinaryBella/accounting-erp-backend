using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface IReportService
{
    Task<TrialBalanceReport> TrialBalanceAsync(DateOnly asOfDate);

    Task<ProfitAndLossReport> ProfitAndLossAsync(DateOnly fromDate, DateOnly toDate);

    Task<GeneralLedgerReport> GeneralLedgerAsync(int accountId, DateOnly fromDate, DateOnly toDate);

    Task<OutstandingReport> CustomerOutstandingAsync(int? customerId, DateOnly asOfDate);

    Task<OutstandingReport> SupplierOutstandingAsync(int? supplierId, DateOnly asOfDate);
}
