using System.Net.Http.Headers;
using DearHome_Backend.Config;
using DearHome_Backend.DTOs.ShippingDtos;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DearHome_Backend.Services.External;

/// <summary>
/// <see cref="IGoshipClient"/> implementation. Registered with <c>AddHttpClient</c>; the base address comes from
/// <see cref="GoshipOptions.BaseUrl"/>. Access tokens are cached in <see cref="IDistributedCache"/>.
/// </summary>
public class GoshipClient : IGoshipClient
{
    private const string TokenCacheKey = "GoShip_AccessToken";
    private const string ExpiryTimeCacheKey = "GoShip_TokenExpiry";

    private readonly HttpClient _httpClient;
    private readonly GoshipOptions _options;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GoshipClient> _logger;
    private readonly TelemetryClient _telemetryClient;

    public GoshipClient(
        HttpClient httpClient,
        IOptions<GoshipOptions> options,
        IDistributedCache cache,
        ILogger<GoshipClient> logger,
        TelemetryClient telemetryClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

        if (_httpClient.BaseAddress == null)
        {
            if (string.IsNullOrEmpty(_options.BaseUrl))
            {
                throw new InvalidOperationException("GoShip:BaseUrl is not configured in application settings.");
            }
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }
    }

    public async Task<string> GetAccessTokenAsync()
    {
        using var operation = _telemetryClient.StartOperation<DependencyTelemetry>("GetValidToken");
        try
        {
            string? accessToken = null;
            string? expiryTimeString = null;
            bool cacheOperationSucceeded = true;

            // Add retry policy for Redis operations
            int maxRetries = 3;
            int currentRetry = 0;
            TimeSpan delay = TimeSpan.FromSeconds(1);

            while (currentRetry <= maxRetries)
            {
                try
                {
                    // Try to get the token from cache with retry logic
                    accessToken = await _cache.GetStringAsync(TokenCacheKey);
                    expiryTimeString = await _cache.GetStringAsync(ExpiryTimeCacheKey);
                    break; // Success, exit retry loop
                }
                catch (Exception ex) when (ex is StackExchange.Redis.RedisConnectionException ||
                                          ex is StackExchange.Redis.RedisTimeoutException ||
                                          ex is System.Net.Sockets.SocketException)
                {
                    // Only retry on connection-related exceptions
                    if (currentRetry == maxRetries)
                    {
                        _logger.LogError(ex, "Redis cache access failed after {RetryCount} retries", maxRetries);
                        _telemetryClient.TrackEvent("RedisCacheFailure", new Dictionary<string, string>
                    {
                        { "Operation", "GetToken" },
                        { "Error", ex.Message }
                    });
                        cacheOperationSucceeded = false;
                        break;
                    }

                    currentRetry++;
                    _logger.LogWarning(ex, "Redis cache access failed (attempt {RetryCount}/{MaxRetries}). Retrying...",
                        currentRetry, maxRetries);

                    // Track dependency failure with Application Insights
                    _telemetryClient.TrackDependency(
                        "Redis",
                        "GetToken",
                        $"{TokenCacheKey}/{ExpiryTimeCacheKey}",
                        DateTimeOffset.UtcNow,
                        TimeSpan.FromMilliseconds(500),
                        false);

                    await Task.Delay(delay);
                    delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, currentRetry))); // Exponential backoff up to 30 seconds
                }
            }

            bool tokenValid = false;

            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(expiryTimeString))
            {
                if (DateTime.TryParse(expiryTimeString, out DateTime expiryTime))
                {
                    // Check if token is still valid (with 5-minute buffer)
                    tokenValid = expiryTime > DateTime.UtcNow.AddMinutes(5);

                    // Add telemetry for token validation
                    _telemetryClient.TrackMetric("TokenTimeRemaining", (expiryTime - DateTime.UtcNow).TotalMinutes);
                }
            }

            if (!tokenValid)
            {
                _logger.LogInformation("GoShip token not found in cache or expired. Requesting new token.");

                // Get fresh token
                var (newToken, expiresIn) = await RequestNewTokenAsync();

                // Calculate expiry time
                var expiryTime = DateTime.UtcNow.AddSeconds(expiresIn);

                // Create cache options
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(expiresIn - 60) // 1 minute before actual expiry
                };

                // Only try to cache if the previous cache operations succeeded
                if (cacheOperationSucceeded)
                {
                    currentRetry = 0;
                    delay = TimeSpan.FromSeconds(1);

                    while (currentRetry <= maxRetries)
                    {
                        try
                        {
                            // Use multiple Set operations in a more resilient way
                            await Task.WhenAll(
                                _cache.SetStringAsync(TokenCacheKey, newToken, cacheOptions),
                                _cache.SetStringAsync(ExpiryTimeCacheKey, expiryTime.ToString("o"), cacheOptions)
                            );

                            _logger.LogInformation("Successfully cached GoShip token. Expires at {ExpiryTime}", expiryTime);
                            break; // Success, exit retry loop
                        }
                        catch (Exception ex) when (ex is StackExchange.Redis.RedisConnectionException ||
                                                 ex is StackExchange.Redis.RedisTimeoutException ||
                                                 ex is System.Net.Sockets.SocketException)
                        {
                            if (currentRetry == maxRetries)
                            {
                                _logger.LogError(ex, "Failed to cache GoShip token after {RetryCount} retries", maxRetries);
                                _telemetryClient.TrackEvent("RedisCacheSetFailure", new Dictionary<string, string>
                            {
                                { "Operation", "SetToken" },
                                { "Error", ex.Message }
                            });
                                break;
                            }

                            currentRetry++;
                            _logger.LogWarning(ex, "Redis cache write failed (attempt {RetryCount}/{MaxRetries}). Retrying...",
                                currentRetry, maxRetries);

                            await Task.Delay(delay);
                            delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, currentRetry))); // Exponential backoff
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Skipping token caching due to previous Redis failures");
                }

                accessToken = newToken;
            }

            return accessToken ?? throw new InvalidOperationException("Failed to obtain a valid GoShip access token");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting valid GoShip token");
            _telemetryClient.TrackException(ex, new Dictionary<string, string> {
            { "Method", "GetAccessTokenAsync" }
        });

            // Try direct token acquisition as fallback
            try
            {
                _logger.LogWarning("Attempting emergency direct token acquisition");
                var (emergencyToken, _) = await RequestNewTokenAsync();
                return emergencyToken;
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Emergency token acquisition failed");
                throw; // Re-throw the original exception
            }
        }
    }

    private async Task<(string Token, int ExpiresIn)> RequestNewTokenAsync()
    {
        using var operation = _telemetryClient.StartOperation<Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry>("RequestNewGoShipToken");
        try
        {
            var apiSecret = _options.ApiSecret;
            var username = apiSecret.Username;
            var password = apiSecret.Password;
            var clientId = apiSecret.ClientId;
            var clientSecret = apiSecret.ClientSecret;

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) ||
                string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("GoShip configuration is incomplete in application settings");
            }

            var authRequest = new
            {
                grant_type = "password",
                client_id = clientId,
                client_secret = clientSecret,
                username = username,
                password = password
            };

            // Make authentication request to GoShip API
            var response = await _httpClient.PostAsJsonAsync("login", authRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to authenticate with GoShip API. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode, errorContent);
                throw new Exception($"Failed to authenticate with GoShip API: {response.StatusCode}");
            }

            var authResult = await response.Content.ReadFromJsonAsync<GoshipAuthResponse>();

            if (authResult == null || string.IsNullOrEmpty(authResult.access_token))
            {
                throw new Exception("Invalid authentication response from GoShip API");
            }

            return (authResult.access_token, authResult.expires_in);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while requesting new GoShip token");
            _telemetryClient.TrackException(ex);
            throw;
        }
    }

    public async Task<object> GetCitiesAsync()
    {
        var citiesResult = await SendAsync<GoshipResponse>(HttpMethod.Get, "cities", null, "Failed to get cities");
        if (citiesResult == null)
        {
            throw new Exception("Invalid cities response from GoShip API");
        }
        if (citiesResult.data == null)
        {
            throw new Exception("Cities data is missing in the GoShip API response");
        }
        return citiesResult.data;
    }

    public async Task<object> GetDistrictsAsync(string cityId)
    {
        var districtsResult = await SendAsync<GoshipResponse>(HttpMethod.Get, $"cities/{cityId}/districts", null, "Failed to get districts");
        if (districtsResult == null)
        {
            throw new Exception("Invalid districts response from GoShip API");
        }
        if (districtsResult.data == null)
        {
            throw new Exception("Districts data is missing in the GoShip API response");
        }
        return districtsResult.data;
    }

    public async Task<object> GetWardsAsync(string districtId)
    {
        var wardsResult = await SendAsync<GoshipResponse>(HttpMethod.Get, $"districts/{districtId}/wards", null, "Failed to get wards");
        if (wardsResult == null)
        {
            throw new Exception("Invalid wards response from GoShip API");
        }
        if (wardsResult.data == null)
        {
            throw new Exception("Wards data is missing in the GoShip API response");
        }
        return wardsResult.data;
    }

    public async Task<List<object>> GetRatesAsync(object ratesRequest)
    {
        var shippingCostResult = await SendAsync<GoshipShippingCostResponse>(HttpMethod.Post, "rates", ratesRequest, "Failed to calculate shipping cost");
        if (shippingCostResult == null)
        {
            throw new Exception("Invalid shipping cost response from GoShip API");
        }
        if (shippingCostResult.data == null)
        {
            throw new Exception("Shipping cost data is missing in the GoShip API response");
        }
        return shippingCostResult.data;
    }

    public async Task<GoshipCreateShipmentResponse> CreateShipmentAsync(object shipmentRequest)
    {
        var shipmentResult = await SendAsync<GoshipCreateShipmentResponse>(HttpMethod.Post, "shipments", shipmentRequest, "Failed to create shipment");
        if (shipmentResult == null)
        {
            throw new Exception("Invalid shipment response from GoShip API");
        }
        return shipmentResult;
    }

    /// <summary>
    /// Sends an authenticated request. On a non-success status, logs the response body and throws
    /// <c>"{failureMessage}: {body}"</c>.
    /// </summary>
    private async Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string requestUri, object? body, string failureMessage)
    {
        var token = await GetAccessTokenAsync();

        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("{FailureMessage}. Status: {StatusCode}, Error: {Error}",
                failureMessage, response.StatusCode, errorContent);
            throw new Exception($"{failureMessage}: {errorContent}");
        }

        return await response.Content.ReadFromJsonAsync<TResponse>();
    }
}
