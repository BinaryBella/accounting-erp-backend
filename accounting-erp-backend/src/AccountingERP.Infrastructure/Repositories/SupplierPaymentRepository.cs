using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Domain.Enums;
using AccountingERP.Infrastructure.Sql;
using Dapper;

namespace AccountingERP.Infrastructure.Repositories;

public sealed class SupplierPaymentRepository : ISupplierPaymentRepository
{
    private readonly IUnitOfWork _uow;

    public SupplierPaymentRepository(IUnitOfWork uow) => _uow = uow;

    public Task<int> InsertAsync(SupplierPaymentInsert p) =>
        _uow.Connection.QuerySingleAsync<int>(new CommandDefinition(PaymentSql.InsertSupplierPayment, new
        {
            p.PaymentNumber,
            p.PaymentDate,
            p.SupplierId,
            p.PaymentMethodId,
            p.ReferenceNo,
            p.Amount
        }, _uow.Transaction));

    public Task InsertAllocationAsync(int paymentId, int supplierBillId, decimal allocatedAmount) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(PaymentSql.InsertBillAllocation, new
        {
            PaymentId = paymentId,
            SupplierBillId = supplierBillId,
            AllocatedAmount = allocatedAmount
        }, _uow.Transaction));

    public Task<BillAllocationTarget?> LockBillAsync(int supplierBillId) =>
        _uow.Connection.QuerySingleOrDefaultAsync<BillAllocationTarget>(new CommandDefinition(
            PaymentSql.LockBill, new { SupplierBillId = supplierBillId }, _uow.Transaction));

    public Task AddBillPaidAmountAsync(int supplierBillId, decimal delta) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(PaymentSql.AddBillPaidAmount, new
        {
            SupplierBillId = supplierBillId,
            Delta = delta
        }, _uow.Transaction));

    public Task MarkPostedAsync(int paymentId, int journalEntryId) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(
            PaymentSql.MarkPosted, new { PaymentId = paymentId, JournalEntryId = journalEntryId }, _uow.Transaction));

    public Task MarkReversedAsync(int paymentId) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(
            PaymentSql.MarkReversed, new { PaymentId = paymentId }, _uow.Transaction));

    public async Task<SupplierPaymentReverseInfo?> GetForReverseAsync(int paymentId)
    {
        using var grid = await _uow.Connection.QueryMultipleAsync(new CommandDefinition(
            PaymentSql.GetSupplierPaymentForReverse, new { PaymentId = paymentId }, _uow.Transaction));

        var header = await grid.ReadSingleOrDefaultAsync<PaymentReverseHeaderRow>();
        if (header is null)
            return null;

        var allocations = (await grid.ReadAsync<SupplierBillAllocationSnapshot>()).ToList();
        return new SupplierPaymentReverseInfo(
            header.PaymentId, header.PaymentNumber, header.Status, header.JournalEntryId, allocations);
    }

    public async Task<SupplierPaymentResponse?> GetByIdAsync(int paymentId)
    {
        using var grid = await _uow.Connection.QueryMultipleAsync(new CommandDefinition(
            PaymentSql.GetSupplierPaymentById, new { PaymentId = paymentId }, _uow.Transaction));

        var header = await grid.ReadSingleOrDefaultAsync<SupplierPaymentHeaderRow>();
        if (header is null)
            return null;

        var allocations = (await grid.ReadAsync<SupplierPaymentAllocationResponse>()).ToList();

        return new SupplierPaymentResponse(
            header.PaymentId, header.PaymentNumber, header.SupplierId, header.SupplierCode, header.SupplierName,
            header.PaymentDate, header.PaymentMethodId, header.PaymentMethod, header.ReferenceNo,
            header.Amount, ((DocumentStatus)header.Status).ToString(),
            header.JournalEntryId, header.PostedAtUtc, header.CreatedAtUtc, allocations);
    }

    public async Task<PagedResult<SupplierPaymentListItem>> ListAsync(SupplierPaymentQuery query)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var parameters = new { query.SupplierId, query.FromDate, query.ToDate, page.Skip, page.Take };

        using var grid = await _uow.Connection.QueryMultipleAsync(
            new CommandDefinition(PaymentSql.ListSupplierPayments, parameters, _uow.Transaction));

        var rows = (await grid.ReadAsync<SupplierPaymentListRow>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        var items = rows
            .Select(r => new SupplierPaymentListItem(
                r.PaymentId, r.PaymentNumber, r.SupplierId, r.SupplierName,
                r.PaymentDate, r.PaymentMethod, r.Amount, ((DocumentStatus)r.Status).ToString()))
            .ToList();

        return new PagedResult<SupplierPaymentListItem>(items, page.Page, page.PageSize, total);
    }

    private sealed class SupplierPaymentHeaderRow
    {
        public int PaymentId { get; set; }
        public string PaymentNumber { get; set; } = default!;
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; } = default!;
        public string SupplierName { get; set; } = default!;
        public DateOnly PaymentDate { get; set; }
        public byte PaymentMethodId { get; set; }
        public string PaymentMethod { get; set; } = default!;
        public string? ReferenceNo { get; set; }
        public decimal Amount { get; set; }
        public byte Status { get; set; }
        public int? JournalEntryId { get; set; }
        public DateTime? PostedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    private sealed class SupplierPaymentListRow
    {
        public int PaymentId { get; set; }
        public string PaymentNumber { get; set; } = default!;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = default!;
        public DateOnly PaymentDate { get; set; }
        public string PaymentMethod { get; set; } = default!;
        public decimal Amount { get; set; }
        public byte Status { get; set; }
    }

    private sealed class PaymentReverseHeaderRow
    {
        public int PaymentId { get; set; }
        public string PaymentNumber { get; set; } = default!;
        public byte Status { get; set; }
        public int? JournalEntryId { get; set; }
    }
}
