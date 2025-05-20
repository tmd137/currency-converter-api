namespace CurrencyConverterAPI.Services.CurrencyConverter
{
    public class EuropeanCentralBankCurrancyProvider : CurrencyProviderBase
    {
        public override string ProviderName => "EuropeanCentralBank";

        public override async Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency)
        {
            return 100;
        }

        public override decimal GetExchangeRateFromCache(string fromCurrency, string toCurrency)
        {
            throw new NotImplementedException();
        }

        public override async Task<CurrencyApiResponseHistoryDTO> GetHistoricalExchangeRateForCurrencyAsync(string baseCurrency, DateTime from, DateTime to)
        {
            throw new NotImplementedException();
        }

        public override async Task<CurrencyApiResponseListDTO> GetLatestExchangeRateForCurrencyAsync(string baseCurrency)
        {
            throw new NotImplementedException();
        }
    }
}
