// TransactionService.Api / Program.cs
//
// Transaction Service başlangıç noktası.
//
// Kayıtlar:
//   - Infrastructure (DB, BankSimulator, PaymentService, Kafka, HttpClients)
//   - SignalR hubs (/hubs/payment — POS terminali ödeme sonucu bekler)
//   - JWT Bearer doğrulama
//   - MSSQL health check
//   - Scalar OpenAPI UI
//
// Port: 5005 (Kong yapılandırmasındaki transaction servisi adresi)

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using QrPayment.Shared.Extensions;
using Scalar.AspNetCore;
using System.Security.Cryptography;
using TransactionService.Infrastructure.Extensions;
using TransactionService.Infrastructure.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ─── Bağımlılıklar ────────────────────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// ─── Health Check (MSSQL) ─────────────────────────────────────────────────
builder.Services
    .AddQrPaymentHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("TransactionDb")!);

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
            // SignalR WebSocket bağlantılarında token query string'den alınır
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    var accessToken = ctx.Request.Query["access_token"];
                    var path = ctx.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        ctx.Token = accessToken;
                    return Task.CompletedTask;
                }
            };
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

// SignalR hub endpoint — POS terminal WebSocket bağlantısı burada
app.MapHub<PaymentHub>("/hubs/payment");

if (app.Environment.IsDevelopment())
    await app.MigrateDatabaseAsync();

app.Run();
