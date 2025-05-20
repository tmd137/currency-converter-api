namespace CurrencyConverterAPI.Services.CurrencyConverter
{
    public interface ICurrencyProvider
    {
        string ProviderName { get; }
        
        decimal GetExchangeRateFromCache(string fromCurrency, string toCurrency);
        Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency);
        Task<decimal> ConvertAmountAsync(decimal amount, string fromCurrency, string toCurrency);
        Task<CurrencyApiResponseListDTO> GetLatestExchangeRateForCurrencyAsync(string baseCurrency);
        Task<CurrencyApiResponseHistoryDTO> GetHistoricalExchangeRateForCurrencyAsync(string baseCurrency, DateTime from, DateTime to);
    }
}
