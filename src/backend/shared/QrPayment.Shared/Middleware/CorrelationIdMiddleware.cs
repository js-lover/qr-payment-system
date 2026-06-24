// QrPayment.Shared / Middleware / CorrelationIdMiddleware.cs
//
// Dağıtık sistemlerde tek bir isteğin tüm servislerdeki log satırlarını
// birbirine bağlamak için kullanılan Correlation ID mekanizması.
//
// Davranış:
//   - İstek "X-Correlation-Id" başlığıyla geliyorsa aynı değeri kullanır
//     (Gateway veya başka servis tarafından eklenmiş olabilir).
//   - Başlık yoksa yeni bir UUID oluşturur.
//   - ID hem HttpContext.Items'a yazılır (diğer middleware/servisler okur)
//     hem de yanıt başlığına eklenir (istemci loglama için).

using Microsoft.AspNetCore.Http;

namespace QrPayment.Shared.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        // Gelen başlıktan al veya yeni oluştur
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        // Diğer servis katmanlarının erişebilmesi için context'e yaz
        context.Items["CorrelationId"] = correlationId;

        // İstemcinin kendi isteğini takip edebilmesi için yanıta ekle
        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }
}
