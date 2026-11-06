using Banking.Modules.Ledger.Infrastructure;
using Banking.Modules.Ledger.Presentation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddHealthChecks();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Unexpected exceptions become a 500 problem response without internal details.
builder.Services.AddProblemDetails();

// Each module brings its controllers from its own Presentation project.
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AccountsController).Assembly);

builder.Services.AddMediatR(options =>
{
    // The key comes from user-secrets or an environment variable (MediatR__LicenseKey), never from the repository.
    options.LicenseKey = builder.Configuration["MediatR:LicenseKey"];
    options.RegisterServicesFromAssemblies(LedgerModule.ApplicationAssembly);
});

var connectionString = builder.Configuration.GetConnectionString("Banking")
    ?? throw new InvalidOperationException("Connection string 'Banking' is not configured.");

builder.Services.AddLedgerModule(connectionString);

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
