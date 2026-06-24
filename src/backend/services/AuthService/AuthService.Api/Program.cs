// AuthService.Api / Program.cs
//
// ASP.NET Core 10 bootstrap.
// Sıra önemli: önce DI kayıtları, sonra middleware pipeline.
//
// Port: 5001 (docker-compose ve Kong konfigürasyonuyla uyumlu)
// OpenAPI: Scalar UI → http://localhost:5001/scalar/v1

using AuthService.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using QrPayment.Shared.Extensions;
using Scalar.AspNetCore;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// ─── Servisler ────────────────────────────────────────────────────────────────

// Infrastructure katmanı (EF Core, JWT, BCrypt, TOTP, HMAC)
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Health check'ler (MSSQL bağlantısı "ready" tag'i ile)
builder.Services.AddQrPaymentHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("AuthDb")!,
        tags: ["ready"],
        name: "mssql");

// JWT doğrulama — public key ile RS256
// Dev ortamında public key dosyası yoksa servis yine de başlar (graceful)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var publicKeyPath = builder.Configuration["Jwt:PublicKeyPath"];
        if (!string.IsNullOrEmpty(publicKeyPath) && File.Exists(publicKeyPath))
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(publicKeyPath));

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "qrpay-auth",
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "qrpay-services",
                ValidateLifetime = true,
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        }
    });

builder.Services.AddAuthorization();

// ─── Build ────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Dev ortamında migration otomatik çalışır
if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync();
}

// ─── Middleware Pipeline ──────────────────────────────────────────────────────

// Correlation ID + Global exception handler (pipeline'ın en dışında)
app.UseQrPaymentMiddleware();

app.MapOpenApi();
app.MapScalarApiReference(); // http://localhost:5001/scalar/v1

app.UseQrPaymentHealthChecks();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
