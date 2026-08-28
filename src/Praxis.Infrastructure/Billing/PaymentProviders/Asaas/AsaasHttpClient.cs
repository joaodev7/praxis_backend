using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Praxis.Infrastructure.Billing.PaymentProviders.Asaas;

public class AsaasHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly AsaasOptions _options;
    private readonly ILogger<AsaasHttpClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    public AsaasHttpClient(HttpClient httpClient, IOptions<AsaasOptions> options, ILogger<AsaasHttpClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("access_token");
            _httpClient.DefaultRequestHeaders.Add("access_token", _options.ApiKey);
        }
    }

    public async Task<TResponse?> GetAsync<TResponse>(string endpoint, CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(endpoint, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Asaas GET {Endpoint} returned status {StatusCode}: {Content}", endpoint, response.StatusCode, content);
                return default;
            }

            return JsonSerializer.Deserialize<TResponse>(content, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling Asaas GET {Endpoint}", endpoint);
            return default;
        }
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(endpoint, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Asaas POST {Endpoint} returned status {StatusCode}: {ResponseBody}", endpoint, response.StatusCode, responseBody);
                return default;
            }

            return JsonSerializer.Deserialize<TResponse>(responseBody, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling Asaas POST {Endpoint}", endpoint);
            return default;
        }
    }

    public async Task<bool> DeleteAsync(string endpoint, CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.DeleteAsync(endpoint, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling Asaas DELETE {Endpoint}", endpoint);
            return false;
        }
    }
}
