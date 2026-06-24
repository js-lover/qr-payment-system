// QrPayment.Shared / Exceptions / BusinessRuleException.cs
//
// Sistemdeki tüm iş kuralı ihlallerinin base exception sınıfı.
// Her servis kendi domain exception'larını buradan türetir.
// ExceptionHandlerMiddleware bu exception'ı yakalar ve RFC 7807
// formatında HTTP yanıtına dönüştürür.
//
// Kullanım:
//   throw new BusinessRuleException("INSUFFICIENT_BALANCE", "Yetersiz Bakiye",
//       "Cüzdan bakiyesi bu işlem için yeterli değil.", 409);

namespace QrPayment.Shared.Exceptions;

/// <summary>
/// Tüm domain/iş kuralı exception'larının tabanı.
/// HTTP durum kodu ve hata kodu taşır; middleware bunu Problem Details'e çevirir.
/// </summary>
public class BusinessRuleException(string errorCode, string title, string message, int httpStatus = 409)
    : Exception(message)
{
    /// <summary>Makine tarafından okunabilen hata kodu. Örn: "INSUFFICIENT_BALANCE"</summary>
    public string ErrorCode { get; } = errorCode;

    /// <summary>İnsan tarafından okunabilen kısa başlık.</summary>
    public string Title { get; } = title;

    /// <summary>Problem Details URL'sindeki path segment. Örn: "insufficient-balance"</summary>
    public string ErrorType { get; } = errorCode.ToLowerInvariant().Replace('_', '-');

    /// <summary>Yanıt olarak dönecek HTTP durum kodu.</summary>
    public int HttpStatus { get; } = httpStatus;
}

/// <summary>Kayıt bulunamadığında fırlatılır. HTTP 404 döner.</summary>
public class NotFoundException(string resource, object id)
    : BusinessRuleException("NOT_FOUND", $"{resource} Not Found",
        $"{resource} with id '{id}' was not found.", 404);

/// <summary>Token geçersiz veya eksik olduğunda. HTTP 401 döner.</summary>
public class UnauthorizedException(string message = "Unauthorized.")
    : BusinessRuleException("UNAUTHORIZED", "Unauthorized", message, 401);

/// <summary>Yetki yetersizliğinde (örn. KYC onayı beklenirken). HTTP 403 döner.</summary>
public class ForbiddenException(string message = "Access denied.")
    : BusinessRuleException("FORBIDDEN", "Forbidden", message, 403);
