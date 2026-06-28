# QR Payment System

Enterprise-grade QR ödeme sistemi. Mikroservis mimarisi, gerçek zamanlı ödeme akışı ve üç farklı istemci uygulaması.

---

## Mimari

```
┌───────────────────────────────────────────────────────────────┐
│                          İstemciler                           │
│  POS Terminal (Vite)  │  Mobil App (Expo)  │  Merchant Panel  │
│       :5173           │  iOS / Android     │  (Next.js) :3000 │
└──────────┬────────────────────┬─────────────────┬────────────┘
           │                    │                 │
           └────────────────────┼─────────────────┘
                                ▼
                    ┌───────────────────────┐
                    │   Kong API Gateway    │  :8000
                    │  Rate Limit · JWT     │  Admin: :8001
                    │  CORS · Routing       │
                    └──────────┬────────────┘
                               │
        ┌──────────────────────┼──────────────────────────┐
        ▼                      ▼               ▼           ▼
 ┌────────────┐  ┌──────────────────┐  ┌──────────┐  ┌──────────────┐
 │    Auth    │  │   Onboarding     │  │  Wallet  │  │   QR Code    │
 │  Service   │  │    Service       │  │  Service │  │   Service    │
 │   :5282    │  │     :5138        │  │   :5265  │  │    :5020     │
 └────────────┘  └──────────────────┘  └──────────┘  └──────────────┘
        ▼                                                     ▼
 ┌────────────────────────────────────────────────────────────────────┐
 │                 Transaction Service  :5133                         │
 │         Ödeme Orkestrasyon · ISO 8583 · SignalR Hub               │
 └────────────────────────┬───────────────────────────────────────────┘
                          │  Kafka Events
                          ▼
                ┌───────────────────────┐        ┌──────────────────┐
                │   Reporting Service   │        │  Bank Simulator  │
                │    (Elasticsearch)    │        │  ISO 8583 :9583  │
                │        :5170          │        │     (TCP)        │
                └───────────────────────┘        └──────────────────┘
```

---

## Ödeme Akışı

```
POS Terminal              Mobil Uygulama            Backend
     │                          │                      │
     ├── POST /qr/generate ─────────────────────────►  │  QR token üret (Redis, 90sn TTL)
     │◄── {token, qrContent} ──────────────────────── │
     │                          │                      │
     │  [QR ekranda göster]     │                      │
     │  [SignalR: qr:{token}]   │                      │
     │                          ├── GET /qr/{t}/validate►│
     │                          │◄── {tutar, işyeri} ─ │
     │                          │                      │
     │                          ├── POST /payments/confirm►│
     │                          │                      ├─ QR claim (Redis SETNX)
     │                          │                      ├─ Wallet provision
     │                          │                      ├─ ISO 8583 → Bank Simulator
     │                          │                      ├─ Wallet confirm / release
     │                          │                      ├─ Kafka: payment.completed
     │◄── SignalR: PaymentResult───────────────────── │  (POS gruba bildir)
     │                          │◄── HTTP {status} ─── │  (Mobil HTTP yanıttan okur)
     │  [ONAYLANDI / REDDEDİLDİ]│  [Başarılı / Hata]  │
```

---

## Servisler

| Servis | Port | Teknoloji | Açıklama |
|--------|------|-----------|----------|
| Auth Service | 5282 | .NET 10, SQL Server | JWT (RS256), BCrypt, TOTP, terminal HMAC auth |
| Onboarding Service | 5138 | .NET 10, SQL Server | KYC, OTP doğrulama, merchant/terminal kayıt |
| Wallet Service | 5265 | .NET 10, SQL Server | Çift girişli ledger, provision/confirm/release |
| QR Code Service | 5020 | .NET 10, Redis | Token üretim, TTL yönetimi, claim (SETNX) |
| Transaction Service | 5133 | .NET 10, SQL Server | Ödeme orkestrasyonu, ISO 8583, SignalR hub |
| Reporting Service | 5170 | .NET 10, Elasticsearch | Kafka consumer, full-text arama |
| Bank Simulator | 9583 | .NET 10, TCP | ISO 8583 auth simülatörü (belirleyici test kuralları) |

