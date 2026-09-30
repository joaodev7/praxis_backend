using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Praxis.Api.Middlewares;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var statusCode = exception switch
        {
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            InvalidOperationException => (int)HttpStatusCode.BadRequest,
            ArgumentException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };

        string message = exception.Message;
        string? detailed = null;

        if (exception is DbUpdateException dbEx)
        {
            var innerMsg = dbEx.InnerException?.Message ?? string.Empty;
            var isPostgresUnique = dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == "23505";
            var isDuplicate = isPostgresUnique || innerMsg.Contains("23505") || innerMsg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);

            if (isDuplicate)
            {
                statusCode = (int)HttpStatusCode.Conflict;

                if (innerMsg.Contains("IX_Users_Email", StringComparison.OrdinalIgnoreCase))
                {
                    message = "O e-mail informado já está em uso por outro usuário no sistema.";
                }
                else if (innerMsg.Contains("IX_Tenants_Cnpj", StringComparison.OrdinalIgnoreCase))
                {
                    message = "O CNPJ informado já está cadastrado por outra organização.";
                }
                else
                {
                    message = "Já existe um registro cadastrado com os dados informados.";
                }
            }
        }

        if (statusCode == (int)HttpStatusCode.InternalServerError)
        {
            if (_env.IsDevelopment())
            {
                message = exception.Message;
                detailed = exception.InnerException?.Message;
            }
            else
            {
                message = "Ocorreu um erro interno no servidor. Se o problema persistir, contate o suporte.";
            }
        }
        else
        {
            message = exception.Message;
            if (_env.IsDevelopment())
            {
                detailed = exception.InnerException?.Message;
            }
        }

        var response = new
        {
            statusCode,
            message,
            detailed
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
