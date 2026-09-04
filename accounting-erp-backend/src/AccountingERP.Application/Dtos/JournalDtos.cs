namespace AccountingERP.Application.Dtos;

public sealed record JournalEntryLineResponse(
    int LineNumber,
    int AccountId,
    string AccountCode,
    string AccountName,
    decimal Debit,
    decimal Credit,
    string? Description,
    int? CustomerId,
    int? SupplierId);

public sealed record JournalEntryResponse(
    int JournalEntryId,
    string EntryNumber,
    DateOnly EntryDate,
    string Description,
    string SourceType,
    int? SourceId,
    bool IsReversal,
    int? ReversesJournalEntryId,
    decimal TotalDebit,
    decimal TotalCredit,
    string CreatedBy,
    DateTime CreatedAtUtc,
    IReadOnlyList<JournalEntryLineResponse> Lines)
{
    /// <summary>Always true for a persisted entry (CK_JournalEntry_Balanced), echoed so callers can assert it.</summary>
    public bool IsBalanced => TotalDebit == TotalCredit;
}

// --- Manual journal entry (PLAN §7 bonus) ---------------------------------

public sealed record CreateJournalEntryRequest(
    DateOnly EntryDate,
    string Description,
    IReadOnlyList<CreateJournalEntryLineRequest> Lines);

public sealed record CreateJournalEntryLineRequest(
    int AccountId,
    decimal Debit,
    decimal Credit,
    string? Description);

public sealed record JournalEntryQuery(
    DateOnly? FromDate,
    DateOnly? ToDate,
    byte? SourceType,
    int? AccountId,
    int Page = 1,
    int PageSize = 50);

// --- Repository insert contract -----------------------------------------------

public sealed record JournalEntryHeaderInsert(
    string EntryNumber,
    DateOnly EntryDate,
    string Description,
    byte SourceType,
    int? SourceId,
    bool IsReversal,
    int? ReversesJournalEntryId,
    decimal TotalDebit,
    decimal TotalCredit);
