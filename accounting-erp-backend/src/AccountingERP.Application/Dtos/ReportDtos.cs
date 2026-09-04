namespace AccountingERP.Application.Dtos;

// ===================== Trial Balance =====================

public sealed record TrialBalanceRow(
    string AccountCode,
    string AccountName,
    string AccountType,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal DebitBalance,
    decimal CreditBalance);

public sealed record TrialBalanceReport(
    DateOnly AsOfDate,
    IReadOnlyList<TrialBalanceRow> Rows,
    decimal TotalDebitBalance,
    decimal TotalCreditBalance)
{
    /// <summary>Demo §10 step 9: Total Debit = Total Credit.</summary>
    public bool IsBalanced => TotalDebitBalance == TotalCreditBalance;
}

// ===================== Profit & Loss =====================

public sealed record ProfitAndLossLine(
    string AccountCode,
    string AccountName,
    decimal Amount);

public sealed record ProfitAndLossReport(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<ProfitAndLossLine> Revenue,
    decimal TotalRevenue,
    IReadOnlyList<ProfitAndLossLine> Expenses,
    decimal TotalExpenses,
    decimal NetProfit);

/// <summary>Raw P&amp;L row from SQL — Section is "Revenue" or "Expense".</summary>
public sealed record ProfitAndLossQueryRow(
    string Section,
    string AccountCode,
    string AccountName,
    decimal Amount);

// ===================== General Ledger =====================

public sealed record GeneralLedgerEntry(
    DateOnly EntryDate,
    string EntryNumber,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance);

public sealed record GeneralLedgerReport(
    int AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalDebit,
    decimal TotalCredit,
    IReadOnlyList<GeneralLedgerEntry> Entries);

public sealed record GeneralLedgerData(
    int AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    decimal OpeningBalance,
    IReadOnlyList<GeneralLedgerEntry> Entries);

// ===================== Customer / Supplier Outstanding =====================

public sealed record OutstandingRow(
    string PartyCode,
    string PartyName,
    string DocumentNumber,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    decimal GrandTotal,
    decimal AmountPaid,
    decimal Outstanding,
    string AgeingBucket);

public sealed record OutstandingReport(
    DateOnly AsOfDate,
    IReadOnlyList<OutstandingRow> Rows,
    decimal TotalOutstanding);
