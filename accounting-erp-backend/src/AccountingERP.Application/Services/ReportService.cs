using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Application.Exceptions;

namespace AccountingERP.Application.Services;

public sealed class ReportService : IReportService
{
    private readonly IReportRepository _reports;

    public ReportService(IReportRepository reports) => _reports = reports;

    public async Task<TrialBalanceReport> TrialBalanceAsync(DateOnly asOfDate)
    {
        var rows = await _reports.TrialBalanceAsync(asOfDate);
        return new TrialBalanceReport(
            asOfDate,
            rows,
            TotalDebitBalance: rows.Sum(r => r.DebitBalance),
            TotalCreditBalance: rows.Sum(r => r.CreditBalance));
    }

    public async Task<ProfitAndLossReport> ProfitAndLossAsync(DateOnly fromDate, DateOnly toDate)
    {
        EnsureRange(fromDate, toDate);

        var rows = await _reports.ProfitAndLossAsync(fromDate, toDate);

        var revenue = rows.Where(r => r.Section == "Revenue")
            .Select(r => new ProfitAndLossLine(r.AccountCode, r.AccountName, r.Amount))
            .ToList();
        var expenses = rows.Where(r => r.Section == "Expense")
            .Select(r => new ProfitAndLossLine(r.AccountCode, r.AccountName, r.Amount))
            .ToList();

        var totalRevenue = revenue.Sum(r => r.Amount);
        var totalExpenses = expenses.Sum(r => r.Amount);

        return new ProfitAndLossReport(
            fromDate, toDate,
            revenue, totalRevenue,
            expenses, totalExpenses,
            NetProfit: totalRevenue - totalExpenses);
    }

    public async Task<GeneralLedgerReport> GeneralLedgerAsync(int accountId, DateOnly fromDate, DateOnly toDate)
    {
        EnsureRange(fromDate, toDate);

        var data = await _reports.GeneralLedgerAsync(accountId, fromDate, toDate)
                   ?? throw new NotFoundException("Account", accountId);

        var totalDebit = data.Entries.Sum(e => e.Debit);
        var totalCredit = data.Entries.Sum(e => e.Credit);
        var closingBalance = data.Entries.Count > 0 ? data.Entries[^1].RunningBalance : data.OpeningBalance;

        return new GeneralLedgerReport(
            data.AccountId, data.AccountCode, data.AccountName, data.AccountType,
            fromDate, toDate,
            data.OpeningBalance, closingBalance,
            totalDebit, totalCredit,
            data.Entries);
    }

    public async Task<OutstandingReport> CustomerOutstandingAsync(int? customerId, DateOnly asOfDate)
    {
        var rows = await _reports.CustomerOutstandingAsync(customerId, asOfDate);
        return new OutstandingReport(asOfDate, rows, rows.Sum(r => r.Outstanding));
    }

    public async Task<OutstandingReport> SupplierOutstandingAsync(int? supplierId, DateOnly asOfDate)
    {
        var rows = await _reports.SupplierOutstandingAsync(supplierId, asOfDate);
        return new OutstandingReport(asOfDate, rows, rows.Sum(r => r.Outstanding));
    }

    private static void EnsureRange(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate < fromDate)
            throw new ValidationException("toDate", "toDate cannot be earlier than fromDate.");
    }
}
