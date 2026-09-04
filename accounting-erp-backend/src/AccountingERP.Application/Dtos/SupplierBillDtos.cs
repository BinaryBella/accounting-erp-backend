namespace AccountingERP.Application.Dtos;

// --- write contract (POST + PUT) -----------------------------------------

public sealed record SupplierBillWriteRequest(
    int SupplierId,
    DateOnly BillDate,
    DateOnly? DueDate,
    string? Notes,
    IReadOnlyList<SupplierBillLineWriteRequest> Lines);

/// <param name="DebitAccountId">Purchases (Expense) or Inventory (Asset). Null → AccountMapping['SupplierBillDefaultDebit'].</param>
public sealed record SupplierBillLineWriteRequest(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRatePercent,
    int? DebitAccountId);

// --- read contract -----------------------------------------------------

public sealed record SupplierBillResponse(
    int SupplierBillId,
    string BillNumber,
    int SupplierId,
    string SupplierCode,
    string SupplierName,
    DateOnly BillDate,
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
    IReadOnlyList<SupplierBillLineResponse> Lines,
    IReadOnlyList<SupplierBillAllocationResponse> Allocations)
{
    public decimal OutstandingAmount => GrandTotal - AmountPaid;
}

public sealed record SupplierBillLineResponse(
    int SupplierBillLineId,
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
    int DebitAccountId,
    string DebitAccountCode,
    string DebitAccountName);

public sealed record SupplierBillAllocationResponse(
    int PaymentAllocationId,
    int PaymentId,
    string PaymentNumber,
    DateOnly PaymentDate,
    string PaymentMethod,
    decimal AllocatedAmount);

public sealed record SupplierBillListItem(
    int SupplierBillId,
    string BillNumber,
    int SupplierId,
    string SupplierName,
    DateOnly BillDate,
    DateOnly? DueDate,
    string Status,
    decimal GrandTotal,
    decimal AmountPaid)
{
    public decimal OutstandingAmount => GrandTotal - AmountPaid;
}

public sealed record SupplierBillQuery(
    int? SupplierId,
    byte? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page = 1,
    int PageSize = 50);

public sealed record PostSupplierBillResult(
    SupplierBillResponse Bill,
    JournalEntryResponse JournalEntry);

// --- internal projections (service ⇄ repository) --------------------------

public sealed record SupplierBillHeaderInsert(
    string BillNumber,
    int SupplierId,
    DateOnly BillDate,
    DateOnly? DueDate,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal GrandTotal,
    string? Notes);

public sealed record SupplierBillHeaderUpdate(
    int SupplierBillId,
    int SupplierId,
    DateOnly BillDate,
    DateOnly? DueDate,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal GrandTotal,
    string? Notes);

public sealed record SupplierBillLineComputed(
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
    int DebitAccountId);

public sealed record SupplierBillPostView(
    int SupplierBillId,
    string BillNumber,
    byte Status,
    int SupplierId,
    string SupplierName,
    DateOnly BillDate,
    decimal TaxAmount,
    decimal GrandTotal,
    IReadOnlyList<SupplierBillPostLine> Lines);

public sealed record SupplierBillPostLine(
    int DebitAccountId,
    decimal LineSubTotal,
    decimal LineDiscount);

public sealed record SupplierBillGuard(
    int SupplierBillId,
    byte Status,
    int? JournalEntryId,
    decimal AmountPaid,
    bool HasAllocations);
