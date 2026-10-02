using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Praxis.Application.Interfaces;

namespace Praxis.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink, CancellationToken cancellationToken = default)
    {
        var subject = "Recuperação de Senha - PRAXIS";
        var htmlBody = BuildPasswordResetHtmlTemplate(userName, resetLink);

        if (!_options.IsConfigured)
        {
            _logger.LogInformation(
                "\n=======================================================\n" +
                "[PRAXIS - RECUPERAÇÃO DE SENHA (SIMULADO / STAGE)]\n" +
                "Destinatário: {UserName} <{ToEmail}>\n" +
                "Link de Redefinição: {ResetLink}\n" +
                "Validade: 2 horas\n" +
                "=======================================================\n",
                userName, toEmail, resetLink);
            return;
        }

        await SendEmailAsync(toEmail, subject, htmlBody, cancellationToken);
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning(
                "[EMAIL SIMULADO] Provedor SMTP não configurado. E-mail para {ToEmail} com assunto '{Subject}' não foi enviado fisicamente.",
                toEmail, subject);
            return;
        }

        try
        {
            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                Credentials = new NetworkCredential(_options.SmtpUser, _options.SmtpPass),
                EnableSsl = _options.EnableSsl
            };

            var message = new MailMessage
            {
                From = new MailAddress(_options.FromEmail, _options.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            message.To.Add(toEmail);

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("E-mail '{Subject}' enviado com sucesso para {ToEmail}.", subject, toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar e-mail '{Subject}' para {ToEmail}.", subject, toEmail);
            throw new InvalidOperationException("Não foi possível enviar o e-mail de recuperação. Tente novamente mais tarde.", ex);
        }
    }

    private static string BuildPasswordResetHtmlTemplate(string userName, string resetLink)
    {
        return $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0""/>
  <title>Recuperação de Senha - PRAXIS</title>
  <style>
    body {{
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      background-color: #f8fafc;
      color: #1e293b;
      margin: 0;
      padding: 0;
      line-height: 1.6;
    }}
    .container {{
      max-width: 580px;
      margin: 40px auto;
      background-color: #ffffff;
      border-radius: 12px;
      overflow: hidden;
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05), 0 2px 4px -1px rgba(0, 0, 0, 0.03);
      border: 1px solid #e2e8f0;
    }}
    .header {{
      background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%);
      padding: 32px;
      text-align: center;
      border-bottom: 3px solid #2563eb;
    }}
    .header h1 {{
      margin: 0;
      color: #ffffff;
      font-size: 24px;
      letter-spacing: 2px;
      font-weight: 800;
    }}
    .header p {{
      margin: 6px 0 0 0;
      color: #94a3b8;
      font-size: 11px;
      text-transform: uppercase;
      letter-spacing: 1.5px;
    }}
    .content {{
      padding: 36px 32px;
    }}
    .greeting {{
      font-size: 18px;
      font-weight: 700;
      color: #0f172a;
      margin-bottom: 16px;
    }}
    .text {{
      font-size: 15px;
      color: #475569;
      margin-bottom: 24px;
    }}
    .btn-container {{
      text-align: center;
      margin: 32px 0;
    }}
    .btn {{
      display: inline-block;
      background-color: #2563eb;
      color: #ffffff !important;
      text-decoration: none;
      font-size: 15px;
      font-weight: 600;
      padding: 14px 32px;
      border-radius: 8px;
      box-shadow: 0 4px 12px rgba(37, 99, 235, 0.25);
    }}
    .warning {{
      background-color: #f1f5f9;
      border-left: 4px solid #f59e0b;
      padding: 14px 16px;
      border-radius: 4px;
      font-size: 13px;
      color: #64748b;
      margin: 24px 0;
    }}
    .link-alt {{
      font-size: 12px;
      color: #94a3b8;
      word-break: break-all;
      margin-top: 20px;
    }}
    .footer {{
      background-color: #f8fafc;
      padding: 24px 32px;
      text-align: center;
      border-top: 1px solid #e2e8f0;
      font-size: 12px;
      color: #94a3b8;
    }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>PRAXIS</h1>
      <p>Inteligência em Ação</p>
    </div>
    <div class=""content"">
      <div class=""greeting"">Olá, {WebUtility.HtmlEncode(userName)}!</div>
      <p class=""text"">
        Recebemos uma solicitação para redefinir a senha de acesso à sua conta na plataforma <strong>PRAXIS</strong>.
      </p>
      <div class=""btn-container"">
        <a href=""{resetLink}"" class=""btn"" target=""_blank"">Redefinir Minha Senha</a>
      </div>
      <div class=""warning"">
        <strong>Atenção:</strong> Por motivos de segurança, este link é de uso único e expira em <strong>2 horas</strong>.
      </div>
      <p class=""text"" style=""font-size: 13px;"">
        Se você não solicitou a redefinição de senha, nenhuma ação é necessária. Sua senha atual permanecerá segura.
      </p>
      <div class=""link-alt"">
        Se o botão acima não funcionar, copie e cole o seguinte link no seu navegador:<br/>
        <a href=""{resetLink}"" style=""color: #2563eb;"">{resetLink}</a>
      </div>
    </div>
    <div class=""footer"">
      &copy; {DateTime.UtcNow.Year} PRAXIS - Gestão de Responsabilidade Técnica e Nutrição. Todos os direitos reservados.
    </div>
  </div>
</body>
</html>";
    }
}
