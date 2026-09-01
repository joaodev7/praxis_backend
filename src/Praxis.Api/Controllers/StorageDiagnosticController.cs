using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Praxis.Infrastructure.Storage;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text;

namespace Praxis.Api.Controllers;

[ApiController]
[Route("api/admin/storage")]
public class StorageDiagnosticController : ControllerBase
{
    private readonly IAmazonS3 _s3Client;
    private readonly R2Options _options;
    private readonly ILogger<StorageDiagnosticController> _logger;

    public StorageDiagnosticController(
        IAmazonS3 s3Client,
        IOptions<R2Options> options,
        ILogger<StorageDiagnosticController> logger)
    {
        _s3Client = s3Client;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint de diagnóstico profundo de infraestrutura (Runtime, DNS, TCP, TLS 1.2/1.3, SslStream, HttpClient, Proxy e AWS SDK).
    /// </summary>
    [HttpGet("test")]
    public async Task<IActionResult> RunFullDiagnostic([FromQuery] string? objectKeyToCheck, CancellationToken cancellationToken)
    {
        var rawAccountId = _options.AccountId?.Trim() ?? string.Empty;
        var host = !string.IsNullOrWhiteSpace(rawAccountId)
            ? $"{rawAccountId}.r2.cloudflarestorage.com"
            : "r2.cloudflarestorage.com";

        var result = new DiagnosticResult
        {
            Timestamp = DateTime.UtcNow,
            TargetHost = host,
            BucketConfigured = _options.BucketName,
            ServiceUrlConfigured = _options.ServiceUrl,
            AccountIdPresent = !string.IsNullOrWhiteSpace(_options.AccountId),
            AccountIdLength = rawAccountId.Length,
            AccessKeyPresent = !string.IsNullOrWhiteSpace(_options.AccessKey),
            AccessKeyPrefix = !string.IsNullOrWhiteSpace(_options.AccessKey) && _options.AccessKey.Length >= 4
                ? _options.AccessKey[..4] + "..."
                : "(ausente)",
            SecretKeyPresent = !string.IsNullOrWhiteSpace(_options.SecretKey),
            IsConfigured = _options.IsConfigured
        };

        // 1. Runtime
        result.Runtime = new RuntimeInfo
        {
            DotNetVersion = Environment.Version.ToString(),
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            OSDescription = RuntimeInformation.OSDescription,
            OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            OSVersion = Environment.OSVersion.ToString()
        };

        // 2. Proxy detection (Booleans only, never values)
        result.Proxy = new ProxyInfo
        {
            HttpProxyPresent = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HTTP_PROXY")) ||
                               !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("http_proxy")),
            HttpsProxyPresent = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HTTPS_PROXY")) ||
                                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("https_proxy")),
            AllProxyPresent = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALL_PROXY")) ||
                              !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("all_proxy")),
            NoProxyPresent = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NO_PROXY")) ||
                             !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("no_proxy")),
            DefaultProxyConfigured = HttpClient.DefaultProxy != null
        };

        // 3. DNS Resolution
        var resolvedAddresses = new List<IPAddress>();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            foreach (var addr in addresses)
            {
                resolvedAddresses.Add(addr);
                result.DnsResults.Add(new DnsEntry
                {
                    Ip = addr.ToString(),
                    AddressFamily = addr.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6"
                });
            }
        }
        catch (Exception ex)
        {
            result.DnsError = ex.Message;
        }

        // 4. TCP Connectivity (Port 443 for each resolved IP)
        foreach (var ipAddr in resolvedAddresses)
        {
            var sw = Stopwatch.StartNew();
            using var tcpClient = new TcpClient(ipAddr.AddressFamily);
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
                await tcpClient.ConnectAsync(ipAddr, 443, linkedCts.Token);
                sw.Stop();

                result.TcpTests.Add(new TcpTestResult
                {
                    Ip = ipAddr.ToString(),
                    AddressFamily = ipAddr.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6",
                    Success = tcpClient.Connected,
                    ElapsedMs = sw.ElapsedMilliseconds
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.TcpTests.Add(new TcpTestResult
                {
                    Ip = ipAddr.ToString(),
                    AddressFamily = ipAddr.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6",
                    Success = false,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    ErrorType = ex.GetType().FullName,
                    ErrorMessage = ex.Message
                });
            }
        }

        // 5. TLS 1.2 Handshake Test via SslStream
        result.Tls12 = await TestTlsHandshakeAsync(host, SslProtocols.Tls12, null, cancellationToken);

        // 6. TLS 1.3 Handshake Test via SslStream
        result.Tls13 = await TestTlsHandshakeAsync(host, SslProtocols.Tls13, null, cancellationToken);

        // 7. HttpClient Simples (GET https://{host})
        result.HttpClientTest = await TestHttpClientAsync(host, cancellationToken);

        // 8. IPv4 Teste Separado (se houver IPv4)
        var ipv4 = resolvedAddresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        if (ipv4 != null)
        {
            result.Ipv4Test = await TestTlsHandshakeAsync(host, SslProtocols.None, ipv4, cancellationToken);
            result.Ipv4HttpTest = await TestHttpClientSpecificIpAsync(host, ipv4, cancellationToken);
        }

        // 9. IPv6 Teste Separado (se houver IPv6)
        var ipv6 = resolvedAddresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6);
        if (ipv6 != null)
        {
            result.Ipv6Test = await TestTlsHandshakeAsync(host, SslProtocols.None, ipv6, cancellationToken);
            result.Ipv6HttpTest = await TestHttpClientSpecificIpAsync(host, ipv6, cancellationToken);
        }

        // 10. AWS SDK Direct PutObject & Full Exception Chain
        if (_options.IsConfigured)
        {
            var testKey = $"system/diagnostic/backend-direct-test-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
            var testBytes = Encoding.UTF8.GetBytes($"PRAXIS R2 Diagnostic Test at {DateTime.UtcNow:O}");

            try
            {
                using var memoryStream = new MemoryStream(testBytes);
                var putRequest = new PutObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = testKey,
                    InputStream = memoryStream,
                    ContentType = "text/plain"
                };

                var putResponse = await _s3Client.PutObjectAsync(putRequest, cancellationToken);
                result.AwsSdkPutSuccess = (int)putResponse.HttpStatusCode >= 200 && (int)putResponse.HttpStatusCode < 300;
                result.AwsSdkPutStatusCode = (int)putResponse.HttpStatusCode;
                result.AwsSdkPutETag = putResponse.ETag;

                // Cleanup
                await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = testKey
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                result.AwsSdkPutSuccess = false;
                result.AwsSdkPutExceptionChain = GetExceptionChain(ex);
                _logger.LogError(ex, "Erro no teste PutObject do AWS SDK no diagnóstico R2");
            }
        }

        // 11. Verificação de ObjectKey específico (se solicitado)
        if (!string.IsNullOrWhiteSpace(objectKeyToCheck) && _options.IsConfigured)
        {
            try
            {
                var checkRequest = new GetObjectMetadataRequest
                {
                    BucketName = _options.BucketName,
                    Key = objectKeyToCheck.Trim()
                };

                var checkResponse = await _s3Client.GetObjectMetadataAsync(checkRequest, cancellationToken);
                result.QueriedObjectResult = new ObjectCheckResult
                {
                    ObjectKey = objectKeyToCheck,
                    Exists = true,
                    ContentLength = checkResponse.ContentLength,
                    ContentType = checkResponse.Headers.ContentType,
                    ETag = checkResponse.ETag,
                    LastModified = checkResponse.LastModified
                };
            }
            catch (AmazonS3Exception ex)
            {
                result.QueriedObjectResult = new ObjectCheckResult
                {
                    ObjectKey = objectKeyToCheck,
                    Exists = false,
                    HttpStatusCode = (int)ex.StatusCode,
                    ErrorCode = ex.ErrorCode,
                    ErrorMessage = ex.Message
                };
            }
            catch (Exception ex)
            {
                result.QueriedObjectResult = new ObjectCheckResult
                {
                    ObjectKey = objectKeyToCheck,
                    Exists = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        return Ok(result);
    }

    private static async Task<TlsHandshakeResult> TestTlsHandshakeAsync(
        string host,
        SslProtocols protocols,
        IPAddress? targetIp,
        CancellationToken cancellationToken)
    {
        var result = new TlsHandshakeResult();
        using var tcpClient = targetIp != null
            ? new TcpClient(targetIp.AddressFamily)
            : new TcpClient();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(7));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            if (targetIp != null)
            {
                await tcpClient.ConnectAsync(targetIp, 443, linkedCts.Token);
            }
            else
            {
                await tcpClient.ConnectAsync(host, 443, linkedCts.Token);
            }

            using var sslStream = new SslStream(tcpClient.GetStream(), false);
            var authOptions = new SslClientAuthenticationOptions
            {
                TargetHost = host, // Envia o SNI correto
                EnabledSslProtocols = protocols == SslProtocols.None ? SslProtocols.Tls12 | SslProtocols.Tls13 : protocols
            };

            await sslStream.AuthenticateAsClientAsync(authOptions, linkedCts.Token);

            result.Success = true;
            result.Protocol = sslStream.SslProtocol.ToString();
            result.CipherAlgorithm = sslStream.CipherAlgorithm.ToString();
            result.CipherStrength = sslStream.CipherStrength;
            result.HashAlgorithm = sslStream.HashAlgorithm.ToString();
            result.HashStrength = sslStream.HashStrength;
            result.KeyExchangeAlgorithm = sslStream.KeyExchangeAlgorithm.ToString();
            result.NegotiatedCipherSuite = sslStream.NegotiatedCipherSuite.ToString();
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorType = ex.GetType().FullName;
            result.ErrorMessage = ex.Message;
            result.InnerExceptionType = ex.InnerException?.GetType().FullName;
            result.InnerExceptionMessage = ex.InnerException?.Message;
            result.ExceptionChain = GetExceptionChain(ex);
        }

        return result;
    }

    private static async Task<HttpClientTestResult> TestHttpClientAsync(string host, CancellationToken cancellationToken)
    {
        var result = new HttpClientTestResult();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(7) };

        try
        {
            // Requisição simples GET para testar se o handshake TLS completa
            var response = await httpClient.GetAsync($"https://{host}", cancellationToken);
            result.Success = true; // Qualquer status HTTP retornado prova que o handshake TLS funcionou
            result.StatusCode = (int)response.StatusCode;
            result.ReasonPhrase = response.ReasonPhrase;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorType = ex.GetType().FullName;
            result.ErrorMessage = ex.Message;
            result.InnerExceptionType = ex.InnerException?.GetType().FullName;
            result.InnerExceptionMessage = ex.InnerException?.Message;
            result.ExceptionChain = GetExceptionChain(ex);
        }

        return result;
    }

    private static async Task<HttpClientTestResult> TestHttpClientSpecificIpAsync(string host, IPAddress ip, CancellationToken cancellationToken)
    {
        var result = new HttpClientTestResult();
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, ct) =>
            {
                var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(new IPEndPoint(ip, 443), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
        };

        using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(7) };

        try
        {
            var response = await httpClient.GetAsync($"https://{host}", cancellationToken);
            result.Success = true;
            result.StatusCode = (int)response.StatusCode;
            result.ReasonPhrase = response.ReasonPhrase;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorType = ex.GetType().FullName;
            result.ErrorMessage = ex.Message;
            result.InnerExceptionType = ex.InnerException?.GetType().FullName;
            result.InnerExceptionMessage = ex.InnerException?.Message;
            result.ExceptionChain = GetExceptionChain(ex);
        }

        return result;
    }

    private static List<ExceptionDetail> GetExceptionChain(Exception? ex)
    {
        var chain = new List<ExceptionDetail>();
        while (ex != null)
        {
            chain.Add(new ExceptionDetail
            {
                Type = ex.GetType().FullName ?? ex.GetType().Name,
                Message = ex.Message,
                Source = ex.Source
            });
            ex = ex.InnerException;
        }
        return chain;
    }
}

