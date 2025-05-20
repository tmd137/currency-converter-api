
namespace CurrencyConverterAPI.Services.CurrencyConverter
{
    public abstract class CurrencyProviderBase : ICurrencyProvider
    {
        public abstract string ProviderName { get; }

        public virtual async Task<decimal> ConvertAmountAsync(decimal amount, string fromCurrency, string toCurrency)
        {
            if(amount <= 0)
                throw new BadHttpRequestException("Amount must be greater than zero.");
            var rate = await GetExchangeRateAsync(fromCurrency, toCurrency);
            return amount * rate;
        }
        public abstract decimal GetExchangeRateFromCache(string fromCurrency, string toCurrency);

        public abstract Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency);

        public abstract Task<CurrencyApiResponseHistoryDTO> GetHistoricalExchangeRateForCurrencyAsync(string baseCurrency, DateTime from, DateTime to);

        public abstract Task<CurrencyApiResponseListDTO> GetLatestExchangeRateForCurrencyAsync(string baseCurrency);

    }
}
