namespace AccountingERP.Application.Dtos;

// --- write contract (POST + PUT share the same shape) -----------------------

public sealed record SalesInvoiceWriteRequest(
    int CustomerId,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string? Notes,
    IReadOnlyList<SalesInvoiceLineWriteRequest> Lines);

public sealed record SalesInvoiceLineWriteRequest(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRatePercent,
    int? RevenueAccountId);

// --- read contract ----------------------------------------------------------

public sealed record SalesInvoiceResponse(
    int SalesInvoiceId,
    string InvoiceNumber,
    int CustomerId,
    string CustomerCode,
    string CustomerName,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string Status,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal GrandTotal,
    decimal AmountPaid,
    string? Notes,
    int? JournalEntryId,
    DateTime? PostedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<SalesInvoiceLineResponse> Lines,
    IReadOnlyList<SalesInvoiceAllocationResponse> Allocations)
{
    public decimal OutstandingAmount => GrandTotal - AmountPaid;
}

public sealed record SalesInvoiceLineResponse(
    int SalesInvoiceLineId,
    int LineNumber,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRatePercent,
    decimal LineSubTotal,
    decimal LineDiscount,
    decimal LineTax,
    decimal LineTotal,
    int RevenueAccountId,
    string RevenueAccountCode,
    string RevenueAccountName);

public sealed record SalesInvoiceAllocationResponse(
    int PaymentAllocationId,
    int PaymentId,
    string PaymentNumber,
    DateOnly PaymentDate,
    string PaymentMethod,
    decimal AllocatedAmount);

public sealed record SalesInvoiceListItem(
    int SalesInvoiceId,
    string InvoiceNumber,
    int CustomerId,
    string CustomerName,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string Status,
    decimal GrandTotal,
    decimal AmountPaid)
{
    public decimal OutstandingAmount => GrandTotal - AmountPaid;
}

public sealed record SalesInvoiceQuery(
    int? CustomerId,
    byte? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page = 1,
    int PageSize = 50);

public sealed record PostSalesInvoiceResult(
    SalesInvoiceResponse Invoice,
    JournalEntryResponse JournalEntry);

// --- internal projections (service ⇄ repository) --------------------------

public sealed record SalesInvoiceHeaderInsert(
    string InvoiceNumber,
    int CustomerId,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal GrandTotal,
    string? Notes);

public sealed record SalesInvoiceHeaderUpdate(
    int SalesInvoiceId,
    int CustomerId,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal GrandTotal,
    string? Notes);

public sealed record SalesInvoiceLineComputed(
    int LineNumber,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRatePercent,
    decimal LineSubTotal,
    decimal LineDiscount,
    decimal LineTax,
    decimal LineTotal,
    int RevenueAccountId);

/// <summary>Minimal read of a draft invoice under UPDLOCK, for building its journal.</summary>
public sealed record SalesInvoicePostView(
    int SalesInvoiceId,
    string InvoiceNumber,
    byte Status,
    int CustomerId,
    string CustomerName,
    DateOnly InvoiceDate,
    decimal TaxAmount,
    decimal GrandTotal,
    IReadOnlyList<SalesInvoicePostLine> Lines);

public sealed record SalesInvoicePostLine(
    int RevenueAccountId,
    decimal LineSubTotal,
    decimal LineDiscount);

/// <summary>Status snapshot for edit/delete/post guards.</summary>
public sealed record SalesInvoiceGuard(
    int SalesInvoiceId,
    byte Status,
    int? JournalEntryId,
    decimal AmountPaid,
    bool HasAllocations);
