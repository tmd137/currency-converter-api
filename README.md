# Currency Converter API

A RESTful API that provides currency exchange rates and conversion using multiple provider integrations.

## API Endpoints

### 1. Get Latest Exchange Rates

GET /api/latest-exchange-rates/{provider}?currency={currency}


**Parameters:**
- `provider`: The exchange rate provider (e.g., "Frankfurter", "ECB", "CRYPTO")
- `currency`: Base currency code (ISO 4217, e.g., "USD", "EUR")

**Response:**
```json
{
  "base": "EUR",
  "rates": {
    "USD": 1.08,
    "GBP": 0.85
  },
  "provider": "ECB",
  "timestamp": "2023-11-15T14:30:00Z"
}
```

### 2. Convert Currency Amount

GET /api/convert/{provider}?amount={amount}&from={from}&to={to}

**Parameters:**

- `provider`: The exchange rate provider
- `amount`: Amount to convert
- `from`: Source currency code
- `to`: Target currency code

**Response:**
"convertedAmount": 100

### 3. Get Historical Exchange Rates  (with v1 and v2)

GET /v1/api/historical-exchange-rates/{provider}?currency={currency}&from={from}&to={to}

GET /v2/api/historical-exchange-rates/{provider}?currency={currency}&from={from}&to={to}

**Parameters:**
- `provider`: The exchange rate provider
- `currency`: Base currency code
- `from`: Start date (YYYY-MM-DD)
- `to`: End date (YYYY-MM-DD)

**Response:**
```json
{
  "base": "EUR",
  "provider": "Frankfurter",
  "start_date": "2023-11-01",
  "end_date": "2023-11-15",
  "rates": {
    "2023-11-01": {
      "USD": 1.06,
      "GBP": 0.87
    },
    "2023-11-15": {
      "USD": 1.08,
      "GBP": 0.85
    }
  }
}
```


**Setup Instructions**

Prerequisites
.NET 9 SDK

Installation

1. Clone the repository
   
  git clone https://github.com/tmd137/currency-converter-api.git  
  
  cd currency-converter-api

2. Restore dependencies
   
   dotnet restore

3. Run the application
   cd CurrencyConverterAPI
   dotnet run

**Assumptions**

*Provider Availability:*

- The API assumes provider services are always available
- Only one provider is sufficent to cover the technical requirements

*Authentication:*

- Public endpoints don't require authentication
- Future versions may implement API key authentication, for now one endpoint is authenticated with a test JWT token

**Possible Future Enhancements**

*Multi-provider Fallback:*
  Automatic failover to secondary providers

*Advanced Conversion:*
  Bulk conversions in single request

*Others:*
  Swagger/OpenAPI Integration: Expose full documentation and try-it-out features.
  Docker Support: Add Dockerfile and docker-compose for containerized deployment.
  Rate Limit Headers: Return rate limit status in response headers.
  Export Features: Allow results to be exported in CSV or JSON.


**Contact**

  For an questions, please contact:
  tinishutmd@gmail.com
