using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Domain.Entities;
using AccountingERP.Infrastructure.Sql;
using Dapper;

namespace AccountingERP.Infrastructure.Repositories;

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly IUnitOfWork _uow;

    public SupplierRepository(IUnitOfWork uow) => _uow = uow;

    public async Task<PagedResult<SupplierResponse>> ListAsync(PartyQuery query)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var parameters = new
        {
            query.IsActive,
            SearchLike = SqlLike.Contains(query.Search),
            page.Skip,
            page.Take
        };

        using var grid = await _uow.Connection.QueryMultipleAsync(
            new CommandDefinition(SupplierSql.List, parameters, _uow.Transaction));

        var items = (await grid.ReadAsync<SupplierResponse>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        return new PagedResult<SupplierResponse>(items, page.Page, page.PageSize, total);
    }

    public Task<SupplierResponse?> GetByIdAsync(int supplierId) =>
        _uow.Connection.QuerySingleOrDefaultAsync<SupplierResponse>(
            new CommandDefinition(SupplierSql.GetById, new { SupplierId = supplierId }, _uow.Transaction));

    public Task<Supplier?> GetEntityByIdAsync(int supplierId) =>
        _uow.Connection.QuerySingleOrDefaultAsync<Supplier>(
            new CommandDefinition(SupplierSql.GetById, new { SupplierId = supplierId }, _uow.Transaction));

    public Task<int?> FindIdByCodeAsync(string supplierCode) =>
        _uow.Connection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(SupplierSql.FindIdByCode, new { SupplierCode = supplierCode }, _uow.Transaction));

    public async Task<bool> ExistsAsync(int supplierId) =>
        await _uow.Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(SupplierSql.Exists, new { SupplierId = supplierId }, _uow.Transaction));

    public async Task<bool> HasTransactionsAsync(int supplierId) =>
        await _uow.Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(SupplierSql.HasTransactions, new { SupplierId = supplierId }, _uow.Transaction));

    public Task<int> InsertAsync(Supplier supplier) =>
        _uow.Connection.QuerySingleAsync<int>(new CommandDefinition(SupplierSql.Insert, new
        {
            supplier.SupplierCode,
            supplier.Name,
            supplier.ContactPerson,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.IsActive
        }, _uow.Transaction));

    public Task UpdateAsync(Supplier supplier) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(SupplierSql.Update, new
        {
            supplier.SupplierId,
            supplier.SupplierCode,
            supplier.Name,
            supplier.ContactPerson,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.IsActive
        }, _uow.Transaction));

    public Task SoftDeleteAsync(int supplierId) =>
        _uow.Connection.ExecuteAsync(
            new CommandDefinition(SupplierSql.SoftDelete, new { SupplierId = supplierId }, _uow.Transaction));
}
