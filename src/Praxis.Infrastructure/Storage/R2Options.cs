namespace Praxis.Infrastructure.Storage;

public class R2Options
{
    public const string SectionName = "R2";

    public string AccountId { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "file-praxis-sandbox";

    public int UploadUrlExpirationMinutes { get; set; } = 15;
    public int DownloadUrlExpirationMinutes { get; set; } = 15;
    public long MaxImageSizeBytes { get; set; } = 5 * 1024 * 1024; // 5 MB
    public long MaxPdfSizeBytes { get; set; } = 10 * 1024 * 1024;  // 10 MB

    public string ServiceUrl => !string.IsNullOrWhiteSpace(AccountId)
        ? $"https://{AccountId.Trim()}.r2.cloudflarestorage.com"
        : string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountId) &&
        !string.IsNullOrWhiteSpace(AccessKey) &&
        !string.IsNullOrWhiteSpace(SecretKey) &&
        !string.IsNullOrWhiteSpace(BucketName);
}
