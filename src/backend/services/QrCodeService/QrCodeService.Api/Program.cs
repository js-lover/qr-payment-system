// QrCodeService.Api / Program.cs
//
// QR Code Service başlangıç noktası.
//
// Kayıtlar:
//   - Redis (IConnectionMultiplexer)
//   - IQrTokenService → QrTokenService
//   - JWT Bearer doğrulama (AuthService public key)
//   - Redis health check
//   - Scalar OpenAPI UI
//
// Pipeline:
//   CorrelationId → ExceptionHandler → OpenAPI → Health → Auth → Controllers
//
// Port: 5004 (Kong yapılandırmasındaki qr servisi adresi)

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using QrCodeService.Infrastructure.Extensions;
using QrPayment.Shared.Extensions;
using Scalar.AspNetCore;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// ─── Bağımlılıklar ────────────────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ─── Health Check (Redis) ─────────────────────────────────────────────────
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services
    .AddQrPaymentHealthChecks()
    .AddRedis(redisConnectionString);

// ─── JWT Bearer ───────────────────────────────────────────────────────────
var publicKeyPath = builder.Configuration["Jwt:PublicKeyPath"];
if (!string.IsNullOrEmpty(publicKeyPath) && File.Exists(publicKeyPath))
{
    var rsa = RSA.Create();
    rsa.ImportFromPem(File.ReadAllText(publicKeyPath));

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });

    builder.Services.AddAuthorization();
}

// ─── Uygulama ─────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseQrPaymentMiddleware();
app.MapOpenApi();
app.MapScalarApiReference();
app.UseQrPaymentHealthChecks();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
