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

        var baseUrl = _options.BaseUrl.TrimEnd('/') + "/";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        _httpClient.DefaultRequestHeaders.Remove("User-Agent");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "PRAXIS-Nutri-Platform/1.0");

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("access_token");
            _httpClient.DefaultRequestHeaders.Add("access_token", _options.ApiKey);
        }
    }

    public async Task<TResponse?> GetAsync<TResponse>(string endpoint, CancellationToken ct = default)
    {
        var (result, _) = await GetWithResultAsync<TResponse>(endpoint, ct);
        return result;
    }

    public async Task<(TResponse? Result, string? ErrorMessage)> GetWithResultAsync<TResponse>(string endpoint, CancellationToken ct = default)
    {
        try
        {
            // Remove leading slash so relative URL resolution with BaseAddress works correctly
            var cleanEndpoint = endpoint.TrimStart('/');
            using var response = await _httpClient.GetAsync(cleanEndpoint, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Asaas GET {Endpoint} returned status {StatusCode}: {Content}", cleanEndpoint, response.StatusCode, content);
                return (default, ExtractErrorMessage(content, $"Erro {response.StatusCode} ao consultar Asaas"));
            }

            var parsed = JsonSerializer.Deserialize<TResponse>(content, JsonOptions);
            return (parsed, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling Asaas GET {Endpoint}", endpoint);
            return (default, ex.Message);
        }
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken ct = default)
    {
        var (result, _) = await PostWithResultAsync<TRequest, TResponse>(endpoint, request, ct);
        return result;
    }

    public async Task<(TResponse? Result, string? ErrorMessage)> PostWithResultAsync<TRequest, TResponse>(string endpoint, TRequest request, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var cleanEndpoint = endpoint.TrimStart('/');
            using var response = await _httpClient.PostAsync(cleanEndpoint, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Asaas POST {Endpoint} returned status {StatusCode}: {ResponseBody}", cleanEndpoint, response.StatusCode, responseBody);
                return (default, ExtractErrorMessage(responseBody, $"Erro {response.StatusCode} no Asaas"));
            }

            var parsed = JsonSerializer.Deserialize<TResponse>(responseBody, JsonOptions);
            return (parsed, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling Asaas POST {Endpoint}", endpoint);
            return (default, ex.Message);
        }
    }

    public async Task<bool> DeleteAsync(string endpoint, CancellationToken ct = default)
    {
        try
        {
            var cleanEndpoint = endpoint.TrimStart('/');
            using var response = await _httpClient.DeleteAsync(cleanEndpoint, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling Asaas DELETE {Endpoint}", endpoint);
            return false;
        }
    }

    private static string ExtractErrorMessage(string responseBody, string fallback)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return fallback;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Array)
            {
                var errorList = new List<string>();
                foreach (var err in errorsProp.EnumerateArray())
                {
                    if (err.TryGetProperty("description", out var desc))
                    {
                        var d = desc.GetString();
                        if (!string.IsNullOrWhiteSpace(d)) errorList.Add(d);
                    }
                }
                if (errorList.Count > 0)
                {
                    return string.Join("; ", errorList);
                }
            }
        }
        catch
        {
            // Ignore parse errors on fallback
        }
        return fallback;
    }
}
