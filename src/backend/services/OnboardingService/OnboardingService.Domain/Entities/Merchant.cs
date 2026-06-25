// OnboardingService.Domain / Entities / Merchant.cs
//
// İşyeri entity'si. Ödeme sistemine bağlanan fiziksel veya dijital işyerlerini temsil eder.
//
// İşyeri onaylandığında Kafka'ya merchant.approved eventi yayınlanır;
// AuthService bu event'i dinleyerek merchant kullanıcı hesabı açar.
//
// MCC (Merchant Category Code): ISO 18245 standardı — örn. 5411 = Bakkal

namespace OnboardingService.Domain.Entities;

public class Merchant
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>İşyeri ticari unvanı.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Vergi numarası. Benzersiz olmalı; duplikasyon kontrolü için.</summary>
    public string TaxNumber { get; private set; } = string.Empty;

    /// <summary>Ödeme transferlerinin yapılacağı IBAN. Format: TR ile başlayan 26 karakter.</summary>
    public string Iban { get; private set; } = string.Empty;

    /// <summary>ISO 18245 merchant category code. Örn: "5411" (bakkal), "5812" (restoran).</summary>
    public string Mcc { get; private set; } = string.Empty;

    /// <summary>Başvuru durumu: PENDING | APPROVED | REJECTED</summary>
    public string Status { get; private set; } = "PENDING";

    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public ICollection<Branch> Branches { get; private set; } = [];

    private Merchant() { }

    public static Merchant Create(string title, string taxNumber, string iban, string mcc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(taxNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(iban);
        ArgumentException.ThrowIfNullOrWhiteSpace(mcc);

        return new Merchant
        {
            Title = title,
            TaxNumber = taxNumber.Trim(),
            Iban = iban.Replace(" ", "").ToUpperInvariant(),
            Mcc = mcc
        };
    }

    /// <summary>Admin işyerini onayladığında çağrılır.</summary>
    public void Approve() => Status = "APPROVED";

    /// <summary>Admin işyerini reddettiğinde çağrılır.</summary>
    public void Reject() => Status = "REJECTED";

    public void Deactivate() => IsActive = false;
}

public class Branch
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid MerchantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Navigation
    public ICollection<Terminal> Terminals { get; private set; } = [];

    private Branch() { }

    public static Branch Create(Guid merchantId, string name, string? address = null)
        => new() { MerchantId = merchantId, Name = name, Address = address };
}

public class Terminal
{
    /// <summary>Terminal kimlik numarası. Örn: "TID001". Primary key.</summary>
    public string Id { get; private set; } = string.Empty;

    public Guid MerchantId { get; private set; }
    public Guid? BranchId { get; private set; }

    /// <summary>HMAC-SHA256 imzalama için paylaşılan gizli anahtar (Base64).</summary>
    public string SecretKey { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private Terminal() { }

    public static Terminal Create(string terminalId, Guid merchantId, string secretKey, Guid? branchId = null)
        => new() { Id = terminalId, MerchantId = merchantId, SecretKey = secretKey, BranchId = branchId };
}