### Altyapı

| Servis | Port | Kullanım |
|--------|------|----------|
| Kong API Gateway | 8000 / 8001 | Routing, rate limiting, CORS, JWT doğrulama |
| SQL Server 2022 | 1433 | Auth, Onboarding, Wallet, Transaction veritabanları |
| Redis 7 | 6379 | QR token cache (90sn TTL) |
| Apache Kafka | 9092 | `payment.completed`, `payment.failed` event'leri |
| Kafka UI | 8080 | Topic ve consumer group yönetimi |
| Elasticsearch 8 | 9200 | İşlem indeksleme ve arama |

---

## İstemci Uygulamaları

### POS Terminal (`src/frontend/pos-terminal`)
- React + Vite + TypeScript
- Akış: Giriş → Tutar → QR Oluştur → Müşteri Tarasın → Sonuç
- SignalR ile gerçek zamanlı ödeme sonucu (grup: `qr:{token}`)
- Countdown timer + QR durum polling (2sn aralık)

### Mobil Uygulama (`src/frontend/mobile-app`)
- React Native + Expo SDK 54
- Akış: Giriş → QR Tara → Tutarı Onayla → Ödeme
- `expo-camera` ile QR tarama
- Ödeme sonucu HTTP yanıtından doğrudan okunur (SignalR race condition yok)

### Merchant Panel (`src/frontend/merchant-panel`)
- Next.js 15 + TypeScript + Tailwind CSS
- İşlem listesi, özet kartlar (ciro, başarılı/başarısız sayısı)
- Cookie tabanlı JWT oturum yönetimi

---

## Kurulum

