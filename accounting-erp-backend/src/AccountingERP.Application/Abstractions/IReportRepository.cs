using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface IReportRepository
{
    Task<IReadOnlyList<TrialBalanceRow>> TrialBalanceAsync(DateOnly asOfDate);

    Task<IReadOnlyList<ProfitAndLossQueryRow>> ProfitAndLossAsync(DateOnly fromDate, DateOnly toDate);

    /// <summary>Null if the account does not exist.</summary>
    Task<GeneralLedgerData?> GeneralLedgerAsync(int accountId, DateOnly fromDate, DateOnly toDate);

    Task<IReadOnlyList<OutstandingRow>> CustomerOutstandingAsync(int? customerId, DateOnly asOfDate);

    Task<IReadOnlyList<OutstandingRow>> SupplierOutstandingAsync(int? supplierId, DateOnly asOfDate);
}
