using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace CurrencyConverterAPI.Services.CurrencyConverter
{
    public class FrankfurterCurrancyProvider : CurrencyProviderBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FrankfurterCurrancyProvider> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;

        public FrankfurterCurrancyProvider(IHttpClientFactory httpClientFactory, ILogger<FrankfurterCurrancyProvider> logger, IConfiguration configuration,IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _cache = cache;
        }

        public override string ProviderName => "Frankfurter";
 
        public override async Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency)
        {
            var badCurrencies_Setting = _configuration.GetSection("BadCurrencies").Get<string>() ?? string.Empty;
            var badCurrencies = badCurrencies_Setting.Split(',');

            //Exclude TRY, PLN, THB, and MXN from the response and return a bad request if these currencies are involved.
            foreach (string currency in badCurrencies ?? Enumerable.Empty<string>())
            {
                if (currency.Equals(fromCurrency, StringComparison.InvariantCultureIgnoreCase) || currency.Trim().Equals(toCurrency, StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.LogError($"Bad currency {fromCurrency} to {toCurrency}");
                    throw new BadHttpRequestException("Bad currency detected.");
                }
            }

            string currencyRatesCacheKey = $"_cache{fromCurrency}-{toCurrency}";
            var result = GetExchangeRateFromCache(fromCurrency, toCurrency);

            _logger.LogInformation("Checking cache for currency rates: {CacheKey}", currencyRatesCacheKey);
            // Check if the rate is already cached
            if (result != -1)
                return result;

            HttpClient client = _httpClientFactory.CreateClient("CurrencyConverterAPI");

            try
            {
                var response = await client.GetAsync($"{DateTime.Today.ToString("yyyy-MM-dd")}?base={fromCurrency}&symbols={toCurrency}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Response from Frankfurter API: {Content}", content);

                var rates = JsonSerializer.Deserialize<CurrencyApiResponseDTO>(content);

                if (rates?.rates[toCurrency] != null)
                {
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromHours(1)) // Cache for 1 hour
                        .SetSlidingExpiration(TimeSpan.FromMinutes(15)) // If not accessed for 15 mins, it expires
                        .SetPriority(CacheItemPriority.Normal); // Priority for eviction

                    _cache.Set(currencyRatesCacheKey, rates?.rates[toCurrency], cacheEntryOptions);
                    _logger.LogInformation("Successfully fetched and cached currency rates.");
                }
                return rates?.rates[toCurrency] ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"An unexpected error occurred: {ex.Message}");
                throw new Exception($"Frankfurter doesn't support {fromCurrency} to {toCurrency}");
            }
        }

        public override decimal GetExchangeRateFromCache(string fromCurrency, string toCurrency)
        {
            var currencyRatesCacheKey = $"_cache{fromCurrency}-{toCurrency}";
            if (_cache.TryGetValue(currencyRatesCacheKey, out decimal cachedResponse))
            {
                _logger.LogInformation("Retrieving currency rates from cache.");
                return cachedResponse;
            }
            else
                return -1;
        }

        public override async Task<CurrencyApiResponseHistoryDTO> GetHistoricalExchangeRateForCurrencyAsync(string baseCurrency, DateTime from, DateTime to)
        {
            if(from > to)
            {
                throw new BadHttpRequestException("From date must be earlier than to date.");
            }

            HttpClient client = _httpClientFactory.CreateClient("CurrencyConverterAPI");
            try
            {
                var response = await client.GetAsync($"{from.ToString("yyyy-MM-dd")}..{to.ToString("yyyy-MM-dd")}?base={baseCurrency}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Response from Frankfurter API: {Content}", content);

                var rates = JsonSerializer.Deserialize<CurrencyApiResponseHistoryDTO>(content);
                return rates;
            }
            catch (Exception ex)
            {
                _logger.LogError($"An unexpected error occurred: {ex.Message}");
                throw new Exception($"An unexpected error occurred: {ex.Message}");
            }
        }

        public override async Task<CurrencyApiResponseListDTO> GetLatestExchangeRateForCurrencyAsync(string baseCurrency)
        {
            HttpClient client = _httpClientFactory.CreateClient("CurrencyConverterAPI");
            try
            {
                var response = await client.GetAsync($"latest?base={baseCurrency}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Response from Frankfurter API: {Content}", content);

                var rates = JsonSerializer.Deserialize<CurrencyApiResponseListDTO>(content);
                return rates;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error calling Frankfurter API: {Message}", ex.Message);
                throw new HttpRequestException($"Error calling Frankfurter API: {ex.Message}");
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
            {
                _logger.LogError("Frankfurter API request timed out.");
                throw new TaskCanceledException($"Frankfurter API request timed out.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"An unexpected error occurred: {ex.Message}");
                throw new Exception($"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}