namespace AccountingERP.Application.Dtos;

// --- write contract -------------------------------------------------------

public sealed record CreateSupplierPaymentRequest(
    int SupplierId,
    DateOnly PaymentDate,
    byte PaymentMethodId,
    string? ReferenceNo,
    decimal Amount,
    IReadOnlyList<SupplierBillAllocationRequest> Allocations);

public sealed record SupplierBillAllocationRequest(
    int SupplierBillId,
    decimal AllocatedAmount);

// --- read contract -------------------------------------------------------

public sealed record SupplierPaymentResponse(
    int PaymentId,
    string PaymentNumber,
    int SupplierId,
    string SupplierCode,
    string SupplierName,
    DateOnly PaymentDate,
    byte PaymentMethodId,
    string PaymentMethod,
    string? ReferenceNo,
    decimal Amount,
    string Status,
    int? JournalEntryId,
    DateTime? PostedAtUtc,
    DateTime CreatedAtUtc,
    IReadOnlyList<SupplierPaymentAllocationResponse> Allocations);

public sealed record SupplierPaymentAllocationResponse(
    int PaymentAllocationId,
    int SupplierBillId,
    string BillNumber,
    decimal AllocatedAmount,
    decimal BillGrandTotal,
    decimal BillAmountPaid)
{
    public decimal BillOutstanding => BillGrandTotal - BillAmountPaid;
}

public sealed record SupplierPaymentListItem(
    int PaymentId,
    string PaymentNumber,
    int SupplierId,
    string SupplierName,
    DateOnly PaymentDate,
    string PaymentMethod,
    decimal Amount,
    string Status);

public sealed record SupplierPaymentQuery(
    int? SupplierId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page = 1,
    int PageSize = 50);

public sealed record PostSupplierPaymentResult(
    SupplierPaymentResponse Payment,
    JournalEntryResponse JournalEntry);

public sealed record ReverseSupplierPaymentResult(
    SupplierPaymentResponse Payment,
    JournalEntryResponse JournalEntry);

// --- internal projections ------------------------------------------------

public sealed record SupplierPaymentInsert(
    string PaymentNumber,
    int SupplierId,
    byte PaymentMethodId,
    DateOnly PaymentDate,
    string? ReferenceNo,
    decimal Amount);

/// <summary>A bill row read <c>WITH (UPDLOCK, ROWLOCK)</c> while allocating a payment to it.</summary>
public sealed record BillAllocationTarget(
    int SupplierBillId,
    string BillNumber,
    byte Status,
    int SupplierId,
    DateOnly BillDate,
    decimal GrandTotal,
    decimal AmountPaid)
{
    public decimal Outstanding => GrandTotal - AmountPaid;
}

public sealed record SupplierPaymentReverseInfo(
    int PaymentId,
    string PaymentNumber,
    byte Status,
    int? JournalEntryId,
    IReadOnlyList<SupplierBillAllocationSnapshot> Allocations);

public sealed record SupplierBillAllocationSnapshot(
    int SupplierBillId,
    decimal AllocatedAmount);
