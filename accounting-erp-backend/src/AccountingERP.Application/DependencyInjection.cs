using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingERP.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers application services and FluentValidation validators.
    /// Validators are picked up automatically from this assembly.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISupplierService, SupplierService>();

        return services;
    }
}
