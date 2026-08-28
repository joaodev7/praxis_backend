namespace Praxis.Application.Interfaces;

public interface IAsaasWebhookService
{
    Task<bool> ProcessWebhookAsync(string webhookTokenHeader, string payloadJson, CancellationToken ct = default);
}
