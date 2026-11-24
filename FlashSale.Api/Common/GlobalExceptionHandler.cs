using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FlashSale.Api.Common;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
: IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

            var validationProblem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Doğrulama Hatası.",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Gönderilen bir veya daha fazla alan iş kurallarına uymuyor.",
                Instance = httpContext.Request.Path,
                Extensions = { ["errors"] = errors }
            };
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(validationProblem, cancellationToken);
            return true;
        }
        if (exception is BadHttpRequestException badHttpRequestEx)
        {
            var badRequestProblem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Geçersiz İstek.",
                Status = StatusCodes.Status400BadRequest,
                Detail = badHttpRequestEx.Message,
                Instance = httpContext.Request.Path
            };
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(badRequestProblem, cancellationToken);
            return true;
        }
        logger.LogError(exception, "Beklenmedik sunucu hatası: {Message}", exception.Message);
        var serverProblem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Title = "Hata.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "beklenmedik bir hata oluştu.",
        };
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(serverProblem, cancellationToken);
        return true;
    }
}
