namespace CurrencyConverterAPI.Services.CurrencyConverter
{
    public interface ICurrencyProviderFactory
    {
        ICurrencyProvider GetProvider(string providerKey);
    }
}
