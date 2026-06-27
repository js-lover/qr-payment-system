// ReportingService.Api / Program.cs
//
// Reporting Service başlangıç noktası.
//
// Kayıtlar:
//   - Elasticsearch istemcisi (transaction araması)
//   - FCM servisi (push notification)
//   - Kafka consumers: PaymentCompletedConsumer, PaymentFailedConsumer
//   - JWT Bearer doğrulama
//   - Scalar OpenAPI UI
//
// Pipeline:
//   CorrelationId → ExceptionHandler → OpenAPI → Health → Auth → Controllers
//
// Port: 5006 (Kong yapılandırmasındaki reporting servisi adresi)

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using QrPayment.Shared.Extensions;
using ReportingService.Infrastructure.Extensions;
using Scalar.AspNetCore;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// ─── Bağımlılıklar ────────────────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ─── Health Check ──────────────────────────────────────────────────────────
builder.Services.AddQrPaymentHealthChecks();

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

app.Run();
