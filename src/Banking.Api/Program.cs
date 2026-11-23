using Banking.Modules.Identity.Infrastructure;
using Banking.Modules.Identity.Presentation;
using Banking.Modules.Ledger.Infrastructure;
using Banking.Modules.Ledger.Presentation;
using Banking.Modules.Transfers.Infrastructure;
using Banking.Modules.Transfers.Presentation;
using Banking.Web.Authentication;
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

// Bearer tokens issued by the Identity module. Every endpoint requires one unless it is [AllowAnonymous].
builder.Services.AddJwtAuthentication(builder.Configuration);

// Each module brings its controllers from its own Presentation project.
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AuthController).Assembly)
    .AddApplicationPart(typeof(AccountsController).Assembly)
    .AddApplicationPart(typeof(TransfersController).Assembly);

builder.Services.AddMediatR(options =>
{
    // The key comes from user-secrets or an environment variable (MediatR__LicenseKey), never from the repository.
    options.LicenseKey = builder.Configuration["MediatR:LicenseKey"];
    options.RegisterServicesFromAssemblies(
        IdentityModule.ApplicationAssembly,
        LedgerModule.ApplicationAssembly,
        TransfersModule.ApplicationAssembly);
});

var connectionString = builder.Configuration.GetConnectionString("Banking")
    ?? throw new InvalidOperationException("Connection string 'Banking' is not configured.");

// Stores Idempotency-Key requests for every module's [Idempotent] endpoints, in its own schema.
builder.Services.AddIdempotency(connectionString);
builder.Services.AddIdentityModule(connectionString, builder.Configuration);
builder.Services.AddLedgerModule(connectionString);
builder.Services.AddTransfersModule(connectionString, builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Not in development: the redirect from http to https makes clients drop the Authorization header, so a correct
// token would be answered with 401.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Who is calling (the token), then whether they may (the endpoint's policy). The order matters.
app.UseAuthentication();
app.UseAuthorization();

// Load balancers check liveness without a token.
app.MapHealthChecks("/health").AllowAnonymous();

app.MapControllers();

app.Run();
