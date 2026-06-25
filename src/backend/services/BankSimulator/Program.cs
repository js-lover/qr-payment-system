// BankSimulator / Program.cs
//
// ISO 8583 Bank Simulator — TCP sunucu.
//
// Amaç:
//   Geliştirme ve test ortamında gerçek bir bankaya bağlanmadan
//   ISO 8583 authorization request/response akışını simüle eder.
//
// Mesaj formatı (TransactionService → BankSimulator):
//   "MTI|maskedPan|amount12digit|stan|hhmmss|rrn|terminalId|merchantId"
//   Örn: "0200|****AB12|000000001000|123456|143022|250624123456|TID00001|MERC0001"
//
// Yanıt formatı (BankSimulator → TransactionService):
//   "APPROVED:00:{stan}:{rrn}"     — başarılı
//   "DECLINED:{code}:{stan}:{rrn}" — başarısız
//
// Simülasyon kuralları (belirleyici, test edilebilir):
//   Tutar 10 TL ve altı    → Her zaman onaylar (00)
//   Tutar 10-999 TL arası  → %90 onay, %10 red (51 yetersiz bakiye)
//   Tutar 1000 TL ve üzeri → %30 onay, %70 red
//   MaskedPan "****0000"   → Her zaman reddeder (test kart)
//   MaskedPan "****9999"   → Her zaman onaylar (test kart)
//
// Çalıştırma:
//   dotnet run --project src/backend/services/BankSimulator
//   Veya: dotnet run -- --port 9583

using System.Net;
using System.Net.Sockets;
using System.Text;

var port = args.Length > 1 && args[0] == "--port"
    ? int.Parse(args[1])
    : int.Parse(Environment.GetEnvironmentVariable("BANK_SIM_PORT") ?? "9583");

var listener = new TcpListener(IPAddress.Any, port);
listener.Start();
Console.WriteLine($"[Bank Simulator] Listening on port {port}...");

while (true)
{
    var client = await listener.AcceptTcpClientAsync();
    _ = HandleClientAsync(client); // fire-and-forget, her bağlantı ayrı task
}

static async Task HandleClientAsync(TcpClient client)
{
    await using var stream = client.GetStream();
    var buffer = new byte[1024];

    try
    {
        var bytesRead = await stream.ReadAsync(buffer);
        var message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

        Console.WriteLine($"[Bank Simulator] ← {message}");

        var response = ProcessMessage(message);
        Console.WriteLine($"[Bank Simulator] → {response}");

        var responseBytes = Encoding.UTF8.GetBytes(response + "\n");
        await stream.WriteAsync(responseBytes);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Bank Simulator] Error: {ex.Message}");
    }
    finally
    {
        client.Close();
    }
}

static string ProcessMessage(string message)
{
    var parts = message.Split('|');
    if (parts.Length < 8) return "DECLINED:30:000000:000000000000"; // Format hatası

    var mti = parts[0];
    var maskedPan = parts[1];
    var amountStr = parts[2];
    var stan = parts[3];
    var rrn = parts[5];

    if (mti != "0200") return $"DECLINED:12:{stan}:{rrn}"; // Sadece authorization request

    // Simülasyon mantığı
    if (maskedPan.EndsWith("0000")) return $"DECLINED:14:{stan}:{rrn}"; // Test kart - her zaman red
    if (maskedPan.EndsWith("9999")) return $"APPROVED:00:{stan}:{rrn}"; // Test kart - her zaman onay

    if (!long.TryParse(amountStr, out var amountKurus))
        return $"DECLINED:30:{stan}:{rrn}";

    // Tutar bazlı onay oranı
    var approvalRate = amountKurus <= 1000L    ? 1.0   // ≤10 TL: her zaman
                     : amountKurus <= 99900L   ? 0.9   // 10-999 TL: %90
                                               : 0.3;  // ≥1000 TL: %30

    var approved = Random.Shared.NextDouble() < approvalRate;
    if (approved)
        return $"APPROVED:00:{stan}:{rrn}";

    // Başarısız işlemde gerçekçi yanıt kodları
    var declineCode = amountKurus > 99900L ? "51" : "05"; // 51=yetersiz bakiye, 05=genel red
    return $"DECLINED:{declineCode}:{stan}:{rrn}";
}
