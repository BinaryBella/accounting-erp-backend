using AccountingERP.Domain.Enums;

namespace AccountingERP.Application.Dtos;

/// <summary>
/// The in-memory description of a balanced accounting transaction, handed to
/// <c>IJournalService.PostAsync</c> — the only code path that writes to the journal
/// (PLAN §4). Callers resolve concrete <see cref="JournalDraftLine.AccountId"/>s
/// (from account-mapping keys or a document's own line accounts) before building this;
/// no account code literal appears in C#.
/// </summary>
public sealed record JournalDraft
{
    public required JournalSourceType SourceType { get; init; }

    /// <summary>Id of the SalesInvoice / SupplierBill / Payment this entry posts for; null for manual/opening.</summary>
    public int? SourceId { get; init; }

    public required DateOnly EntryDate { get; init; }

    public required string Description { get; init; }

    public bool IsReversal { get; init; }

    public int? ReversesJournalEntryId { get; init; }

    public required IReadOnlyList<JournalDraftLine> Lines { get; init; }
}

public sealed record JournalDraftLine(
    int AccountId,
    decimal Debit,
    decimal Credit,
    string? Description = null,
    int? CustomerId = null,
    int? SupplierId = null)
{
    /// <summary>A debit line, rounded to 2dp away from zero (PLAN §2.1).</summary>
    public static JournalDraftLine ForDebit(int accountId, decimal amount, string? description = null,
        int? customerId = null, int? supplierId = null)
        => new(accountId, decimal.Round(amount, 2, MidpointRounding.AwayFromZero), 0m, description, customerId, supplierId);

    /// <summary>A credit line, rounded to 2dp away from zero (PLAN §2.1).</summary>
    public static JournalDraftLine ForCredit(int accountId, decimal amount, string? description = null,
        int? customerId = null, int? supplierId = null)
        => new(accountId, 0m, decimal.Round(amount, 2, MidpointRounding.AwayFromZero), description, customerId, supplierId);
}
