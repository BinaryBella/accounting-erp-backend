using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Infrastructure.Sql;
using Dapper;

namespace AccountingERP.Infrastructure.Repositories;

public sealed class ReportRepository : IReportRepository
{
    private readonly IUnitOfWork _uow;

    public ReportRepository(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<TrialBalanceRow>> TrialBalanceAsync(DateOnly asOfDate) =>
        (await _uow.Connection.QueryAsync<TrialBalanceRow>(new CommandDefinition(
            ReportSql.TrialBalance, new { AsOfDate = asOfDate }, _uow.Transaction))).ToList();

    public async Task<IReadOnlyList<ProfitAndLossQueryRow>> ProfitAndLossAsync(DateOnly fromDate, DateOnly toDate) =>
        (await _uow.Connection.QueryAsync<ProfitAndLossQueryRow>(new CommandDefinition(
            ReportSql.ProfitAndLoss, new { FromDate = fromDate, ToDate = toDate }, _uow.Transaction))).ToList();

    public async Task<GeneralLedgerData?> GeneralLedgerAsync(int accountId, DateOnly fromDate, DateOnly toDate)
    {
        using var grid = await _uow.Connection.QueryMultipleAsync(new CommandDefinition(
            ReportSql.GeneralLedger,
            new { AccountId = accountId, FromDate = fromDate, ToDate = toDate },
            _uow.Transaction));

        var header = await grid.ReadSingleOrDefaultAsync<GeneralLedgerHeaderRow>();
        if (header is null)
            return null;

        var entries = (await grid.ReadAsync<GeneralLedgerEntry>()).ToList();

        return new GeneralLedgerData(
            header.AccountId, header.AccountCode, header.AccountName, header.AccountType,
            header.OpeningBalance, entries);
    }

    public async Task<IReadOnlyList<OutstandingRow>> CustomerOutstandingAsync(int? customerId, DateOnly asOfDate) =>
        (await _uow.Connection.QueryAsync<OutstandingRow>(new CommandDefinition(
            ReportSql.CustomerOutstanding, new { PartyId = customerId, AsOfDate = asOfDate }, _uow.Transaction))).ToList();

    public async Task<IReadOnlyList<OutstandingRow>> SupplierOutstandingAsync(int? supplierId, DateOnly asOfDate) =>
        (await _uow.Connection.QueryAsync<OutstandingRow>(new CommandDefinition(
            ReportSql.SupplierOutstanding, new { PartyId = supplierId, AsOfDate = asOfDate }, _uow.Transaction))).ToList();

    private sealed class GeneralLedgerHeaderRow
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = default!;
        public string AccountName { get; set; } = default!;
        public string AccountType { get; set; } = default!;
        public decimal OpeningBalance { get; set; }
    }
}
