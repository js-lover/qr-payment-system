// QrPayment.Shared / Models / ApiResponse.cs
//
// Sistemdeki tüm API yanıtlarının ortak zarfı (envelope).
// Her servis endpoint'i bu yapıyı kullanır; böylece istemci
// tarafı tek bir deserialization mantığıyla tüm yanıtları işleyebilir.
//
// Başarılı yanıt  → { "success": true,  "data": { ... } }
// Başarısız yanıt → { "success": false, "error": { "code": "...", "message": "..." } }

namespace QrPayment.Shared.Models;

/// <summary>
/// Tüm API endpoint'lerinin döndürdüğü standart yanıt zarfı.
/// </summary>
public record ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }

    /// <summary>Distributed tracing için her yanıtta iletilen izleme kimliği.</summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>Başarılı yanıt oluşturur.</summary>
    public static ApiResponse<T> Ok(T data, string traceId = "") =>
        new() { Success = true, Data = data, TraceId = traceId };

    /// <summary>Hatalı yanıt oluşturur.</summary>
    public static ApiResponse<T> Fail(ApiError error, string traceId = "") =>
        new() { Success = false, Error = error, TraceId = traceId };
}

/// <summary>
/// Hata yanıtlarındaki hata detay bloğu.
/// ValidationErrors sadece HTTP 400 (doğrulama hatası) yanıtlarında dolar.
/// </summary>
public record ApiError
{
    /// <summary>Makine tarafından okunabilen hata kodu. Örn: "QR_TOKEN_EXPIRED"</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>İnsan tarafından okunabilen hata açıklaması.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Alan bazlı doğrulama hataları. Örn: { "amount": ["0'dan büyük olmalı"] }</summary>
    public Dictionary<string, string[]>? ValidationErrors { get; init; }
}

/// <summary>
/// Sayfalanmış liste yanıtları için sarmalayıcı.
/// Statement, transaction geçmişi gibi liste endpoint'lerinde kullanılır.
/// </summary>
public record PaginatedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }

    /// <summary>İstemcinin "daha fazla yükle" butonunu gösterip göstermeyeceğini belirtir.</summary>
    public bool HasNextPage => Page * PageSize < TotalCount;
}
