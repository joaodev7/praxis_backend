using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Praxis.Infrastructure.Storage;
using System.Collections;
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
    /// Endpoint de diagnóstico comparativo de infraestrutura e TLS entre o endpoint específico da conta e o endpoint genérico do R2.
    /// </summary>
    [HttpGet("test")]
    public async Task<IActionResult> RunFullDiagnostic([FromQuery] string? objectKeyToCheck, CancellationToken cancellationToken)
    {
        var rawAccountId = _options.AccountId?.Trim() ?? string.Empty;
        var accountHost = !string.IsNullOrWhiteSpace(rawAccountId)
            ? $"{rawAccountId}.r2.cloudflarestorage.com"
            : "3c73a89fae6057f74c8b82b6fd1813d1.r2.cloudflarestorage.com";
        var genericHost = "r2.cloudflarestorage.com";

        var result = new ComparativeDiagnosticResult
        {
            Timestamp = DateTime.UtcNow,
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

        // 1. Runtime & OS
        result.Runtime = new RuntimeInfo
        {
            DotNetVersion = Environment.Version.ToString(),
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            OSDescription = RuntimeInformation.OSDescription,
            OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            OSVersion = Environment.OSVersion.ToString()
        };

        // 2. OpenSSL & OS Filesystem Inspection
        result.OpenSslConfig = ReadOpenSslConfiguration();

        // 3. Environment Variables Audit (Key names and presence only, NEVER secret values)
        result.TlsEnvironmentVariables = AuditTlsEnvironmentVariables();

        // 4. Proxy Detection (Booleans only)
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

        // 5. OpenSSL CLI Version & Ciphers
        result.OpenSslVersionCli = await RunProcessAsync("openssl", "version -a", 5000);
        result.OpenSslCiphersCli = await RunProcessAsync("openssl", "ciphers -v", 5000);

        // 6. TARGET 1: Endpoint Específico da Conta ({AccountId}.r2.cloudflarestorage.com)
        result.Target1_AccountSpecific = await DiagnoseTargetAsync(accountHost, cancellationToken);

        // 7. TARGET 2: Endpoint Genérico (r2.cloudflarestorage.com)
        result.Target2_GenericR2 = await DiagnoseTargetAsync(genericHost, cancellationToken);

        // 8. AWS SDK Direct PutObject & Full Exception Chain (apenas se configurado)
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

        // 9. Verificação de ObjectKey específico (se solicitado)
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

    private static async Task<TargetDiagnosticResult> DiagnoseTargetAsync(string host, CancellationToken cancellationToken)
    {
        var targetResult = new TargetDiagnosticResult { Host = host };

        // 1. DNS
        var resolvedAddresses = new List<IPAddress>();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            foreach (var addr in addresses)
            {
                resolvedAddresses.Add(addr);
                targetResult.DnsResults.Add(new DnsEntry
                {
                    Ip = addr.ToString(),
                    AddressFamily = addr.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6"
                });
            }
        }
        catch (Exception ex)
        {
            targetResult.DnsError = ex.Message;
        }

        // 2. TCP 443 Connectivity para cada IP
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

                targetResult.TcpTests.Add(new TcpTestResult
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
                targetResult.TcpTests.Add(new TcpTestResult
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

        // 3. OpenSSL s_client TLS 1.2
        targetResult.OpenSslSClientTls12 = await RunProcessAsync("openssl", $"s_client -servername {host} -connect {host}:443 -tls1_2", 7000);

        // 4. OpenSSL s_client Default (TLS 1.3 / Auto)
        targetResult.OpenSslSClientDefault = await RunProcessAsync("openssl", $"s_client -servername {host} -connect {host}:443", 7000);

        // 5. OpenSSL s_client Brief IPv4
        targetResult.OpenSslSClientBrief = await RunProcessAsync("openssl", $"s_client -brief -4 -connect {host}:443 -servername {host}", 7000);

        // 6. Curl IPv4
        targetResult.CurlTest = await RunProcessAsync("curl", $"-4 -Iv https://{host}", 7000);

        // 7. Managed SslStream TLS 1.2
        targetResult.ManagedTls12 = await TestTlsHandshakeAsync(host, SslProtocols.Tls12, null, cancellationToken);

        // 8. Managed SslStream TLS 1.3
        targetResult.ManagedTls13 = await TestTlsHandshakeAsync(host, SslProtocols.Tls13, null, cancellationToken);

        // 9. HttpClient Simples (GET https://{host})
        targetResult.HttpClientTest = await TestHttpClientAsync(host, cancellationToken);

        return targetResult;
    }

    private static async Task<ProcessExecResult> RunProcessAsync(string command, string arguments, int timeoutMs = 7000)
    {
        var res = new ProcessExecResult { Command = $"{command} {arguments}" };
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Fechar stdin imediatamente para comandos interativos como s_client
            try { process.StandardInput.Close(); } catch { }

            using var cts = new CancellationTokenSource(timeoutMs);
            await process.WaitForExitAsync(cts.Token);

            res.Success = process.ExitCode == 0;
            res.ExitCode = process.ExitCode;
            res.Output = outputBuilder.ToString().Trim();
            res.Error = errorBuilder.ToString().Trim();
        }
        catch (Exception ex)
        {
            res.Success = false;
            res.ExecutionException = $"{ex.GetType().Name}: {ex.Message}";
        }
        return res;
    }

    private static OpenSslConfigInfo ReadOpenSslConfiguration()
    {
        var info = new OpenSslConfigInfo();

        try
        {
            if (System.IO.File.Exists("/etc/os-release"))
            {
                info.OsRelease = System.IO.File.ReadAllText("/etc/os-release").Trim();
            }

            if (System.IO.File.Exists("/etc/ssl/openssl.cnf"))
            {
                var lines = System.IO.File.ReadAllLines("/etc/ssl/openssl.cnf");
                var relevantLines = lines
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
                    .Where(l => l.Contains("CipherString", StringComparison.OrdinalIgnoreCase) ||
                                l.Contains("SECLEVEL", StringComparison.OrdinalIgnoreCase) ||
                                l.Contains("MinProtocol", StringComparison.OrdinalIgnoreCase) ||
                                l.Contains("MaxProtocol", StringComparison.OrdinalIgnoreCase) ||
                                l.Contains("system_default", StringComparison.OrdinalIgnoreCase) ||
                                l.Contains("ssl_conf", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                info.OpenSslCnfRelevantSettings = relevantLines;
            }
            else
            {
                info.OpenSslCnfRelevantSettings = new List<string> { "Arquivo /etc/ssl/openssl.cnf não encontrado." };
            }
        }
        catch (Exception ex)
        {
            info.InspectionError = ex.Message;
        }

        return info;
    }

    private static List<EnvVarAuditEntry> AuditTlsEnvironmentVariables()
    {
        var entries = new List<EnvVarAuditEntry>();
        var envVars = Environment.GetEnvironmentVariables();

        var prefixes = new[] { "SSL", "TLS", "OPENSSL", "CURL", "HTTP", "HTTPS", "ALL", "NO", "DOTNET", "ASPNETCORE" };

        foreach (DictionaryEntry de in envVars)
        {
            var key = de.Key?.ToString() ?? string.Empty;
            var isMatch = prefixes.Any(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase));

            if (isMatch)
            {
                var val = de.Value?.ToString() ?? string.Empty;
                var isSecret = key.Contains("KEY", StringComparison.OrdinalIgnoreCase) ||
                               key.Contains("SECRET", StringComparison.OrdinalIgnoreCase) ||
                               key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) ||
                               key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase) ||
                               key.Contains("CONNECTION", StringComparison.OrdinalIgnoreCase);

                entries.Add(new EnvVarAuditEntry
                {
                    Key = key,
                    IsPresent = true,
                    Length = val.Length,
                    SafeValue = isSecret ? $"[PROTEGIDO - {val.Length} chars]" : (val.Length > 80 ? val[..80] + "..." : val)
                });
            }
        }

        return entries.OrderBy(e => e.Key).ToList();
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

public class ComparativeDiagnosticResult
{
    public DateTime Timestamp { get; set; }
    public string BucketConfigured { get; set; } = string.Empty;
    public string ServiceUrlConfigured { get; set; } = string.Empty;
    public bool AccountIdPresent { get; set; }
    public int AccountIdLength { get; set; }
    public bool AccessKeyPresent { get; set; }
    public string AccessKeyPrefix { get; set; } = string.Empty;
    public bool SecretKeyPresent { get; set; }
    public bool IsConfigured { get; set; }

    public RuntimeInfo? Runtime { get; set; }
    public OpenSslConfigInfo? OpenSslConfig { get; set; }
    public List<EnvVarAuditEntry> TlsEnvironmentVariables { get; set; } = new();
    public ProxyInfo? Proxy { get; set; }

    public ProcessExecResult? OpenSslVersionCli { get; set; }
    public ProcessExecResult? OpenSslCiphersCli { get; set; }

    public TargetDiagnosticResult? Target1_AccountSpecific { get; set; }
    public TargetDiagnosticResult? Target2_GenericR2 { get; set; }

    public bool AwsSdkPutSuccess { get; set; }
    public int? AwsSdkPutStatusCode { get; set; }
    public string? AwsSdkPutETag { get; set; }
    public List<ExceptionDetail>? AwsSdkPutExceptionChain { get; set; }

    public ObjectCheckResult? QueriedObjectResult { get; set; }
}

public class TargetDiagnosticResult
{
    public string Host { get; set; } = string.Empty;
    public List<DnsEntry> DnsResults { get; set; } = new();
    public string? DnsError { get; set; }

    public List<TcpTestResult> TcpTests { get; set; } = new();

    public ProcessExecResult? OpenSslSClientTls12 { get; set; }
    public ProcessExecResult? OpenSslSClientDefault { get; set; }
    public ProcessExecResult? OpenSslSClientBrief { get; set; }
    public ProcessExecResult? CurlTest { get; set; }

    public TlsHandshakeResult? ManagedTls12 { get; set; }
    public TlsHandshakeResult? ManagedTls13 { get; set; }

    public HttpClientTestResult? HttpClientTest { get; set; }
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

public class OpenSslConfigInfo
{
    public string? OsRelease { get; set; }
    public List<string> OpenSslCnfRelevantSettings { get; set; } = new();
    public string? InspectionError { get; set; }
}

public class EnvVarAuditEntry
{
    public string Key { get; set; } = string.Empty;
    public bool IsPresent { get; set; }
    public int Length { get; set; }
    public string SafeValue { get; set; } = string.Empty;
}

public class ProcessExecResult
{
    public string Command { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int? ExitCode { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public string? ExecutionException { get; set; }
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
