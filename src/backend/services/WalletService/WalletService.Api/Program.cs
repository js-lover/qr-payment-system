// WalletService.Api / Program.cs
//
// Wallet Service başlangıç noktası.
//
// Kayıtlar:
//   - Infrastructure (WalletDbContext, Repositories, WalletService, Kafka consumer)
//   - JWT Bearer doğrulama (AuthService public key)
//   - Scalar OpenAPI UI (/scalar/v1)
//   - MSSQL health check
//
// Pipeline:
//   CorrelationId → ExceptionHandler → OpenAPI → Health → Auth → Controllers
//
// Port: 5003 (Kong yapılandırmasındaki wallet servisi adresi)

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using QrPayment.Shared.Extensions;
using Scalar.AspNetCore;
using System.Security.Cryptography;
using WalletService.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ─── Bağımlılıklar ────────────────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ─── Health Check (MSSQL) ─────────────────────────────────────────────────
builder.Services
    .AddQrPaymentHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("WalletDb")!);

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
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "sub",
                RoleClaimType = "role"
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

if (app.Environment.IsDevelopment())
    await app.MigrateDatabaseAsync();

app.Run();
