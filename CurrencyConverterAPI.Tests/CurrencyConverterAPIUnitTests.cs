using CurrencyConverterAPI.Services.CurrencyConverter;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace CurrencyConverterAPI.Tests
{
    public class CurrencyConverterAPIUnitTests : IClassFixture<CurrencyProviderFactoryFixture>
    {
        private readonly ICurrencyProviderFactory _factory;
        private readonly string Provider = "Frankfurter";

        public CurrencyConverterAPIUnitTests(CurrencyProviderFactoryFixture fixture)
        {
            _factory = fixture.Factory;
        }

        [Fact]
        public async Task Convert_ValidInput_ReturnsConvertedAmount()
        {
            //Arrange  
            var providerInstance = _factory.GetProvider(Provider);

            //Act  
            var result = await providerInstance.ConvertAmountAsync(30, "EUR", "USD");

            Assert.True(result > 0);
        }

        [Theory]
        [InlineData(-10)]
        [InlineData(0)]
        public async Task Convert_InvalidAmount_ThrowsException(decimal amount)
        {
            //Arrange  
            var providerInstance = _factory.GetProvider(Provider);

            //Act & Assert  
            await Assert.ThrowsAsync<BadHttpRequestException>(async () =>
                 await providerInstance.ConvertAmountAsync(amount, "EUR", "CAD"));
        }

        [Theory]
        [InlineData(-10)]
        [InlineData(0)]
        public async Task Convert_Endpoint_ThrowsException_For_Invalid_Provider(decimal amount)
        {
            //Arrange  
            var providerInstance = _factory.GetProvider(Provider);

            //Act & Assert  
            await Assert.ThrowsAsync<Exception>(async () =>
                 await providerInstance.ConvertAmountAsync(amount, "EUR", string.Empty));
        }

        [Fact]
        public async Task GetProvider_Should_Throw_Unsupported_Provider()
        {
            //Arrange, Act & Assert  
            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            {
                _ = _factory.GetProvider("UnknownProvider");
            });
        }

        [Fact]
        public void GetProvider_Should_Return_Frankfurter()
        {
            var provider = _factory.GetProvider(Provider);

            Assert.NotNull(provider);
            Assert.Equal("Frankfurter", provider.ProviderName);
        }

        [Fact]
        public async Task Frankfurter_Provider_GetExchangeRateAsync_Should_Return_LessThan_1()
        {
            var provider = _factory.GetProvider(Provider);
            var result = await provider.GetExchangeRateAsync("USD", "EUR");
            Assert.True(result < 1);
        }

        [Fact]
        public void GetExchangeRate_ShouldReturnFromCache_WhenCacheExists()
        {
            // Arrange
            var provider = _factory.GetProvider(Provider);

            // Act
            var result = provider.GetExchangeRateFromCache("USD", "EUR");

            // Assert
            Assert.True(result != -1);
        }

        [Fact]
        public void GetProvider_Should_Return_EuropeanCentralBank()
        {
            var provider = _factory.GetProvider("EuropeanCentralBank");

            Assert.NotNull(provider);
            Assert.Equal("EuropeanCentralBank", provider.ProviderName);
        }

        [Fact]
        public async Task EuropeanCentralBank_Provider_GetExchangeRateAsync_Should_Return_100()
        {
            var provider = _factory.GetProvider("EuropeanCentralBank");

            Assert.NotNull(provider);
            Assert.Equal(100, await provider.GetExchangeRateAsync("USD", "EUR"));
        }

        [Fact]
        public async Task EuropeanCentralBank_Provider_GetHistoricalExchangeRateForCurrencyAsync_Should_Throw_NotImplementedException()
        {
            var provider = _factory.GetProvider("EuropeanCentralBank");

            await Assert.ThrowsAsync<NotImplementedException>(async () =>
                await provider.GetHistoricalExchangeRateForCurrencyAsync("EUR", DateTime.Today, DateTime.Today));
        }

        [Fact]
        public async Task EuropeanCentralBank_Provider_GetLatestExchangeRateForCurrencyAsync_Should_Throw_NotImplementedException()
        {
            var provider = _factory.GetProvider("EuropeanCentralBank");

            await Assert.ThrowsAsync<NotImplementedException>(async () =>
                await provider.GetLatestExchangeRateForCurrencyAsync("EUR"));
        }

        [Fact]
        public async Task EuropeanCentralBank_Provider_GetExchangeRateFromCache_Should_Throw_NotImplementedException()
        {
            var provider = _factory.GetProvider("EuropeanCentralBank");

            await Assert.ThrowsAsync<NotImplementedException>(async () =>
                 provider.GetExchangeRateFromCache("EUR", "USD"));
        }

        [Fact]
        public async Task Frankfurter_Provider_GetExchangeRateAsync_Should_Throw_HttpRequestException()
        {
            var provider = _factory.GetProvider(Provider);

            await Assert.ThrowsAsync<Exception>(async () =>
                 await provider.GetExchangeRateAsync("ZZZ", "USD"));
        }

        [Fact]
        public async Task Frankfurter_Provider_GetHistoricalExchangeRateForCurrencyAsync_Should_Throw_NotImplementedException()
        {
            var provider = _factory.GetProvider(Provider);

            await Assert.ThrowsAsync<Exception>(async () =>
                 await provider.GetHistoricalExchangeRateForCurrencyAsync("ZZZ", DateTime.Today, DateTime.Today));
        }

        [Fact]
        public async Task GetLatestRates_ValidInput_ReturnsRates()
        {
            //Arrange  
            var providerInstance = _factory.GetProvider(Provider);

            var result = await providerInstance.GetLatestExchangeRateForCurrencyAsync("EUR");

            Assert.NotNull(result);
            Assert.Contains("USD", result.rates);
        }

        [Fact]
        public async Task GetLatestRates_InvalidCurrency_Throws()
        {
            //Arrange  
            var providerInstance = _factory.GetProvider(Provider);

            //Act & Assert  
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
             await providerInstance.GetLatestExchangeRateForCurrencyAsync("ZZZ"));
        }


        [Fact]
        public async Task GetLatestRates_Returns_CurrencyApiResponseListDTO()
        {
            //Arrange  
            var providerInstance = _factory.GetProvider(Provider);
            //Act  
            var result = await providerInstance.GetLatestExchangeRateForCurrencyAsync("EUR");

            Assert.NotNull(result);
            Assert.Equal(typeof(CurrencyApiResponseListDTO), result.GetType());
        }

        [Fact]
        public async Task GetHistoricalRates_Returns_CurrencyApiResponseHistoryDTO()
        {
            //Arrange  
            var providerInstance = _factory.GetProvider(Provider);
            var from = new DateTime(2025, 5, 1).AddDays(-1);
            var to = new DateTime(2025, 5, 1);

            //Act  
            var result = await providerInstance.GetHistoricalExchangeRateForCurrencyAsync("EUR", from, to);

            Assert.NotNull(result);
            Assert.Equal(typeof(CurrencyApiResponseHistoryDTO), result.GetType()); 
        }

        [Fact]
        public async Task GetHistoricalRates_ValidRange_ReturnsRates()
        {
            //Arrange  
            var providerInstance = _factory.GetProvider(Provider);
            var from = new DateTime(2025, 5, 1).AddDays(-1);
            var to = new DateTime(2025, 5, 5);

            //Act  
            var result = await providerInstance.GetHistoricalExchangeRateForCurrencyAsync("EUR", from, to);

            Assert.NotNull(result);
            Assert.All(result.rates.Keys, date => Assert.InRange(DateTime.Parse(date), from, to));
        }

        [Fact]
        public async Task GetHistoricalRates_InvalidDateRange_Throws()
        {
            //Arrange  
            var providerInstance = _factory.GetProvider(Provider);
            var from = new DateTime(2025, 5, 16);
            var to = new DateTime(2025, 5, 1);

            await Assert.ThrowsAsync<BadHttpRequestException>(async () =>
                await providerInstance.GetHistoricalExchangeRateForCurrencyAsync("EUR", from, to));
        }
    }

    public class CurrencyProviderFactoryFixture
    {
        public ICurrencyProviderFactory Factory { get; }
        public IServiceProvider Services { get; }

        public CurrencyProviderFactoryFixture()
        {
            // Set up DI container manually
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Add memory cache
            services.AddMemoryCache();

            // Add configuration
            var configuration = new ConfigurationBuilder().Build();

            services.AddSingleton<IConfiguration>(configuration);

            // Register IHttpClientFactory
            services.AddHttpClient("CurrencyConverterAPI", client =>
            {
                client.BaseAddress = new Uri("https://api.frankfurter.dev/v1/");
            });

            // Build provider
            Services = services.BuildServiceProvider();

            // Resolve dependencies
            var loggerFactory = Services.GetRequiredService<ILoggerFactory>();
            var httpClientFactory = Services.GetRequiredService<IHttpClientFactory>();
            var memoryCache = Services.GetRequiredService<IMemoryCache>();
            var config = Services.GetRequiredService<IConfiguration>();

            var logger = loggerFactory.CreateLogger<CurrencyProviderFactory>();
            var frankfurter = new FrankfurterCurrancyProvider(httpClientFactory, loggerFactory.CreateLogger<FrankfurterCurrancyProvider>(), config, memoryCache);
            var europeCentralBank = new EuropeanCentralBankCurrancyProvider();

            Factory = new CurrencyProviderFactory(new List<ICurrencyProvider> { frankfurter, europeCentralBank }, logger);
        }
    }
}
