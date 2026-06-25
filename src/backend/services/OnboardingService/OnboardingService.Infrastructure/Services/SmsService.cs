// OnboardingService.Infrastructure / Services / SmsService.cs
//
// SMS OTP gönderimi için soyutlama.
//
// İki implementasyon:
//   1. LogSmsService (Dev)     — OTP'yi konsola yazar, gerçek SMS göndermez
//   2. NetgsmSmsService (Prod) — Netgsm HTTP API ile gerçek SMS gönderir
//
// Hangi implementasyonun kullanılacağı appsettings.json'daki Sms:Provider
// değerine göre InfrastructureExtensions'ta seçilir.
//
// Netgsm API bilgisi: https://www.netgsm.com.tr/dokuman/

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OnboardingService.Infrastructure.Services;

public interface ISmsService
{
    Task SendOtpAsync(string gsm, string otpCode, CancellationToken ct = default);
}

/// <summary>
/// Geliştirme ortamı SMS servisi.
/// OTP'yi gerçek SMS göndermek yerine loglara yazar.
/// Dev ortamında Netgsm hesabı ve API key gerektirmez.
/// </summary>
public class LogSmsService(ILogger<LogSmsService> logger) : ISmsService
{
    public Task SendOtpAsync(string gsm, string otpCode, CancellationToken ct = default)
    {
        // Geliştirici konsola bakarak kodu görebilir
        logger.LogWarning("[DEV ONLY] OTP for {Gsm}: {OtpCode}", gsm, otpCode);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Üretim ortamı SMS servisi.
/// Netgsm HTTP API kullanarak gerçek SMS gönderir.
/// appsettings.json'da Sms:Netgsm:UserCode ve Password dolu olmalı.
/// </summary>
public class NetgsmSmsService(IConfiguration configuration, ILogger<NetgsmSmsService> logger,
    HttpClient httpClient) : ISmsService
{
    public async Task SendOtpAsync(string gsm, string otpCode, CancellationToken ct = default)
    {
        var userCode = configuration["Sms:Netgsm:UserCode"]
                       ?? throw new InvalidOperationException("Netgsm UserCode is not configured.");
        var password = configuration["Sms:Netgsm:Password"]
                       ?? throw new InvalidOperationException("Netgsm Password is not configured.");
        var header = configuration["Sms:Netgsm:MsgHeader"] ?? "QRPAY";

        var message = $"QR Odeme OTP: {otpCode}. Gecerlilik: 3 dakika.";

        // Netgsm'in GET tabanlı API'si (döküman: https://www.netgsm.com.tr/dokuman/)
        var url = $"https://api.netgsm.com.tr/sms/send/get" +
                  $"?usercode={userCode}&password={password}" +
                  $"&gsmno={gsm.Replace("+", "")}&message={Uri.EscapeDataString(message)}" +
                  $"&msgheader={header}";

        try
        {
            var response = await httpClient.GetStringAsync(url, ct);

            // Netgsm başarılı yanıtta "00 ..." ile başlayan kod döner
            if (!response.StartsWith("00"))
                logger.LogError("Netgsm SMS failed for {Gsm}. Response: {Response}", gsm, response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Netgsm SMS exception for {Gsm}", gsm);
            throw;
        }
    }
}