public class DiagnosticResult
{
    public DateTime Timestamp { get; set; }
    public string TargetHost { get; set; } = string.Empty;
    public string BucketConfigured { get; set; } = string.Empty;
    public string ServiceUrlConfigured { get; set; } = string.Empty;
    public bool AccountIdPresent { get; set; }
    public int AccountIdLength { get; set; }
    public bool AccessKeyPresent { get; set; }
    public string AccessKeyPrefix { get; set; } = string.Empty;
    public bool SecretKeyPresent { get; set; }
    public bool IsConfigured { get; set; }

    public RuntimeInfo? Runtime { get; set; }
    public ProxyInfo? Proxy { get; set; }

    public List<DnsEntry> DnsResults { get; set; } = new();
    public string? DnsError { get; set; }

    public List<TcpTestResult> TcpTests { get; set; } = new();

    public TlsHandshakeResult? Tls12 { get; set; }
    public TlsHandshakeResult? Tls13 { get; set; }

    public HttpClientTestResult? HttpClientTest { get; set; }

    public TlsHandshakeResult? Ipv4Test { get; set; }
    public HttpClientTestResult? Ipv4HttpTest { get; set; }

    public TlsHandshakeResult? Ipv6Test { get; set; }
    public HttpClientTestResult? Ipv6HttpTest { get; set; }

