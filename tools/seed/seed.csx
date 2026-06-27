// tools/seed/seed.csx
// Admin kullanıcısını auth_db'ye ekler.
// Kullanım: dotnet script seed.csx
// Bağlantı: localhost:1433, sa / QrPay!Dev2026

#r "nuget: BCrypt.Net-Next, 4.0.3"
#r "nuget: Microsoft.Data.SqlClient, 5.2.2"

using BCrypt.Net;
using Microsoft.Data.SqlClient;

var connStr = "Server=localhost,1433;Database=auth_db;User Id=sa;Password=QrPay!Dev2026;TrustServerCertificate=True";
var username = "admin";
var password = "Admin123!";
var role = "ADMIN";
var hash = BCrypt.Net.BCrypt.HashPassword(password, 12);
var id = Guid.NewGuid();

using var conn = new SqlConnection(connStr);
await conn.OpenAsync();

// Zaten varsa ekleme
var check = new SqlCommand("SELECT COUNT(1) FROM users WHERE username = @u", conn);
check.Parameters.AddWithValue("@u", username);
var count = (int)await check.ExecuteScalarAsync();

if (count > 0)
{
    Console.WriteLine($"Admin kullanıcısı zaten mevcut.");
}
else
{
    var cmd = new SqlCommand(@"
        INSERT INTO users (id, username, password_hash, role, is_active, created_at)
        VALUES (@id, @u, @h, @r, 1, SYSDATETIMEOFFSET())", conn);
    cmd.Parameters.AddWithValue("@id", id);
    cmd.Parameters.AddWithValue("@u", username);
    cmd.Parameters.AddWithValue("@h", hash);
    cmd.Parameters.AddWithValue("@r", role);
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine($"Admin kullanıcısı oluşturuldu. ID: {id}");
}
