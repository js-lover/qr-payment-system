// AuthService.Domain / Entities / TerminalCredential.cs
//
// POS terminal kimlik bilgisi. Her terminal bir HMAC-SHA256 gizli anahtarına sahip.
// Terminal her API isteğini bu anahtarla imzalar; AuthService imzayı doğrular.
//
// mTLS: Üretim ortamında terminal sertifikası da kontrol edilir.
// Dev ortamında HMAC imzası yeterli kabul edilir.

namespace AuthService.Domain.Entities;

public class TerminalCredential
{
    /// <summary>Terminal kimlik numarası. Örn: "TID001". Primary key.</summary>
    public string TerminalId { get; private set; } = string.Empty;

    /// <summary>HMAC-SHA256 imzalama için paylaşılan gizli anahtar (Base64 encoded).</summary>
    public string SecretKey { get; private set; } = string.Empty;

    public Guid MerchantId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private TerminalCredential() { }

    public static TerminalCredential Create(string terminalId, string secretKey, Guid merchantId)
    {
        return new TerminalCredential
        {
            TerminalId = terminalId,
            SecretKey = secretKey,
            MerchantId = merchantId
        };
    }

    public void Deactivate() => IsActive = false;
}