    public bool AwsSdkPutSuccess { get; set; }
    public int? AwsSdkPutStatusCode { get; set; }
    public string? AwsSdkPutETag { get; set; }
    public List<ExceptionDetail>? AwsSdkPutExceptionChain { get; set; }

    public ObjectCheckResult? QueriedObjectResult { get; set; }
}

public class RuntimeInfo
{
    public string DotNetVersion { get; set; } = string.Empty;
    public string FrameworkDescription { get; set; } = string.Empty;
    public string OSDescription { get; set; } = string.Empty;
    public string OSArchitecture { get; set; } = string.Empty;
    public string ProcessArchitecture { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
}

public class ProxyInfo
{
    public bool HttpProxyPresent { get; set; }
    public bool HttpsProxyPresent { get; set; }
    public bool AllProxyPresent { get; set; }
    public bool NoProxyPresent { get; set; }
    public bool DefaultProxyConfigured { get; set; }
}

public class DnsEntry
{
    public string Ip { get; set; } = string.Empty;
    public string AddressFamily { get; set; } = string.Empty;
}

public class TcpTestResult
{
    public string Ip { get; set; } = string.Empty;
    public string AddressFamily { get; set; } = string.Empty;
    public bool Success { get; set; }
    public long ElapsedMs { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TlsHandshakeResult
{
    public bool Success { get; set; }
    public string? Protocol { get; set; }
    public string? CipherAlgorithm { get; set; }
    public int? CipherStrength { get; set; }
    public string? HashAlgorithm { get; set; }
    public int? HashStrength { get; set; }
    public string? KeyExchangeAlgorithm { get; set; }
    public string? NegotiatedCipherSuite { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public string? InnerExceptionType { get; set; }
    public string? InnerExceptionMessage { get; set; }
    public List<ExceptionDetail>? ExceptionChain { get; set; }
}

public class HttpClientTestResult
{
    public bool Success { get; set; }
    public int? StatusCode { get; set; }
    public string? ReasonPhrase { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public string? InnerExceptionType { get; set; }
    public string? InnerExceptionMessage { get; set; }
    public List<ExceptionDetail>? ExceptionChain { get; set; }
}

public class ExceptionDetail
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
}

public class ObjectCheckResult
{
    public string ObjectKey { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public long? ContentLength { get; set; }
    public string? ContentType { get; set; }
    public string? ETag { get; set; }
    public DateTime? LastModified { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
