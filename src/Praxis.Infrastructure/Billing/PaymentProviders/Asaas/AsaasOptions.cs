namespace Praxis.Infrastructure.Billing.PaymentProviders.Asaas;

public class AsaasOptions
{
    public const string SectionName = "PaymentProviders:Asaas";

    public string Environment { get; set; } = "Sandbox"; // "Sandbox" | "Production"
    public string ApiKey { get; set; } = string.Empty;
    public string WebhookToken { get; set; } = string.Empty;

    public string BaseUrl => Environment.Equals("Production", StringComparison.OrdinalIgnoreCase)
        ? "https://api.asaas.com/v3"
        : "https://sandbox.asaas.com/api/v3";
}
