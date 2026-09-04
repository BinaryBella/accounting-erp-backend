using System.Data;
using Dapper;

namespace AccountingERP.Infrastructure;

/// <summary>
/// One-time Dapper setup, called from DI registration (PLAN §6, "two gotchas").
/// </summary>
public static class DapperConfiguration
{
    private static bool _applied;

    public static void Apply()
    {
        if (_applied)
            return;

        // Money must never silently truncate.
        SqlMapper.Settings.CommandTimeout = 30;

        // DateOnly on DATE columns. Dapper 2.1.35+ and Microsoft.Data.SqlClient 5.2+
        // support this natively, but registering the handler keeps the mapping explicit
        // and correct even if those versions move.
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        _applied = true;
    }

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);

        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }
    }
}
