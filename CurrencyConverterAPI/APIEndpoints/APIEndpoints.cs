using Asp.Versioning;
using CurrencyConverterAPI.Services.Authentication;
using CurrencyConverterAPI.Services.CurrencyConverter;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CurrencyConverterAPI.APIEndpoints
{
    public static class APIEndpoints
    {
        public static void AddAPIEndpoints(this WebApplication app)
        {
            app.MapGet("/ip-test", () => Results.Ok("IP logged"));
            app.MapGet("/ip-test2", () => Results.Ok("X-Forwarded-For IP logged"));
            app.MapGet("/ip-test3", () => Results.Ok("X-Real-IP IP logged"));

            //This is for testing Request Logging Middleware
            app.MapGet("/middleware", (HttpContext context) => {
                return "Hello RequestLoggingMiddleware";
            });

            //This is for testing Server Error
            app.MapGet("/error", (HttpContext context) => {
                throw new Exception("ERROR!!!");
            });

            //This is for testing Open Telemetry and API throttling 
            app.MapGet("/api/trace", (HttpContext context) => {
                var source = new ActivitySource("CurrencyConverter");
                using var activity = source.StartActivity("TestTrace", ActivityKind.Server);
                activity?.SetTag("http.method", context.Request.Method);
                activity?.SetTag("http.url", context.Request.Path);

                return "This endpoint is throttled by the 'strict' policy";

            }).RequireRateLimiting("strict");

            //This is will authenticate and generate jwt token for user
            app.MapPost("/api/token", (LoginRequest request, IAuthService authService) =>
            {
                var roles = new List<string>();
                // Add roles based on username (for demo)
                if (request.Username == "admin")
                {
                    roles.Add("Admin");
                }
                else
                {
                    roles.Add("User");
                }

                var token = authService.GenerateToken(request.Username, roles.ToArray());
                return Results.Ok(new { token });
            });


            // Endpoint to get exchange rate with specific provider
            app.MapGet("/api/exchange-rate/{provider}", async (
                string provider,
                [FromQuery] string from,
                [FromQuery] string to,
                ICurrencyProviderFactory factory) =>
            {
                try
                {
                    var providerInstance = factory.GetProvider(provider);
                    var rate = await providerInstance.GetExchangeRateAsync(from, to);
                    return Results.Ok(new { from, to, rate, provider = providerInstance.ProviderName });
                }
                catch (BadHttpRequestException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            })
            .WithOpenApi();

            // Endpoint to convert amount with specific provider
            app.MapGet("/api/convert/{provider}", async (
                string provider,
                [FromQuery] decimal amount,
                [FromQuery] string from,
                [FromQuery] string to,
                ICurrencyProviderFactory factory) =>
            {
                try
                {
                    var providerInstance = factory.GetProvider(provider);
                    var result = await providerInstance.ConvertAmountAsync(amount, from, to);
                    return Results.Ok(new { amount, from, to, result, provider = providerInstance.ProviderName });
                }
                catch (Exception ex) when (ex is KeyNotFoundException || ex is BadHttpRequestException)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            })
            .WithOpenApi();


            // Endpoint to get latest exchange rates with specific provider and RBAC access control
            app.MapGet("/api/latest-exchange-rates/{provider}", async (
                string provider,
                [FromQuery] string currency,
                ICurrencyProviderFactory factory) =>
            {
                try
                {
                    var providerInstance = factory.GetProvider(provider);
                    var result = await providerInstance.GetLatestExchangeRateForCurrencyAsync(currency);
                    return Results.Ok(new { currency, result, provider = providerInstance.ProviderName });
                }
                catch (BadHttpRequestException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            })
            .WithOpenApi()
            .RequireAuthorization("RequireAdmin"); 

            //API Version Set
            var apiVersionSet = app.NewApiVersionSet()
                .HasApiVersion(new ApiVersion(1, 0))
                .HasApiVersion(new ApiVersion(2, 0))
                .HasDeprecatedApiVersion(new ApiVersion(1, 0))
                .ReportApiVersions()
                .Build();

            // Endpoint to get historical exchange rates with specific provider /v1/api/historical-exchange-rates
            app.MapGet("/v{apiVersion:apiVersion}/api/historical-exchange-rates/{provider}", async (
                string provider,
                [FromQuery] string currency,
                [FromQuery] DateTime from,
                [FromQuery] DateTime to,
                ICurrencyProviderFactory factory) =>
            {
                try
                {
                    var providerInstance = factory.GetProvider(provider);
                    var result = await providerInstance.GetHistoricalExchangeRateForCurrencyAsync(currency, from, to);
                    return Results.Ok(new { Version = "1", currency, from, to, result, provider = providerInstance.ProviderName });
                }
                catch (BadHttpRequestException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            })
            .WithOpenApi()
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1, 0));

            // Endpoint to get historical exchange rates with specific provider /v2/api/historical-exchange-rates
            app.MapGet("/v{apiVersion:apiVersion}/api/historical-exchange-rates/{provider}", async (
                string provider,
                [FromQuery] string currency,
                [FromQuery] DateTime from,
                [FromQuery] DateTime to,
                ICurrencyProviderFactory factory) =>
            {
                try
                {
                    var providerInstance = factory.GetProvider(provider);
                    var result = await providerInstance.GetHistoricalExchangeRateForCurrencyAsync(currency, from, to);
                    return Results.Ok(new { Version = "2", currency, from, to, result, provider = providerInstance.ProviderName });
                }
                catch (BadHttpRequestException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            })
            .WithOpenApi()
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(2, 0));

        }
    }
}
