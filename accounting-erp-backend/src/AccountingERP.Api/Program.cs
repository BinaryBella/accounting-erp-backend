using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// --- Services -------------------------------------------------------------
builder.Services.AddControllers();

// RFC 7807 ProblemDetails is the error contract for every endpoint (see PLAN §7).
builder.Services.AddProblemDetails();

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

// Infrastructure (SqlConnectionFactory / UnitOfWork / repositories) and
// Application services are registered in later steps.

var app = builder.Build();

// --- HTTP pipeline ------------------------------------------------------------
app.UseExceptionHandler();   // pairs with AddProblemDetails; custom middleware added in a later step
app.UseStatusCodePages();

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
