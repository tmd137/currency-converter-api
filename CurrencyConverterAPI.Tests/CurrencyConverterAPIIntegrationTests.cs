using CurrencyConverterAPI.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace CurrencyConverterAPI.Tests
{
    public class CurrencyConverterAPIIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        public Mock<ILogger<RequestLoggingMiddleware>> MockLogger { get; } = new();

        public CurrencyConverterAPIIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task JWT_Token_Endpoint_ReturnsOk()
        {
            string jsonPayload = JsonConvert.SerializeObject(
                        new
                        {
                            Username = "admin",
                            Password = "*"
                        });

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
             HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/token/");
            request.Content = content;
            HttpResponseMessage response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private async Task<string> GetTestToken(string username)
        {
            string jsonPayload = JsonConvert.SerializeObject(
                       new
                       {
                           Username = username,
                           Password = "*"
                       });

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/token/");
            request.Content = content;
            HttpResponseMessage response = await _client.SendAsync(request);

            string responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<AuthResponse>(responseJson);
            return result?.token ?? string.Empty;
        }

        [Fact]
        public async Task JWT_Token_Endpoint_Returns_Valid_Token()
        {
            //Arrange
            var token = await GetTestToken("admin");

            // Act
            var handler = new JwtSecurityTokenHandler();

            // Assert
            Assert.True(handler.CanReadToken(token));

            var jwtToken = handler.ReadJwtToken(token);
            Assert.NotNull(jwtToken);
            Assert.Equal("admin", jwtToken.Payload["sub"]);
        }

        [Fact]
        public async Task JWT_Token_Endpoint_Returns_Valid_Token_For_Non_Admins()
        {
            //Arrange
            var token = await GetTestToken("user");

            // Act
            var handler = new JwtSecurityTokenHandler();

            // Assert
            Assert.True(handler.CanReadToken(token));

            var jwtToken = handler.ReadJwtToken(token);
            Assert.NotNull(jwtToken);
            Assert.Equal("user", jwtToken.Payload["sub"]);
        }

        [Theory]
        [InlineData("Frankfurter", 30, "EUR", "USD")]
        [InlineData("Frankfurter", 100, "USD", "JPY")]
        public async Task ConvertEndpoint_ReturnsOk(string provider, decimal amount, string from, string to)
        {
            var response = await _client.GetAsync($"/api/convert/{provider}?amount={amount}&from={from}&to={to}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData("Frankfurter", "EUR")]
        public async Task LatestExchangeRates_Returns_UnAuthorized(string provider, string currency)
        {
            var response = await _client.GetAsync($"/api/latest-exchange-rates/{provider}?currency={currency}");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("Frankfurter", "EUR")]
        public async Task LatestExchangeRates_ReturnsOk(string provider, string currency)
        {
            var token = await GetTestToken("admin");
            // Add the Authorization header
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _client.GetAsync($"/api/latest-exchange-rates/{provider}?currency={currency}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData("Frankfurter", "EUR", "2025-05-01", "2025-05-16")]
        public async Task HistoricalExchangeRates_ReturnsOk(string provider, string currency, string from, string to)
        {
            var response = await _client.GetAsync($"/v2/api/historical-exchange-rates/{provider}?currency={currency}&from={from}&to={to}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData("UnknownProvider")]
        public async Task UnknownProvider_ReturnsBadRequest(string provider)
        {
            var response = await _client.GetAsync($"/api/convert/{provider}?amount=30&from=EUR&to=USD");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Convert_InvalidAmount_ReturnsBadRequest()
        {
            var response = await _client.GetAsync("/api/convert/Frankfurter?amount=-5&from=EUR&to=USD");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task V1_Historical_InvalidDateRange_ReturnsBadRequest()
        {
            var response = await _client.GetAsync("/v1/api/historical-exchange-rates/Frankfurter?currency=EUR&from=2025-05-16&to=2025-05-01");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Historical_InvalidDateRange_ReturnsBadRequest()
        {
            var response = await _client.GetAsync("/v2/api/historical-exchange-rates/Frankfurter?currency=EUR&from=2025-05-16&to=2025-05-01");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Exhange_Rate_Should_Return_BadRequest_For_Some_Currency()
        {
            //TRY,PLN,THB,MXN
            var response = await _client.GetAsync("/api/exchange-rate/Frankfurter?from=TRY&to=EUR");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Exhange_Rate_Should_Not_Return_BadRequest_For_Valid_Currency()
        {
            var response = await _client.GetAsync("/api/exchange-rate/Frankfurter?from=USD&to=EUR");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task TraceEndpoint_EnforcesRateLimitPolicy()
        {
            const string url = "/api/trace";

            HttpResponseMessage? lastResponse = null;

            // First 10 requests should succeed
            for (int i = 1; i <= 10; i++)
            {
                var response = await _client.GetAsync(url);
                Assert.True(response.IsSuccessStatusCode, $"Request {i} should succeed");
                lastResponse = response;
            }

            //11th request should fail with 429
            var tooManyResponse = await _client.GetAsync(url);
            Assert.Equal(HttpStatusCode.TooManyRequests, tooManyResponse.StatusCode);
        }

        [Fact]
        public async Task RequestLoggingMiddleware_LogsRequestInfo()
        {
            // Arrange and Act
            var response = await _client.GetAsync("/middleware");

            // Assert
            Assert.True(response.IsSuccessStatusCode);
        }

        [Fact]
        public async Task RequestLoggingMiddleware_Logs_404_Response()
        {
            var response = await _client.GetAsync("/nonexistent");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Logs_500_Response()
        {
            var response = await _client.GetAsync("/error");
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task Logs_IP_From_XForwardedFor_Header()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/ip-test2");
            request.Headers.Add("X-Forwarded-For", "203.0.113.1");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.EnsureSuccessStatusCode();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Logs_IP_From_XRealIP_Header()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/ip-test3");
            request.Headers.Add("X-Real-IP", "198.1.2.3");

            var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Logs_IP_Address()
        {
            var response = await _client.GetAsync("/ip-test");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    record AuthResponse(string token);
}