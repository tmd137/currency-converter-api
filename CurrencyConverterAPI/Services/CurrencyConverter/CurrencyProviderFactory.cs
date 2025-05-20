namespace CurrencyConverterAPI.Services.CurrencyConverter
{
    public class CurrencyProviderFactory : ICurrencyProviderFactory
    {
        private readonly Dictionary<string, ICurrencyProvider> _providers;
        private readonly ILogger<CurrencyProviderFactory> _logger;

        public CurrencyProviderFactory(
            IEnumerable<ICurrencyProvider> providers,
            ILogger<CurrencyProviderFactory> logger)
        {
            _logger = logger;
            _providers = providers.ToDictionary(p => p.ProviderName, p => p);
        }

        public ICurrencyProvider GetProvider(string providerKey)
        {
            if (_providers.TryGetValue(providerKey ?? string.Empty, out var provider))
            {
                return provider;
            }

            _logger.LogWarning("Unknown currency provider requested: {Provider}", providerKey);
            throw new KeyNotFoundException($"Currency provider '{providerKey}' not found");
        }
    }
}
