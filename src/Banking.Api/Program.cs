using Banking.Modules.Identity.Infrastructure;
using Banking.Modules.Ledger.Infrastructure;
using Banking.Modules.Ledger.Presentation;
using Banking.Modules.Transfers.Infrastructure;
using Banking.Modules.Transfers.Presentation;
using Banking.Web.Idempotency;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// One clock for the whole application; tests can replace it.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHealthChecks();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Unexpected exceptions become a 500 problem response without internal details.
builder.Services.AddProblemDetails();

// Each module brings its controllers from its own Presentation project.
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AccountsController).Assembly)
    .AddApplicationPart(typeof(TransfersController).Assembly);

builder.Services.AddMediatR(options =>
{
    // The key comes from user-secrets or an environment variable (MediatR__LicenseKey), never from the repository.
    options.LicenseKey = builder.Configuration["MediatR:LicenseKey"];
    options.RegisterServicesFromAssemblies(LedgerModule.ApplicationAssembly, TransfersModule.ApplicationAssembly);
});

var connectionString = builder.Configuration.GetConnectionString("Banking")
    ?? throw new InvalidOperationException("Connection string 'Banking' is not configured.");

// Stores Idempotency-Key requests for every module's [Idempotent] endpoints, in its own schema.
builder.Services.AddIdempotency(connectionString);
builder.Services.AddIdentityModule(connectionString);
builder.Services.AddLedgerModule(connectionString);
builder.Services.AddTransfersModule(connectionString, builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
