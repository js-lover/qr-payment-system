// AuthService.Infrastructure / Services / HmacService.cs
//
// POS terminal istek doğrulama servisi (HMAC-SHA256).
//
// Terminal her isteğe şu başlıkları ekler:
//   X-Terminal-Id: TID001
//   X-Timestamp:   2026-06-24T10:00:00Z   (UTC, ISO 8601)
//   X-Nonce:       rastgele-32-char-string
//   X-HMAC-Sig:    base64(HMAC-SHA256(terminalId + timestamp + nonce + body, secretKey))
//
// Sunucu şunları kontrol eder:
//   1. HMAC imzası doğru mu?
//   2. Timestamp 5 dakikadan eski mi? → replay attack önlemi
//   3. Nonce daha önce kullanılmış mı? → replay attack önlemi (Redis'te tutulur)
//
// Dev notları:
//   Nonce cache'i şimdilik in-memory; production'da Redis kullanılmalı.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace AuthService.Infrastructure.Services;

public interface IHmacService
{
    bool Verify(string terminalId, string secretKey, string timestamp, string nonce,
                string body, string providedSignature);
    string Compute(string terminalId, string secretKey, string timestamp, string nonce, string body);
}

public class HmacService(IMemoryCache cache) : IHmacService
{
    private static readonly TimeSpan ReplayWindow = TimeSpan.FromMinutes(5);

    public string Compute(string terminalId, string secretKey, string timestamp, string nonce, string body)
    {
        // İmzalanacak payload: tüm bileşenleri birleştir
        var payload = $"{terminalId}:{timestamp}:{nonce}:{body}";
        var keyBytes = Convert.FromBase64String(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToBase64String(hash);
    }

    public bool Verify(string terminalId, string secretKey, string timestamp, string nonce,
                       string body, string providedSignature)
    {
        // 1. Timestamp kontrolü — 5 dakikadan eski istekleri reddet
        if (!DateTimeOffset.TryParse(timestamp, out var requestTime)) return false;
        if (Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalMinutes) > 5) return false;

        // 2. Nonce replay kontrolü — aynı nonce'u bir kez kabul et
        var nonceKey = $"hmac:nonce:{nonce}";
        if (cache.TryGetValue(nonceKey, out _)) return false; // Nonce daha önce kullanılmış
        cache.Set(nonceKey, true, ReplayWindow);

        // 3. HMAC imzasını doğrula — constant-time karşılaştırma
        var expected = Compute(terminalId, secretKey, timestamp, nonce, body);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(providedSignature));
    }
}
