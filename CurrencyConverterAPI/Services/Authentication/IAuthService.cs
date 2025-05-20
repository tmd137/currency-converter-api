namespace CurrencyConverterAPI.Services.Authentication
{
    public interface IAuthService
    {
        string GenerateToken(string username, string[] roles);
    }
}