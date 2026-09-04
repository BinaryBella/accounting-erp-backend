namespace AccountingERP.Application.Exceptions;

/// <summary>
/// A journal draft's debits do not equal its credits (or the total is not positive).
/// Thrown by JournalService before anything touches the database — the first of the
/// three lines of defence on double-entry (PLAN §2.9). Mapped to HTTP 422.
/// </summary>
public sealed class UnbalancedJournalException : AppException
{
    public decimal TotalDebit { get; }

    public decimal TotalCredit { get; }

    public UnbalancedJournalException(decimal totalDebit, decimal totalCredit)
        : base($"Journal entry does not balance: debits {totalDebit:0.00} ≠ credits {totalCredit:0.00}.")
    {
        TotalDebit = totalDebit;
        TotalCredit = totalCredit;
    }
}
