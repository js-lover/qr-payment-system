// QrPayment.Shared / Extensions / WebApplicationExtensions.cs
//
// Her servisin Program.cs dosyasında tek satırla ortak middleware
// ve health check endpoint'lerini aktive etmesini sağlayan uzantı metotları.
//
// Kullanım (Program.cs):
//   app.UseQrPaymentMiddleware();     // CorrelationId + ExceptionHandler
//   app.UseQrPaymentHealthChecks();   // /health ve /ready endpoint'leri
//   builder.Services.AddQrPaymentHealthChecks();  // DI kaydı
//
// /health → tüm check'ler; Kubernetes liveness probe
// /ready  → "ready" tag'li check'ler; Kubernetes readiness probe

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using QrPayment.Shared.Middleware;

namespace QrPayment.Shared.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// CorrelationId ve ExceptionHandler middleware'lerini pipeline'a ekler.
    /// Her servisin Program.cs'inde UseRouting() öncesinde çağrılmalı.
    /// </summary>
    public static IApplicationBuilder UseQrPaymentMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionHandlerMiddleware>();
        return app;
    }

    /// <summary>
    /// Kubernetes probe'ları için /health ve /ready endpoint'lerini açar.
    /// </summary>
    public static IApplicationBuilder UseQrPaymentHealthChecks(this IApplicationBuilder app)
    {
        // Liveness probe — servis ayakta mı?
        app.UseHealthChecks("/health", new HealthCheckOptions
        {
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = 200,
                [HealthStatus.Degraded] = 200,
                [HealthStatus.Unhealthy] = 503
            }
        });

        // Readiness probe — servis trafik almaya hazır mı? (DB bağlantısı vs.)
        app.UseHealthChecks("/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        return app;
    }

    /// <summary>
    /// Health check altyapısını DI container'a kaydeder.
    /// Dönen IHealthChecksBuilder'a her servis kendi DB/Redis check'lerini zincirleyebilir.
    /// Örn: builder.Services.AddQrPaymentHealthChecks().AddSqlServer(...).AddRedis(...);
    /// </summary>
    public static IHealthChecksBuilder AddQrPaymentHealthChecks(this IServiceCollection services)
        => services.AddHealthChecks();
}
