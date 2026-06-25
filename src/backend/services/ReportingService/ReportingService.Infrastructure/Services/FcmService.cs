// ReportingService.Infrastructure / Services / FcmService.cs
//
// Firebase Cloud Messaging (FCM) push notification servisi.
//
// Kullanım: Ödeme tamamlandığında müşteriye push bildirim gönderilir.
//
// İki implementasyon:
//   1. LogFcmService (Dev)  — bildirimi loglara yazar, FCM'e göndermez
//   2. FirebaseFcmService (Prod) — Firebase Admin SDK ile gerçek push gönderir
//
// Firebase Admin SDK (GoogleService-Account):
//   - service account JSON → appsettings'de FIREBASE_CREDENTIALS_PATH
//   - SDK: FirebaseAdmin paketi (firebase-admin NuGet)
//
// MVP'de HttpClient ile FCM v1 API doğrudan çağrılır (SDK yerine).
// Bu yaklaşım SDK bağımlılığını azaltır.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReportingService.Infrastructure.Services;

public interface IFcmService
{
    Task SendPaymentNotificationAsync(
        string fcmToken, string title, string body, CancellationToken ct = default);
}

/// <summary>Dev ortamı — gerçek push göndermez, loglara yazar.</summary>
public class LogFcmService(ILogger<LogFcmService> logger) : IFcmService
{
    public Task SendPaymentNotificationAsync(
        string fcmToken, string title, string body, CancellationToken ct = default)
    {
        logger.LogWarning("[DEV ONLY] FCM Push to {Token}: {Title} — {Body}", fcmToken, title, body);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Üretim ortamı — FCM v1 HTTP API üzerinden push bildirim gönderir.
/// Google Service Account token'ı her 1 saatte yenilenmeli (OAuth2).
/// MVP'de sabit token kullanılır; üretimde Google Auth Library tercih edilmeli.
/// </summary>
public class FirebaseFcmService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<FirebaseFcmService> logger) : IFcmService
{
    public async Task SendPaymentNotificationAsync(
        string fcmToken, string title, string body, CancellationToken ct = default)
    {
        var projectId = configuration["Firebase:ProjectId"]
            ?? throw new InvalidOperationException("Firebase:ProjectId not configured.");

        var accessToken = configuration["Firebase:AccessToken"]
            ?? throw new InvalidOperationException("Firebase:AccessToken not configured.");

        var payload = new
        {
            message = new
            {
                token = fcmToken,
                notification = new { title, body },
                android = new { priority = "high" },
                apns = new { headers = new Dictionary<string, string> { ["apns-priority"] = "10" } }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            logger.LogError("FCM send failed for token {Token}. StatusCode: {Status}",
                fcmToken, response.StatusCode);
    }
}
