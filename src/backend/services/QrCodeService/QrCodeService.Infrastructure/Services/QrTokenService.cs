// QrCodeService.Infrastructure / Services / QrTokenService.cs
//
// QR token üretimi ve Redis yönetimi.
//
// Tasarım kararları:
//   - Token UUID'dir (cryptographically random, öngörülemez)
//   - QR içeriği: "QRPAY:{uuid}" — mobil uygulama bu formatı parse eder
//   - Redis TTL: 90 saniye (QR kodunun ekranda bekleme süresi)
//   - Claim: SETNX (SET if Not Exists) ile atomik kilitleme
//     Aynı token iki kez claim edilemez — race condition koruması
//   - Token JSON olarak Redis'te saklanır (StackExchange.Redis)
//
// Redis anahtar şeması:
//   qr:{uuid}          → QrToken JSON (TTL: 90s) — token bilgisi
//   qr:{uuid}:claimed  → "1" (TTL: 120s) — claim kilidi

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using QrCodeService.Domain.Models;
using StackExchange.Redis;

namespace QrCodeService.Infrastructure.Services;

public interface IQrTokenService
{
    Task<QrToken> GenerateAsync(Guid terminalId, Guid merchantId, string merchantTitle, decimal amount, CancellationToken ct = default);
    Task<QrToken?> GetAsync(string token, CancellationToken ct = default);
    Task<bool> ClaimAsync(string token, CancellationToken ct = default);
    Task<QrToken?> ValidateAsync(string token, CancellationToken ct = default);
}

public class QrTokenService(IConnectionMultiplexer redis, IConfiguration configuration) : IQrTokenService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private int TtlSeconds => int.Parse(configuration["QrCode:TtlSeconds"] ?? "90");

    private static string TokenKey(string token) => $"qr:{token}";
    private static string ClaimKey(string token) => $"qr:{token}:claimed";

    /// <summary>
    /// Yeni QR token üretir ve Redis'e kaydeder.
    /// Token, POS terminali ekranında QR kod olarak gösterilir.
    /// Müşteri mobil uygulaması bu QR'ı tarar.
    /// </summary>
    public async Task<QrToken> GenerateAsync(
        Guid terminalId, Guid merchantId, string merchantTitle, decimal amount, CancellationToken ct = default)
    {
        var uuid = Guid.NewGuid().ToString("N"); // compact UUID, kısa QR kodu için
        var qrToken = new QrToken
        {
            Token = uuid,
            QrContent = $"QRPAY:{uuid}",
            TerminalId = terminalId,
            MerchantId = merchantId,
            MerchantTitle = merchantTitle,
            Amount = amount,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(TtlSeconds),
            RemainingSeconds = TtlSeconds
        };

        var db = redis.GetDatabase();
        var json = JsonSerializer.Serialize(qrToken, JsonOptions);

        // SET key value EX 90 — atomik, TTL ile birlikte
        await db.StringSetAsync(TokenKey(uuid), json, TimeSpan.FromSeconds(TtlSeconds));

        return qrToken;
    }

    /// <summary>Token'ı Redis'ten getirir. Süresi dolmuşsa null döner (Redis otomatik siler).</summary>
    public async Task<QrToken?> GetAsync(string token, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var json = await db.StringGetAsync(TokenKey(token));
        if (json.IsNullOrEmpty) return null;

        var qrToken = JsonSerializer.Deserialize<QrToken>((string)json!, JsonOptions);
        if (qrToken is null) return null;

        // Kalan TTL'yi Redis'ten sorgula
        var ttl = await db.KeyTimeToLiveAsync(TokenKey(token));
        qrToken.RemainingSeconds = (int)(ttl?.TotalSeconds ?? 0);

        return qrToken;
    }

    /// <summary>
    /// Token'ı atomik olarak claim eder (SETNX).
    /// Başarılıysa true, token zaten claim edilmişse false döner.
    /// Bu mekanizma aynı QR'ın iki kez ödenmesini önler.
    /// </summary>
    public async Task<bool> ClaimAsync(string token, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();

        // SETNX: Anahtar yoksa set et ve true döndür; varsa set etme ve false döndür
        // Claim kilidi token TTL'sinden biraz daha uzun tutulur (120s) — gecikmeli silme için
        var claimed = await db.StringSetAsync(
            ClaimKey(token), "1",
            TimeSpan.FromSeconds(TtlSeconds + 30),
            When.NotExists);

        if (claimed)
        {
            // Token durumunu CLAIMED olarak güncelle
            var qrToken = await GetAsync(token, ct);
            if (qrToken is not null)
            {
                qrToken.Status = "CLAIMED";
                var json = JsonSerializer.Serialize(qrToken, JsonOptions);
                var remaining = await db.KeyTimeToLiveAsync(TokenKey(token));
                if (remaining.HasValue)
                    await db.StringSetAsync(TokenKey(token), json, remaining.Value);
            }
        }

        return claimed;
    }

    /// <summary>
    /// Token'ın geçerliliğini doğrular.
    /// Durum PENDING veya SCANNING olmalı; CLAIMED veya süre dolmuşsa null döner.
    /// </summary>
    public async Task<QrToken?> ValidateAsync(string token, CancellationToken ct = default)
    {
        var qrToken = await GetAsync(token, ct);
        if (qrToken is null) return null;
        if (qrToken.Status == "CLAIMED") return null;
        return qrToken;
    }
}
