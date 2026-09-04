using AccountingERP.Application.Abstractions;
using AccountingERP.Infrastructure.Sql;
using Dapper;

namespace AccountingERP.Infrastructure.Repositories;

public sealed class NumberSequenceRepository : INumberSequenceRepository
{
    private readonly IUnitOfWork _uow;

    public NumberSequenceRepository(IUnitOfWork uow) => _uow = uow;

    public async Task<string> NextAsync(string sequenceKey)
    {
        var number = await _uow.Connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(NumberSequenceSql.AllocateNext, new { SequenceKey = sequenceKey }, _uow.Transaction));

        if (string.IsNullOrEmpty(number))
            throw new InvalidOperationException(
                $"Number sequence '{sequenceKey}' is not configured in dbo.NumberSequence. Run 02_seed.sql.");

        return number;
    }
}
