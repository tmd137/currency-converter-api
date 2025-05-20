using Asp.Versioning;
using CurrencyConverterAPI.APIEndpoints;
using CurrencyConverterAPI.Helpers;
using CurrencyConverterAPI.Services.Authentication;
using CurrencyConverterAPI.Services.CurrencyConverter;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Serilog.Sinks.InMemory;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

#region Serilog Config
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.InMemory()
        .CreateLogger();

    var logger = Log.ForContext("Policy", "Polly");
#endregion

#region Polly Config
var retryCount = builder.Configuration.GetRequiredSection("Polly:Policies:RetryCount").Get<int>();
var durationOfBreak = builder.Configuration.GetRequiredSection("Polly:Policies:DurationOfBreak").Get<int>();
var maxCountBeforeBreaker = builder.Configuration.GetRequiredSection("Polly:Policies:MaxCountBeforeBreaker").Get<int>();

var retryPolicy = HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: retryCount,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                logger.Warning($"Retry {retryAttempt} after {timespan.TotalSeconds} seconds due to {outcome.Exception?.Message}");
            });

    var circuitBreakerPolicy = HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: maxCountBeforeBreaker,
            durationOfBreak: TimeSpan.FromSeconds(durationOfBreak),
            onBreak: (outcome, timespan) =>
            {
                logger.Error($"Circuit broken for {timespan.TotalSeconds} seconds due to: {outcome.Exception?.Message}");
            },
            onReset: () => logger.Information("Circuit reset."),
            onHalfOpen: () => logger.Information("Circuit in half-open state...")
        );

    // Combine policies: retry first, then circuit breaker
    var combinedPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

    builder.Services.AddHttpClient("CurrencyConverterAPI", client =>
    {
        client.BaseAddress = new Uri("https://api.frankfurter.dev/v1/");
    })
    .AddPolicyHandler(combinedPolicy);

#endregion

#region Open Telemetry Config
var sourceName = builder.Configuration["ServiceName"] ?? "CurrencyConverter";
builder.Services.AddOpenTelemetry().WithTracing(b =>
{
    b.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(sourceName))
     .AddSource(sourceName)
     .AddAspNetCoreInstrumentation()
     .AddJaegerExporter(o =>
     {
         o.AgentHost = "localhost";
         o.AgentPort = 6831;
     })
     .AddConsoleExporter();
});
#endregion

#region Rate Limiting Config
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("strict", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
         await Task.FromResult(Results.StatusCode(StatusCodes.Status429TooManyRequests));
    };
});
#endregion

#region Authentication Config
// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// Authorization with RBAC
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireManager", policy => policy.RequireRole("Manager"));
});
#endregion

#region API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;

    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"),
        new QueryStringApiVersionReader("api-version")
    );
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV"; 
    options.SubstituteApiVersionInUrl = true;
}); ;
#endregion

#region APP Services
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddSingleton<ICurrencyProvider, FrankfurterCurrancyProvider>();
builder.Services.AddSingleton<ICurrencyProvider, EuropeanCentralBankCurrancyProvider>();
builder.Services.AddSingleton<ICurrencyProviderFactory, CurrencyProviderFactory>();
#endregion

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddMemoryCache();

builder.Host.UseSerilog();
var app = builder.Build();

app.Use(async (context, next) =>
{
    // Simulate a known IP for test purposes
    context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("123.123.123.123");
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseRateLimiter();
app.MapControllers();

#region Endpoints
app.MapGet("/", () => "Welcome to Bamboo Card Currency Converter API.");
app.AddAPIEndpoints();
#endregion

app.Run();
public partial class Program { }