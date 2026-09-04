using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface IJournalService
{
    /// <summary>
    /// The single write path to dbo.JournalEntry (PLAN §4). Asserts the draft balances
    /// in memory (throws <c>UnbalancedJournalException</c> → 422), allocates the JV number,
    /// inserts header + lines (TVP), then reconciles the header totals from the persisted
    /// lines. MUST be called inside an active unit-of-work transaction opened by the caller.
    /// Returns the new JournalEntryId.
    /// </summary>
    Task<int> PostAsync(JournalDraft draft);

    Task<JournalEntryResponse> GetAsync(int id);

    /// <summary>Read that returns null instead of throwing — for other services echoing the entry they just posted.</summary>
    Task<JournalEntryResponse?> GetOrNullAsync(int id);

    Task<PagedResult<JournalEntryResponse>> ListAsync(JournalEntryQuery query);

    /// <summary>Manual journal entry (depreciation, accruals, corrections). Owns its own transaction.</summary>
    Task<JournalEntryResponse> CreateManualEntryAsync(CreateJournalEntryRequest request);
}
