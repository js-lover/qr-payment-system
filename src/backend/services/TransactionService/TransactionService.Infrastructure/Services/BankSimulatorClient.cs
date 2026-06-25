// TransactionService.Infrastructure / Services / BankSimulatorClient.cs
//
// ISO 8583 Bank Simulator TCP istemcisi.
//
// ISO 8583 nedir:
//   Finansal işlem mesaj standardı. POS terminal → banka arası protokol.
//   Her mesaj bitmap'lerle işaretlenmiş "Data Element" (DE) alanlarından oluşur.
//   Bu MVP'de sadeleştirilmiş metin formatı kullanılır; gerçek bankacılık
//   için Jpos, Progate gibi kütüphaneler tercih edilir.
//
// MVP mesaj formatı (tab-ayrımlı metin, gerçek hex bitmap değil):
//   MTI|DE2|DE4|DE11|DE12|DE37|DE41|DE42
//   MTI  : 0200 (Authorization Request)
//   DE2  : masked PAN (müşteri IBAN'ından üretilmiş)
//   DE4  : tutar (kuruş, 12 haneli sol sıfır dolgu)
//   DE11 : STAN (6 haneli sıra numarası)
//   DE12 : yerel saat (HHmmss)
//   DE37 : RRN (12 haneli referans)
//   DE41 : Terminal ID
//   DE42 : Merchant ID
//
// Bank Simulator yanıtı:
//   APPROVED:00:{stan}:{rrn}
//   DECLINED:{code}:{stan}:{rrn}
//   (Kod: "51" yetersiz bakiye, "05" genel red, "14" geçersiz kart vb.)

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Text;

namespace TransactionService.Infrastructure.Services;

public interface IBankSimulatorClient
{
    Task<BankResponse> AuthorizeAsync(BankRequest request, CancellationToken ct = default);
}

public record BankRequest(
    long AmountKurus,
    string TerminalId,
    string MerchantId,
    string Stan,
    string Rrn,
    string MaskedPan);

public record BankResponse(
    bool IsApproved,
    string ResponseCode,
    string Stan,
    string Rrn);

public class BankSimulatorClient(IConfiguration configuration, ILogger<BankSimulatorClient> logger)
    : IBankSimulatorClient
{
    private string Host => configuration["BankSimulator:Host"] ?? "localhost";
    private int Port => int.Parse(configuration["BankSimulator:Port"] ?? "9583");
    private int TimeoutMs => int.Parse(configuration["BankSimulator:TimeoutMs"] ?? "5000");

    public async Task<BankResponse> AuthorizeAsync(BankRequest request, CancellationToken ct = default)
    {
        // MTI 0200 = Authorization Request
        var message = $"0200|{request.MaskedPan}|{request.AmountKurus:D12}|{request.Stan}|{DateTime.UtcNow:HHmmss}|{request.Rrn}|{request.TerminalId}|{request.MerchantId}";

        logger.LogInformation(
            "ISO8583 → Bank Simulator. STAN={Stan} Amount={Amount} Terminal={Terminal}",
            request.Stan, request.AmountKurus, request.TerminalId);

        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeoutMs);

            await client.ConnectAsync(Host, Port, cts.Token);

            await using var stream = client.GetStream();
            var messageBytes = Encoding.UTF8.GetBytes(message + "\n");
            await stream.WriteAsync(messageBytes, cts.Token);

            // Yanıt oku
            var buffer = new byte[256];
            var bytesRead = await stream.ReadAsync(buffer, cts.Token);
            var responseText = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

            return ParseResponse(responseText, request.Stan, request.Rrn);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Bank Simulator timeout. STAN={Stan}", request.Stan);
            // Timeout → işlemi başarısız say
            return new BankResponse(false, "96", request.Stan, request.Rrn);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bank Simulator connection error. STAN={Stan}", request.Stan);
            return new BankResponse(false, "91", request.Stan, request.Rrn);
        }
    }

    private static BankResponse ParseResponse(string responseText, string defaultStan, string defaultRrn)
    {
        // Format: APPROVED:00:{stan}:{rrn}  veya  DECLINED:{code}:{stan}:{rrn}
        var parts = responseText.Split(':');
        if (parts.Length < 2) return new BankResponse(false, "96", defaultStan, defaultRrn);

        var isApproved = parts[0] == "APPROVED";
        var code = parts.Length > 1 ? parts[1] : "96";
        var stan = parts.Length > 2 ? parts[2] : defaultStan;
        var rrn = parts.Length > 3 ? parts[3] : defaultRrn;

        return new BankResponse(isApproved, code, stan, rrn);
    }
}
