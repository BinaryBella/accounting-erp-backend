using System.Data;
using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Domain.Enums;
using AccountingERP.Infrastructure.Sql;
using Dapper;

namespace AccountingERP.Infrastructure.Repositories;

public sealed class SupplierBillRepository : ISupplierBillRepository
{
    private readonly IUnitOfWork _uow;

    public SupplierBillRepository(IUnitOfWork uow) => _uow = uow;

    public Task<int> InsertHeaderAsync(SupplierBillHeaderInsert h) =>
        _uow.Connection.QuerySingleAsync<int>(new CommandDefinition(SupplierBillSql.InsertHeader, new
        {
            h.BillNumber,
            h.SupplierId,
            h.BillDate,
            h.DueDate,
            h.SubTotal,
            h.DiscountAmount,
            h.TaxAmount,
            h.GrandTotal,
            h.Notes
        }, _uow.Transaction));

    public Task InsertLinesAsync(int supplierBillId, IReadOnlyList<SupplierBillLineComputed> lines)
    {
        var table = new DataTable();
        table.Columns.Add("LineNumber", typeof(int));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("Quantity", typeof(decimal));
        table.Columns.Add("UnitPrice", typeof(decimal));
        table.Columns.Add("DiscountPercent", typeof(decimal));
        table.Columns.Add("TaxRatePercent", typeof(decimal));
        table.Columns.Add("LineSubTotal", typeof(decimal));
        table.Columns.Add("LineDiscount", typeof(decimal));
        table.Columns.Add("LineTax", typeof(decimal));
        table.Columns.Add("LineTotal", typeof(decimal));
        table.Columns.Add("DebitAccountId", typeof(int));

        foreach (var l in lines)
        {
            table.Rows.Add(
                l.LineNumber, l.Description, l.Quantity, l.UnitPrice, l.DiscountPercent, l.TaxRatePercent,
                l.LineSubTotal, l.LineDiscount, l.LineTax, l.LineTotal, l.DebitAccountId);
        }

        var tvp = table.AsTableValuedParameter("dbo.SupplierBillLineTvp");
        return _uow.Connection.ExecuteAsync(new CommandDefinition(
            SupplierBillSql.InsertLines, new { SupplierBillId = supplierBillId, Lines = tvp }, _uow.Transaction));
    }

    public Task DeleteLinesAsync(int supplierBillId) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(
            SupplierBillSql.DeleteLines, new { SupplierBillId = supplierBillId }, _uow.Transaction));

    public Task UpdateHeaderAsync(SupplierBillHeaderUpdate h) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(SupplierBillSql.UpdateHeader, new
        {
            h.SupplierBillId,
            h.SupplierId,
            h.BillDate,
            h.DueDate,
            h.SubTotal,
            h.DiscountAmount,
            h.TaxAmount,
            h.GrandTotal,
            h.Notes
        }, _uow.Transaction));

    public async Task<SupplierBillResponse?> GetByIdAsync(int supplierBillId)
    {
        using var grid = await _uow.Connection.QueryMultipleAsync(new CommandDefinition(
            SupplierBillSql.GetById, new { SupplierBillId = supplierBillId }, _uow.Transaction));

        var header = await grid.ReadSingleOrDefaultAsync<SupplierBillHeaderRow>();
        if (header is null)
            return null;

        var lines = (await grid.ReadAsync<SupplierBillLineResponse>()).ToList();
        var allocations = (await grid.ReadAsync<SupplierBillAllocationResponse>()).ToList();

        return new SupplierBillResponse(
            header.SupplierBillId, header.BillNumber, header.SupplierId, header.SupplierCode, header.SupplierName,
            header.BillDate, header.DueDate, ((DocumentStatus)header.Status).ToString(),
            header.SubTotal, header.DiscountAmount, header.TaxAmount, header.GrandTotal, header.AmountPaid, header.Notes,
            header.JournalEntryId, header.PostedAtUtc, header.CreatedAtUtc, header.UpdatedAtUtc,
            lines, allocations);
    }

    public async Task<PagedResult<SupplierBillListItem>> ListAsync(SupplierBillQuery query)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var parameters = new
        {
            query.SupplierId,
            query.Status,
            query.FromDate,
            query.ToDate,
            page.Skip,
            page.Take
        };

        using var grid = await _uow.Connection.QueryMultipleAsync(
            new CommandDefinition(SupplierBillSql.List, parameters, _uow.Transaction));

        var rows = (await grid.ReadAsync<SupplierBillListRow>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        var items = rows
            .Select(r => new SupplierBillListItem(
                r.SupplierBillId, r.BillNumber, r.SupplierId, r.SupplierName,
                r.BillDate, r.DueDate, ((DocumentStatus)r.Status).ToString(), r.GrandTotal, r.AmountPaid))
            .ToList();

        return new PagedResult<SupplierBillListItem>(items, page.Page, page.PageSize, total);
    }

    public async Task<SupplierBillPostView?> GetForPostAsync(int supplierBillId)
    {
        using var grid = await _uow.Connection.QueryMultipleAsync(new CommandDefinition(
            SupplierBillSql.GetForPost, new { SupplierBillId = supplierBillId }, _uow.Transaction));

        var header = await grid.ReadSingleOrDefaultAsync<SupplierBillPostHeader>();
        if (header is null)
            return null;

        var lines = (await grid.ReadAsync<SupplierBillPostLine>()).ToList();
        return new SupplierBillPostView(
            header.SupplierBillId, header.BillNumber, header.Status, header.SupplierId, header.SupplierName,
            header.BillDate, header.TaxAmount, header.GrandTotal, lines);
    }

    public Task<SupplierBillGuard?> GetGuardAsync(int supplierBillId) =>
        _uow.Connection.QuerySingleOrDefaultAsync<SupplierBillGuard>(new CommandDefinition(
            SupplierBillSql.GetGuard, new { SupplierBillId = supplierBillId }, _uow.Transaction));

    public Task MarkPostedAsync(int supplierBillId, int journalEntryId) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(
            SupplierBillSql.MarkPosted, new { SupplierBillId = supplierBillId, JournalEntryId = journalEntryId }, _uow.Transaction));

    public Task DeleteAsync(int supplierBillId) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(
            SupplierBillSql.Delete, new { SupplierBillId = supplierBillId }, _uow.Transaction));

    // --- Dapper row shapes -------------------------------------------------

    private sealed class SupplierBillHeaderRow
    {
        public int SupplierBillId { get; set; }
        public string BillNumber { get; set; } = default!;
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; } = default!;
        public string SupplierName { get; set; } = default!;
        public DateOnly BillDate { get; set; }
        public DateOnly? DueDate { get; set; }
        public byte Status { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal AmountPaid { get; set; }
        public string? Notes { get; set; }
        public int? JournalEntryId { get; set; }
        public DateTime? PostedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    private sealed class SupplierBillListRow
    {
        public int SupplierBillId { get; set; }
        public string BillNumber { get; set; } = default!;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = default!;
        public DateOnly BillDate { get; set; }
        public DateOnly? DueDate { get; set; }
        public byte Status { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal AmountPaid { get; set; }
    }

    private sealed class SupplierBillPostHeader
    {
        public int SupplierBillId { get; set; }
        public string BillNumber { get; set; } = default!;
        public byte Status { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = default!;
        public DateOnly BillDate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
    }
}