### Gereksinimler

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- iOS için: Xcode + [Expo Go](https://apps.apple.com/app/expo-go/id982107779) (v54)

### 1. Altyapıyı Başlat

```bash
cd infra/docker
docker compose up -d
```

Tüm container'ların `healthy` durumuna gelmesini bekle (~30sn).

### 2. Backend Servislerini Başlat

Her servis için ayrı terminal:

```bash
cd src/backend/services/AuthService/AuthService.Api        && dotnet run &
cd src/backend/services/OnboardingService/OnboardingService.Api && dotnet run &
cd src/backend/services/WalletService/WalletService.Api    && dotnet run &
cd src/backend/services/QrCodeService/QrCodeService.Api    && dotnet run &
cd src/backend/services/BankSimulator                      && dotnet run &
cd src/backend/services/TransactionService/TransactionService.Api && dotnet run &
cd src/backend/services/ReportingService/ReportingService.Api    && dotnet run &
```

### 3. Kong API Gateway'i Yapılandır

İlk kurulumda bir kez çalıştır (ayarlar PostgreSQL'de kalıcıdır):

```bash
for svc_route in \
  "auth-service:5282:/auth" \
  "onboarding-service:5138:/onboarding" \
  "wallet-service:5265:/wallet" \
  "qr-service:5020:/qr" \
  "reporting-service:5170:/reports"; do
  svc="${svc_route%%:*}"; rest="${svc_route#*:}"; port="${rest%%:*}"; path="${rest#*:}"
  curl -s -X POST http://localhost:8001/services -d "name=$svc" -d "url=http://host.docker.internal:$port" > /dev/null
  curl -s -X POST "http://localhost:8001/services/$svc/routes" -d "paths[]=$path" -d strip_path=false > /dev/null
done

# Transaction Service (çoklu path: /payments ve /hubs)
curl -s -X POST http://localhost:8001/services -d name=transaction-service -d url=http://host.docker.internal:5133 > /dev/null
curl -s -X POST http://localhost:8001/services/transaction-service/routes \
  -d "paths[]=/payments" -d "paths[]=/hubs" -d strip_path=false > /dev/null

# CORS (SignalR için credentials + explicit origins gerekli)
curl -s -X POST http://localhost:8001/plugins \
  -d name=cors \
  -d "config.origins[]=http://localhost:5173" \
  -d "config.origins[]=http://localhost:3000" \
  -d "config.origins[]=http://localhost:8081" \
  -d "config.methods[]=GET" -d "config.methods[]=POST" \
  -d "config.methods[]=PUT" -d "config.methods[]=DELETE" \
  -d "config.methods[]=OPTIONS" -d "config.methods[]=PATCH" \
  -d "config.credentials=true" \
  -d "config.max_age=3600" > /dev/null

echo "Kong yapılandırması tamamlandı."
```

### 4. Frontend Uygulamalarını Başlat

**POS Terminal:**
```bash
cd src/frontend/pos-terminal
cp .env.local.example .env.local   # merchant bilgilerini düzenle
npm install && npm run dev
# http://localhost:5173
```

**Merchant Panel:**
```bash
cd src/frontend/merchant-panel
npm install && npm run dev
# http://localhost:3000
```

**Mobil Uygulama:**
```bash
cd src/frontend/mobile-app

# Fiziksel cihaz için Mac'in LAN IP'sini yaz
echo "EXPO_PUBLIC_API_BASE_URL=http://$(ipconfig getifaddr en0):8000" > .env.local

npm install
npx expo start --ios --clear
```

> Simulator için `.env.local` gerekli değil; `localhost:8000` doğrudan çalışır.

---

## Test Kullanıcıları

| Kullanıcı | Kullanıcı Adı | Şifre | Rol |
|-----------|--------------|-------|-----|
| Müşteri | `customer1` | `Customer123!` | CUSTOMER |
| İşyeri Yöneticisi | `merchant1` | `Merchant123!` | MERCHANT |
| Yönetici | `admin` | `Admin123!` | ADMIN |

**Test senaryosu:**
1. POS Terminal'e `merchant1 / Merchant123!` ile giriş yap
2. Tutar gir → QR oluştur
3. Mobil uygulamaya `customer1 / Customer123!` ile giriş yap → QR tara → Öde
4. Merchant Panel'de `merchant1` ile işlemleri izle

---

## API Referansı

Tüm istekler `http://localhost:8000` (Kong) üzerinden yapılır.

### Auth — `/auth`

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| POST | `/auth/token` | Giriş (JWT access + refresh token) |
| POST | `/auth/refresh` | Access token yenile |
| POST | `/auth/revoke` | Refresh token iptal et |
| POST | `/auth/totp/setup` | TOTP 2FA kurulumu |
| POST | `/auth/totp/verify` | TOTP kodu doğrula |
| POST | `/auth/terminal/challenge` | Terminal HMAC challenge |

### QR Code — `/qr`

| Method | Endpoint | Yetki | Açıklama |
|--------|----------|-------|----------|
| POST | `/qr/generate` | TERMINAL/MERCHANT/ADMIN | QR token üret (90sn TTL) |
| GET | `/qr/{token}/validate` | CUSTOMER | QR bilgilerini al |
| POST | `/qr/{token}/claim` | CUSTOMER | QR'ı atomik olarak claim et |
| GET | `/qr/{token}/status` | TERMINAL/MERCHANT/ADMIN | QR durumu (PENDING/CLAIMED/EXPIRED) |

### Wallet — `/wallet`

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | `/wallet/balance` | Bakiye sorgula |
| POST | `/wallet/topup` | Bakiye yükle (ADMIN) |
| POST | `/wallet/provision` | Tutarı bloke et |
| POST | `/wallet/confirm` | Blokajı onayla |
| POST | `/wallet/release` | Blokajı serbest bırak |

### Payments — `/payments`

| Method | Endpoint | Yetki | Açıklama |
|--------|----------|-------|----------|
| POST | `/payments/confirm` | CUSTOMER | Ödemeyi başlat — nihai sonucu HTTP yanıtında döner |
| GET | `/payments/{id}` | Bearer | İşlem durumu sorgula |

### Reports — `/reports`

| Method | Endpoint | Yetki | Açıklama |
|--------|----------|-------|----------|
| GET | `/reports/my-transactions` | CUSTOMER | Kişisel işlem geçmişi |
| GET | `/reports/transactions` | ADMIN/MERCHANT | Filtreli işlem listesi |

### SignalR Hub — `/hubs/payment`

POS terminal, QR oluşturulunca `qr:{token}` grubuna katılır. Ödeme tamamlandığında `PaymentResult` eventi yayınlanır:

```json
{
  "transactionId": "uuid",
  "status": "COMPLETED",
  "responseCode": "00"
}
```

---

## Bank Simulator (ISO 8583)

TCP bağlantısı, port **9583**. Belirleyici test kuralları:

| Koşul | Sonuç |
|-------|-------|
| Tutar ≤ 10 TL | Her zaman APPROVED (`00`) |
| 10 TL < Tutar < 1000 TL | %90 APPROVED / %10 DECLINED (`51`) |
| Tutar ≥ 1000 TL | %30 APPROVED / %70 DECLINED |
| MaskedPan `****0000` | Her zaman DECLINED (test kart) |
| MaskedPan `****9999` | Her zaman APPROVED (test kart) |

---

## Proje Yapısı

```
qr-payment-system/
├── src/
│   ├── backend/
│   │   ├── services/
│   │   │   ├── AuthService/
│   │   │   ├── OnboardingService/
│   │   │   ├── WalletService/
│   │   │   ├── QrCodeService/
│   │   │   ├── TransactionService/
│   │   │   ├── ReportingService/
│   │   │   └── BankSimulator/
│   │   └── shared/
│   │       ├── QrPayment.Shared/        # ApiResponse, exceptions, middleware
│   │       ├── QrPayment.Contracts/     # Servisler arası DTO'lar
│   │       └── QrPayment.Kafka/         # Producer, consumer, events, topics
│   └── frontend/
│       ├── pos-terminal/                # React + Vite
│       ├── mobile-app/                  # React Native + Expo SDK 54
│       └── merchant-panel/              # Next.js 15
├── infra/
│   ├── docker/
│   │   └── docker-compose.yml
│   └── kong/
│       └── kong.yml                     # Declarative config (referans)
├── database/
│   └── seeds/
│       └── dev_seed.sql
└── .github/
    └── workflows/
        └── backend-ci.yml               # Build + test pipeline
```

---

## CI/CD

GitHub Actions (`.github/workflows/backend-ci.yml`):
1. NuGet package cache
2. `dotnet restore`
3. `dotnet build --configuration Release`
4. `dotnet test` (xUnit)
5. Test sonuçları artifact olarak yüklenir

---

## Teknik Notlar

**Senkron ödeme akışı** — `POST /payments/confirm` tüm adımları (provision → bank auth → kafka → SignalR) senkron işler ve HTTP yanıtında nihai statüsü döndürür. Mobil uygulama SignalR beklemez; HTTP yanıtını okur. POS terminal SignalR ile bildirim alır çünkü QR ekranında pasif bekliyor.

**QR claim'de Redis SETNX** — Aynı QR'ın iki farklı müşteri tarafından eş zamanlı ödenmesini engellemek için atomik SETNX operasyonu kullanılır. Race condition garantisi verir.

**Çift girişli ledger** — Wallet Service'te her finansal hareket hem debit hem credit kaydı olarak tutulur. Provision/release döngüsü ile yarım kalan ödemelerde bakiye tutarsızlığı önlenir.

**Kong CORS + SignalR** — `Access-Control-Allow-Origin: *`, SignalR negotiate isteğinin `Authorization` header'ı ile uyumsuz. Explicit origin listesi ve `credentials: true` zorunlu. `config.headers` boş bırakıldığında Kong preflight'ta istenen tüm header'ları echo eder.
