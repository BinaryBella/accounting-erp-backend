using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface IJournalRepository
{
    /// <summary>Inserts the header with the draft's computed totals; CK_JournalEntry_Balanced rejects it if they are unequal or not positive.</summary>
    Task<int> InsertHeaderAsync(JournalEntryHeaderInsert header);

    /// <summary>Bulk-inserts the lines in one round trip via the dbo.JournalEntryLineTvp table-valued parameter. Line numbers assigned 1..n.</summary>
    Task InsertLinesAsync(int journalEntryId, IReadOnlyList<JournalDraftLine> lines);

    /// <summary>
    /// Rewrites TotalDebit/TotalCredit on the header from SUM() of the lines just persisted,
    /// so CK_JournalEntry_Balanced is evaluated against reality, not an asserted number (PLAN §2.9 step 2).
    /// </summary>
    Task ReconcileTotalsAsync(int journalEntryId);

    Task<JournalEntryResponse?> GetByIdAsync(int journalEntryId);

    Task<PagedResult<JournalEntryResponse>> ListAsync(JournalEntryQuery query);

    /// <summary>The original entry's header basics and lines, for building a reversing entry. Null if it does not exist.</summary>
    Task<JournalEntryReverseInfo?> GetForReverseAsync(int journalEntryId);

    /// <summary>True if some entry already has <c>ReversesJournalEntryId = journalEntryId</c>.</summary>
    Task<bool> IsAlreadyReversedAsync(int journalEntryId);
}
