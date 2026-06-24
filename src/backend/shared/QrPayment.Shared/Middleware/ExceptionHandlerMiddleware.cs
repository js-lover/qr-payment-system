// QrPayment.Shared / Middleware / ExceptionHandlerMiddleware.cs
//
// Tüm servislerde kullanılan merkezi exception yakalama katmanı.
// BusinessRuleException türlerini yakalar ve RFC 7807 "Problem Details"
// formatında HTTP yanıtına dönüştürür.
//
// Middleware zincirindeki konumu: Pipeline'ın en dışında olmalı,
// tüm diğer middleware'lerden önce UseQrPaymentMiddleware() ile eklenir.
//
// İki senaryo:
//   1. BusinessRuleException → bilinen iş kuralı hatası, 4xx döner
//   2. Exception             → beklenmedik hata, 500 döner (detay loglanır)

using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using QrPayment.Shared.Exceptions;

namespace QrPayment.Shared.Middleware;

public class ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BusinessRuleException ex)
        {
            // Beklenen domain hataları — Warning seviyesinde loglanır
            logger.LogWarning(ex, "Business rule violation: {ErrorCode} on {Path}",
                ex.ErrorCode, context.Request.Path);
            await WriteProblemAsync(context, ex.HttpStatus, ex.ErrorType, ex.Title, ex.Message, ex.ErrorCode);
        }
        catch (Exception ex)
        {
            // Beklenmedik hatalar — Error seviyesinde loglanır, detay istemciye açılmaz
            logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, 500, "internal-server-error",
                "Internal Server Error", "An unexpected error occurred.", "INTERNAL_ERROR");
        }
    }

    /// <summary>RFC 7807 Problem Details formatında yanıt yazar.</summary>
    private static async Task WriteProblemAsync(
        HttpContext context, int status, string type, string title, string detail, string errorCode)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://qrpay.example.com/errors/{type}",
            title,
            status,
            detail,
            instance = context.Request.Path.Value,
            traceId = Activity.Current?.Id ?? context.TraceIdentifier,
            errorCode
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
