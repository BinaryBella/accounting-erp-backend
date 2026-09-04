using AccountingERP.Api.Middleware;
using AccountingERP.Application;
using AccountingERP.Infrastructure;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// --- Services -------------------------------------------------------------
builder.Services.AddControllers();

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AccountingERP API",
        Version = "v1",
        Description = "SSIT Practical Assessment — Accounting & ERP backend. "
                    + "ASP.NET Core Web API + Dapper + SQL Server. Double-entry posting engine, "
                    + "sales invoices, supplier bills, payments and financial reports."
    });
});

var app = builder.Build();

// --- HTTP pipeline ------------------------------------------------------------
app.UseExceptionHandling();   // RFC 7807 for every unhandled exception (PLAN §7)

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", utcNow = DateTime.UtcNow }))
   .WithName("Health")
   .WithTags("Infrastructure");

app.Run();

// Exposed so the integration-test project can drive the API via WebApplicationFactory.
public partial class Program { }
