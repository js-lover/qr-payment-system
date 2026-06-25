// ReportingService.Domain / Models / TransactionDocument.cs
//
// Elasticsearch'te saklanan işlem belgesi.
//
// Her başarılı/başarısız ödeme işlemi bu yapıyla Elasticsearch'e yazılır.
// MSSQL'deki transactions tablosuyla eşleşir ama daha zengin analitik verisi taşır.
//
// Elasticsearch index: "transactions"
// ID: Kafka event'indeki TransactionId (UUID)
//
// Tasarım notu:
//   Raporlama servisi sadece okuma yapar (Elasticsearch) veya Kafka event'i dinler.
//   MSSQL'e doğrudan bağlanmaz — servisler arası bağımsızlık prensibi.

namespace ReportingService.Domain.Models;

public class TransactionDocument
{
    public string Id { get; set; } = string.Empty;

    public Guid TransactionId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid MerchantId { get; set; }
    public string MerchantTitle { get; set; } = string.Empty;
    public string TerminalId { get; set; } = string.Empty;

    /// <summary>İşlem tutarı (TL, decimal).</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "TRY";

    /// <summary>İşlem sonucu: COMPLETED | FAILED | REVERSED</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>ISO 8583 Field 39. "00" = başarılı.</summary>
    public string IsoResponseCode { get; set; } = string.Empty;

    public string? Stan { get; set; }
    public string? Rrn { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Elasticsearch full-text arama için işlem açıklaması.</summary>
    public string Description => $"{MerchantTitle} - {Amount:F2} {Currency} - {Status}";
}
