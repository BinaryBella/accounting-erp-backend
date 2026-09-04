namespace AccountingERP.Application.Dtos;

/// <summary>Body for every <c>POST /{id}/reverse</c>. Reason is mandatory (PLAN §5).</summary>
public sealed record ReverseRequest(
    DateOnly ReversalDate,
    string Reason);

public sealed record ReverseSalesInvoiceResult(
    SalesInvoiceResponse Invoice,
    JournalEntryResponse JournalEntry);

public sealed record ReverseSupplierBillResult(
    SupplierBillResponse Bill,
    JournalEntryResponse JournalEntry);

public sealed record ReverseCustomerPaymentResult(
    CustomerPaymentResponse Payment,
    JournalEntryResponse JournalEntry);

// --- internal projections ------------------------------------------------

/// <summary>The original entry and its lines, read so a mirrored (debit/credit-swapped) entry can be posted.</summary>
public sealed record JournalEntryReverseInfo(
    int JournalEntryId,
    string EntryNumber,
    byte SourceType,
    int? SourceId,
    bool IsReversal,
    IReadOnlyList<JournalDraftLine> Lines);

public sealed record CustomerPaymentReverseInfo(
    int PaymentId,
    string PaymentNumber,
    byte Status,
    int? JournalEntryId,
    IReadOnlyList<PaymentAllocationSnapshot> Allocations);

public sealed record PaymentAllocationSnapshot(
    int SalesInvoiceId,
    decimal AllocatedAmount);
