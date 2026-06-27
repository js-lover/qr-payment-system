// tools/seed/Program.cs
// Admin + test kullanıcılarını auth_db'ye seed eder.
// Kullanım: dotnet run --project tools/seed

using BCrypt.Net;
using Microsoft.Data.SqlClient;

const string ConnStr = "Server=localhost,1433;Database=auth_db;User Id=sa;Password=QrPay!Dev2026;TrustServerCertificate=True";

var users = new[]
{
    (Username: "admin",    Password: "Admin123!", Role: "ADMIN"),
    (Username: "merchant1",Password: "Merchant1!", Role: "MERCHANT"),
    (Username: "customer1",Password: "Customer1!", Role: "CUSTOMER"),
};

await using var conn = new SqlConnection(ConnStr);
await conn.OpenAsync();

foreach (var (username, password, role) in users)
{
    var check = new SqlCommand("SELECT COUNT(1) FROM users WHERE username = @u", conn);
    check.Parameters.AddWithValue("@u", username);
    var exists = (int)(await check.ExecuteScalarAsync())! > 0;

    if (exists)
    {
        Console.WriteLine($"[SKIP] {username} zaten var.");
        continue;
    }

    var hash = BCrypt.Net.BCrypt.HashPassword(password, 12);
    var id   = Guid.NewGuid();

    var cmd = new SqlCommand(@"
        INSERT INTO users (Id, Username, PasswordHash, Role, IsActive, CreatedAt)
        VALUES (@id, @u, @h, @r, 1, SYSDATETIMEOFFSET())", conn);
    cmd.Parameters.AddWithValue("@id", id);
    cmd.Parameters.AddWithValue("@u", username);
    cmd.Parameters.AddWithValue("@h", hash);
    cmd.Parameters.AddWithValue("@r", role);
    await cmd.ExecuteNonQueryAsync();

    Console.WriteLine($"[OK] {username} ({role}) oluşturuldu. Şifre: {password}");
}

Console.WriteLine("Seed tamamlandı.");
