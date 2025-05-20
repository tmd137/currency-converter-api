namespace CurrencyConverterAPI
{
    //{"amount":1.0,"base":"EUR","date":"2025-05-16","rates":{"USD":1.1194}}
    public record CurrencyApiResponseDTO(decimal Amount, string Base, DateTime date, Dictionary<string, decimal> rates);
    public record CurrencyApiResponseListDTO(decimal amount, string @base, DateTime date, Dictionary<string, decimal> rates);
    public record CurrencyApiResponseHistoryDTO(decimal amount, string @base, DateTime start_date, DateTime end_date, Dictionary<string, Dictionary<string, decimal>> rates);
}
