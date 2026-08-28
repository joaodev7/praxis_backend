using System.Net;
using System.Text.Json;

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

        context.Response.StatusCode = statusCode;

        string message;
        string? detailed = null;

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
