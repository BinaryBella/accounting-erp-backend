using AccountingERP.Application.Abstractions;
using AccountingERP.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingERP.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Dapper data-access: the connection factory (stateless, singleton),
    /// the per-request unit of work, and the repositories.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        DapperConfiguration.Apply();

        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IJournalRepository, JournalRepository>();
        services.AddScoped<INumberSequenceRepository, NumberSequenceRepository>();

        return services;
    }
}
