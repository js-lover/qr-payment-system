# QR Payment System — Kodlama İmplementasyon Planı

> **Durum:** Onaylandı  
> **Hazırlayan:** Claude Code  
> **Tarih:** 2026-06-16  
> **Dayandığı Kaynak:** NotebookLM — "QR Payment System — Full Documentation"

---

## 1. Teknoloji Stack'i (Kesinleşmiş Kararlar)

### Backend
| Katman | Teknoloji | Karar Nedeni |
|--------|-----------|--------------|
| Dil & Framework | .NET 10 (C#) + ASP.NET Core | ADR-008: Native AOT, yüksek performans |
| Finansal DB | MSSQL Server 2022 | ACID garantisi, DECIMAL(18,4) desteği |
| Cache / TTL | Redis 7 | QR token TTL yönetimi (90sn), In-Memory hız |
| Message Broker | Apache Kafka | Async event-driven, replay özelliği |
| Search & Analytics | Elasticsearch 8 | Raporlama ve log indeksleme |
| Real-Time | ASP.NET SignalR (WebSocket) | Kasaya anlık ödeme bildirimi |
| API Gateway | Kong | RBAC, rate limiting, routing |
| Container | Docker + Kubernetes (K8s) | HPA, servis izolasyonu |
| ISO 8583 | TCP/TLS Socket (Custom Simulator) | Gerçek banka bağlantısı yok, simülatör yazılacak |

### Frontend
| Platform | Teknoloji | Karar Nedeni |
|----------|-----------|--------------|
| Müşteri Mobil App | **React Native** (TypeScript) | iOS+Android tek codebase, JS ekosistemi, QR kamera desteği |
| İşyeri Web Paneli | **Next.js 14** (React + TypeScript) | SSR/SSG, admin dashboard için ideal, Tailwind CSS |
| POS Terminal UI | **React** (TypeScript + Vite) | Hafif, web tabanlı, kasaya embed edilebilir |
| UI Kütüphanesi | **shadcn/ui** + Tailwind CSS | Web panelleri için |
| State Management | **Zustand** | Hem web hem React Native, basit ve test edilebilir |
| HTTP Client | **Axios** | İnterceptor desteği, token yönetimi (tüm platformlar) |
| Real-Time | **@microsoft/signalr** | POS, mobil ve web için SignalR WebSocket client |

### Üçüncü Parti Servisler
| Servis | Karar | Notlar |
|--------|-------|--------|
| SMS OTP | **Netgsm** | Türkiye'deki en düşük maliyetli SMS gateway (~0.01-0.02 TL/SMS). Dev ortamında OTP loglara basılır, production'da Netgsm aktif edilir. |
| Push Notification | **Firebase FCM** | Google'ın ücretsiz push notification altyapısı. iOS için APNs'i FCM üzerinden yönetir, ayrı entegrasyon gerekmez. |
| KYC | **Mock / Manuel Onay** | Türkiye'de ücretsiz/erişilebilir bir NVI/KPS API'si bulunamadı. Müşteri kimlik belgesi + selfie yükler, Admin panelinden manuel onaylanır. Gerçek KYC entegrasyonu ilerleyen aşamada eklenebilir. |
| Banka Bağlantısı | **ISO 8583 Simülatör** | Gerçek banka bağlantısı şimdilik yok. `00`, `51`, `91` ve timeout senaryolarını simüle eden custom TCP server yazılacak. |

---

## 2. Monorepo Klasör Yapısı

```
qr-payment-system/
├── .github/
│   └── workflows/                    # CI/CD pipeline'ları
│       ├── backend.yml
│       ├── mobile.yml
│       └── web.yml
│
├── src/
│   ├── backend/
│   │   ├── QrPayment.sln             # .NET Solution dosyası
│   │   ├── services/
│   │   │   ├── AuthService/          # JWT, OAuth2, mTLS, HMAC
│   │   │   ├── OnboardingService/    # KYC, Müşteri & İşyeri kayıt
│   │   │   ├── WalletService/        # Cüzdan, Bakiye, Ledger
│   │   │   ├── QrCodeService/        # QR üretim, Redis TTL
│   │   │   ├── TransactionService/   # ISO 8583, Provizyon, Reversal
│   │   │   ├── ReportingService/     # Elasticsearch, Mutabakat, PDF
│   │   │   └── BankSimulator/        # ISO 8583 TCP mock server
│   │   └── shared/
│   │       ├── QrPayment.Shared/     # Ortak modeller, exceptions
│   │       ├── QrPayment.Kafka/      # Kafka event tanımları
│   │       └── QrPayment.Contracts/  # Servisler arası DTO'lar
│   │
│   ├── mobile/
│   │   └── QrPaymentApp/             # React Native müşteri uygulaması
│   │       ├── src/
│   │       │   ├── core/             # DI, routing, theme, constants
│   │       │   ├── features/
│   │       │   │   ├── auth/         # Login, OTP, Biyometrik
│   │       │   │   ├── onboarding/   # KYC, belge yükleme
│   │       │   │   ├── wallet/       # Bakiye, Top-up
│   │       │   │   ├── qr/           # QR tarama, onay ekranı
│   │       │   │   └── history/      # Ekstre, makbuz
│   │       │   ├── shared/           # Ortak bileşenler, hooks, utils
│   │       │   └── store/            # Zustand store tanımları
│   │       ├── android/
│   │       ├── ios/
│   │       └── __tests__/
│   │
│   └── web/
│       ├── merchant-panel/           # Next.js İşyeri Paneli
│       │   ├── app/
│       │   │   ├── (auth)/           # Login sayfası
│       │   │   ├── dashboard/        # Ana panel
│       │   │   ├── reports/          # Satış raporları
│       │   │   ├── reconciliation/   # Mutabakat
│       │   │   └── onboarding/       # İşyeri başvurusu
│       │   └── components/
│       └── pos-terminal/             # React POS Kasası UI
│           ├── src/
│           │   ├── screens/
│           │   │   ├── AmountEntry/  # Tutar giriş ekranı
│           │   │   ├── QrDisplay/    # QR kodu göster
│           │   │   └── PaymentResult/# Başarı/Hata ekranı
│           │   └── hooks/            # SignalR WebSocket hook'u
│           └── public/
│
├── infra/
│   ├── docker/
│   │   └── docker-compose.yml        # Lokal dev ortamı
│   ├── k8s/
│   │   ├── base/                     # Deployment, Service, ConfigMap
│   │   └── overlays/
│   │       ├── staging/
│   │       └── production/
│   └── kong/
│       └── kong.yml                  # API Gateway route tanımları
│
├── database/
│   ├── migrations/                   # EF Core migrations
│   └── seeds/                        # Test verisi
│
└── docs/                             # Mevcut dokümantasyon
```

---

## 3. Faz Bazlı İmplementasyon Planı

### Faz 0 — Altyapı & Geliştirme Ortamı

**Amaç:** Tüm servisler için ortak altyapıyı kurmak, "her şeyin çalıştığını görmek."

#### Görevler:
- [ ] Git repo yapısı + `.gitignore` + `README.md` oluşturma
- [ ] `docker-compose.yml` hazırlama:
  - MSSQL Server 2022 (port 1433)
  - Redis 7 (port 6379)
  - Kafka + Zookeeper (port 9092)
  - Elasticsearch 8 (port 9200)
  - Kong API Gateway (port 8000/8001)
- [ ] `.NET 10 Solution` oluşturma (`QrPayment.sln`)
- [ ] `QrPayment.Shared` projesi: Ortak exception'lar, base response modeli, constants
- [ ] `QrPayment.Kafka` projesi: Event topic tanımları, producer/consumer base class
- [ ] `QrPayment.Contracts` projesi: Servisler arası DTO'lar
- [ ] Tüm servislerde ortak middleware: Global exception handler, request logging, correlation ID
- [ ] GitHub Actions CI pipeline (build + test)

**Çıktı:** `docker-compose up` ile tüm altyapı ayağa kalkar.

---

### Faz 1 — Auth Service

**Amaç:** Sistemin güvenlik kapısını açmak. Diğer tüm servisler buna bağımlı.

#### Veritabanı Şeması:
```sql
-- auth_db
CREATE TABLE users (
    id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    username    NVARCHAR(100) NOT NULL UNIQUE,
    password_hash NVARCHAR(256) NOT NULL,    -- BCrypt.Net-Next
    role        NVARCHAR(50) NOT NULL,        -- CUSTOMER | MERCHANT | TERMINAL | ADMIN
    totp_secret NVARCHAR(100),               -- Otp.NET (Google Authenticator)
    is_active   BIT DEFAULT 1,
    created_at  DATETIME2 DEFAULT GETUTCDATE()
);

CREATE TABLE refresh_tokens (
    id          BIGINT IDENTITY PRIMARY KEY,
    user_id     UNIQUEIDENTIFIER NOT NULL,
    token       NVARCHAR(512) NOT NULL,
    expires_at  DATETIME2 NOT NULL,
    is_revoked  BIT DEFAULT 0
);

CREATE TABLE terminal_credentials (
    terminal_id NVARCHAR(50) PRIMARY KEY,
    secret_key  NVARCHAR(256) NOT NULL,      -- HMAC-SHA256 imzalama anahtarı
    merchant_id UNIQUEIDENTIFIER NOT NULL,
    is_active   BIT DEFAULT 1
);
```

#### API Endpoint'leri:
| Method | Endpoint | Açıklama | Role |
|--------|----------|----------|------|
| POST | `/auth/token` | JWT access + refresh token üret | Public |
| POST | `/auth/refresh` | Refresh token ile yeni access token | Authenticated |
| POST | `/auth/revoke` | Token iptal | Authenticated |
| POST | `/auth/totp/setup` | Google Authenticator QR kodu | Customer |
| POST | `/auth/totp/verify` | TOTP kodu doğrula | Customer |
| POST | `/auth/terminal/challenge` | Terminal mTLS + HMAC doğrulama | Terminal |

#### Servis Özellikleri:
- JWT: RS256 (asimetrik anahtar), 15dk access + 7 gün refresh
- BCrypt.Net-Next: Work factor 12
- Otp.NET: TOTP 30sn window
- mTLS: Terminal sertifika doğrulaması
- HMAC-SHA256: Her terminal isteğinde imza kontrolü
- Kong'a JWT plugin entegrasyonu

---

### Faz 2 — Onboarding Service

**Amaç:** Müşteri ve işyeri kayıt süreçlerini hayata geçirmek.

#### Veritabanı Şeması:
```sql
-- onboarding_db
CREATE TABLE customers (
    id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    identity_hash    NVARCHAR(256) NOT NULL UNIQUE,  -- TCKN hash'i, ham TCKN saklanmaz
    first_name       NVARCHAR(100) NOT NULL,
    last_name        NVARCHAR(100) NOT NULL,
    gsm              NVARCHAR(20) NOT NULL UNIQUE,
    kyc_status       NVARCHAR(50) DEFAULT 'PENDING',  -- PENDING|APPROVED|REJECTED
    kyc_verified_at  DATETIME2,
    created_at       DATETIME2 DEFAULT GETUTCDATE(),
    is_active        BIT DEFAULT 1
);

CREATE TABLE merchants (
    id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    title        NVARCHAR(200) NOT NULL,
    tax_number   NVARCHAR(20) NOT NULL UNIQUE,
    iban         NVARCHAR(50) NOT NULL,
    mcc          NVARCHAR(10) NOT NULL,
    status       NVARCHAR(50) DEFAULT 'PENDING',
    created_at   DATETIME2 DEFAULT GETUTCDATE(),
    is_active    BIT DEFAULT 1
);

CREATE TABLE branches (
    id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    merchant_id UNIQUEIDENTIFIER NOT NULL,
    name        NVARCHAR(200) NOT NULL,
    address     NVARCHAR(500),
    is_active   BIT DEFAULT 1
);

CREATE TABLE terminals (
    id          NVARCHAR(50) PRIMARY KEY,        -- TID
    merchant_id UNIQUEIDENTIFIER NOT NULL,
    branch_id   UNIQUEIDENTIFIER,
    secret_key  NVARCHAR(256) NOT NULL,          -- HMAC anahtarı
    is_active   BIT DEFAULT 1,
    created_at  DATETIME2 DEFAULT GETUTCDATE()
);

CREATE TABLE otp_verifications (
    id          BIGINT IDENTITY PRIMARY KEY,
    gsm         NVARCHAR(20) NOT NULL,
    otp_code    NVARCHAR(10) NOT NULL,
    expires_at  DATETIME2 NOT NULL,
    is_used     BIT DEFAULT 0
);
```

#### API Endpoint'leri:
| Method | Endpoint | Açıklama | Role |
|--------|----------|----------|------|
| POST | `/onboarding/customer/register` | Müşteri kayıt başlat | Public |
| POST | `/onboarding/customer/verify-otp` | SMS OTP doğrula | Public |
| POST | `/onboarding/customer/kyc` | KYC belge yükleme | Customer |
| GET | `/onboarding/customer/:id/status` | KYC durumu sorgula | Customer |
| POST | `/onboarding/merchant/apply` | İşyeri başvurusu | Public |
| POST | `/onboarding/merchant/:id/approve` | İşyeri onaylama | Admin |
| POST | `/onboarding/merchant/:id/terminal` | Terminal oluştur | Admin |

#### SMS OTP Entegrasyonu:
- **Dev ortamı:** OTP kodu loglara (`Console.WriteLine`) yazılır, gerçek SMS gönderilmez
- **Production:** Netgsm HTTP API (`https://api.netgsm.com.tr/sms/send/get`) entegrasyonu
- OTP: 6 haneli, 3 dakika geçerlilik, maksimum 3 deneme hakkı

#### KYC Akışı:
- Müşteri TCKN hash'i + kimlik fotoğrafı + selfie yükler (S3/Azure Blob veya lokal storage)
- `kyc_status = PENDING` olarak kaydedilir
- Admin, Merchant Panel'deki KYC onay ekranından belgeleri inceleyip `APPROVED` veya `REJECTED` yapar
- Onay sonrası `customer.kyc_approved` Kafka eventi yayınlanır → WalletService cüzdanı aktif eder

#### Kafka Events (Publish):
- `customer.registered` → WalletService dinler, cüzdan açar
- `customer.kyc_approved` → WalletService cüzdanı aktif eder
- `merchant.approved` → AuthService dinler, merchant user oluşturur
- `terminal.created` → AuthService dinler, terminal credential kaydeder

---

### Faz 3 — Wallet & Account Service

**Amaç:** Finansal çekirdeği inşa etmek. En kritik servis.

#### Veritabanı Şeması:
```sql
-- wallet_db
CREATE TABLE wallets (
    id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    customer_id     UNIQUEIDENTIFIER NOT NULL UNIQUE,
    balance         DECIMAL(18,4) NOT NULL DEFAULT 0,
    blocked_balance DECIMAL(18,4) NOT NULL DEFAULT 0,
    currency        NVARCHAR(3) DEFAULT 'TRY',
    created_at      DATETIME2 DEFAULT GETUTCDATE(),
    is_active       BIT DEFAULT 1
);

-- Double-Entry Bookkeeping — IMMUTABLE, hiçbir zaman UPDATE/DELETE yok
CREATE TABLE ledger_entries (
    id              BIGINT IDENTITY PRIMARY KEY,  -- Sıralı, değiştirilemez
    transaction_ref NVARCHAR(100) NOT NULL,        -- İşlem referansı
    wallet_id       UNIQUEIDENTIFIER NOT NULL,
    entry_type      NVARCHAR(10) NOT NULL,         -- DEBIT | CREDIT
    amount          DECIMAL(18,4) NOT NULL,
    balance_after   DECIMAL(18,4) NOT NULL,
    description     NVARCHAR(500),
    created_at      DATETIME2 DEFAULT GETUTCDATE()
);

CREATE TABLE topup_requests (
    id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    wallet_id   UNIQUEIDENTIFIER NOT NULL,
    amount      DECIMAL(18,4) NOT NULL,
    method      NVARCHAR(50) NOT NULL,  -- CREDIT_CARD | EFT | HAVALE
    status      NVARCHAR(50) DEFAULT 'PENDING',
    created_at  DATETIME2 DEFAULT GETUTCDATE()
);

CREATE TABLE provisions (
    id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    wallet_id       UNIQUEIDENTIFIER NOT NULL,
    qr_token        UNIQUEIDENTIFIER NOT NULL UNIQUE,
    amount          DECIMAL(18,4) NOT NULL,
    status          NVARCHAR(50) DEFAULT 'BLOCKED',  -- BLOCKED|CONFIRMED|RELEASED
    expires_at      DATETIME2 NOT NULL,
    created_at      DATETIME2 DEFAULT GETUTCDATE()
);
```

#### API Endpoint'leri:
| Method | Endpoint | Açıklama | Role |
|--------|----------|----------|------|
| GET | `/wallet/balance` | JWT'deki wallet_id ile bakiye sorgula | Customer |
| POST | `/wallet/topup` | Cüzdana para yükleme | Customer |
| POST | `/wallet/provision` | QR için bloke koy | System (QR Service) |
| POST | `/wallet/provision/:id/confirm` | Blokeyi kesinleştir | System (Tx Service) |
| POST | `/wallet/provision/:id/release` | Blokeyi geri al (Reversal) | System (Tx Service) |
| GET | `/wallet/ledger` | Muhasebe kayıtları (Admin) | Admin |

#### Kritik SQL Güvenlik Kodu:
```sql
-- Race condition önleme: UPDLOCK + ROWLOCK
BEGIN TRANSACTION;
    SELECT balance, blocked_balance
    FROM wallets WITH (UPDLOCK, ROWLOCK)
    WHERE id = @WalletId AND is_active = 1;

    IF @balance >= @amount
    BEGIN
        UPDATE wallets
        SET balance = balance - @amount,
            blocked_balance = blocked_balance + @amount
        WHERE id = @WalletId;
    END
COMMIT;
```

#### Kafka Events:
- Dinler: `customer.registered` → Cüzdan oluştur (pasif), `customer.kyc_approved` → Cüzdanı aktif et
- Publish: `provision.created`, `payment.completed`, `payment.reversed`

---

### Faz 4 — QR Code Service

**Amaç:** 90 saniyelik, tek kullanımlık, finansal veri içermeyen QR token üretimi.

#### Redis Yapısı:
```
KEY:   qr:{uuid-token}
VALUE: {
    "token": "4a1b2c3d-...",
    "merchantId": "...",
    "terminalId": "TID001",
    "amount": 250.00,
    "currency": "TRY",
    "status": "ACTIVE",  // ACTIVE | PROCESSING | USED | EXPIRED
    "createdAt": "2026-06-16T10:00:00Z"
}
TTL:   90 saniye
```

#### API Endpoint'leri:
| Method | Endpoint | Açıklama | Role |
|--------|----------|----------|------|
| POST | `/v1/qr/generate` | Terminal için QR token üret | Terminal |
| GET | `/v1/qr/validate` | Token geçerliliğini kontrol et | Customer App |
| PUT | `/v1/qr/{token}/status` | Token durumunu güncelle | System |

#### İş Kuralları:
- Token = UUID v4, Redis'e TTL=90sn ile yaz
- Finansal bilgi (tutar) Redis'te tutulur, QR kodun içinde **olmaz**
- `ACTIVE → PROCESSING`: Müşteri taradığında (race condition önleme: Redis SETNX)
- `PROCESSING → USED`: Ödeme tamamlandığında
- `PROCESSING → ACTIVE`: Ödeme başarısız olduğunda (release)
- Expired token isteği → `410 Gone` döndür

---

### Faz 5 — Transaction Service + ISO 8583 Simülatörü

**Amaç:** Ödeme akışının bankacılık ayağı ve banka simülatörü. En karmaşık servis.

#### Veritabanı Şeması:
```sql
-- transaction_db
CREATE TABLE transactions (
    id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    wallet_id       UNIQUEIDENTIFIER NOT NULL,
    merchant_id     UNIQUEIDENTIFIER NOT NULL,
    terminal_id     NVARCHAR(50) NOT NULL,
    qr_token        UNIQUEIDENTIFIER NOT NULL UNIQUE,
    amount          DECIMAL(18,4) NOT NULL,
    status          NVARCHAR(50) NOT NULL DEFAULT 'PENDING',
                    -- PENDING | ISO_SENT | SUCCESS | FAILED | REVERSED
    iso_resp_code   NVARCHAR(10),       -- Field 39: "00"=OK, "51"=Yetersiz bakiye
    stan            NVARCHAR(12),       -- System Trace Audit Number
    rrn             NVARCHAR(12),       -- Retrieval Reference Number
    created_at      DATETIME2 DEFAULT GETUTCDATE(),
    completed_at    DATETIME2,
    reversal_at     DATETIME2
);

CREATE TABLE iso_message_log (
    id          BIGINT IDENTITY PRIMARY KEY,
    tx_id       UNIQUEIDENTIFIER NOT NULL,
    msg_type    NVARCHAR(10) NOT NULL,   -- 0200 | 0210 | 0420 | 0430
    direction   NVARCHAR(10) NOT NULL,   -- SENT | RECEIVED
    payload     NVARCHAR(MAX),           -- Maskelenmiş ISO mesaj
    created_at  DATETIME2 DEFAULT GETUTCDATE()
);
```

#### Ödeme Akışı (Sequence):
```
1. POST /v1/payments/confirm gelir
2. QR token → Redis'ten doğrula (PROCESSING durumda mı?)
3. WalletService'e provision isteği gönder (HTTP veya Kafka)
4. Provision OK → ISO 8583 (0200) mesajı simülatöre TCP/TLS ile gönder
5a. Simülatör 0210 döndürür → Field 39 = "00" (Başarılı)
    → WalletService: provision CONFIRM
    → Transaction status = SUCCESS
    → Kafka: payment.completed event yayınla
    → SignalR: Kasaya "Başarılı" bildir
5b. Simülatör 0210 döndürür → Field 39 ≠ "00" (Başarısız: 51=Yetersiz bakiye, 91=Banka hatası)
    → WalletService: provision RELEASE
    → Transaction status = FAILED
    → Kafka: payment.failed event yayınla
    → SignalR: Kasaya "Hatalı" bildir
5c. Timeout (30sn) veya bağlantı hatası
    → WalletService: provision RELEASE
    → Transaction status = REVERSED
    → Simülatöre 0420 REVERSAL mesajı gönder
    → SignalR: Kasaya "İptal" bildir
```

#### API Endpoint'leri:
| Method | Endpoint | Açıklama | Role |
|--------|----------|----------|------|
| POST | `/v1/payments/confirm` | Ödeme akışını başlat | Customer App |
| GET | `/v1/payments/{id}` | İşlem durumu sorgula | Customer |
| GET | `/v1/payments/{id}/receipt` | PDF makbuz indir | Customer |

#### ISO 8583 Simülatör (BankSimulator projesi):
- ASP.NET Core IHostedService olarak TCP listener
- Konfigüre edilebilir senaryolar: `00` (başarılı), `51` (yetersiz bakiye), `91` (timeout), rastgele hata
- Her işlem için simülasyon gecikmesi ayarlanabilir (gerçekçi test için 200-500ms)
- Reversal (0420) mesajlarını kabul eder ve 0430 ile cevap verir

#### Servis Özellikleri:
- Custom TCP/TLS socket client (IHostedService)
- Connection pool: Simülatörle persistent bağlantı
- STAN: Her işlem için artan sıralı numara (thread-safe, Interlocked)
- Timeout politikası: 30sn bekleme, sonra Reversal (0420)

---

### Faz 6 — Reporting & Reconciliation Service

**Amaç:** Raporlama, gün sonu mutabakat, PDF makbuz.

#### Elasticsearch Index Yapısı:
```json
// Index: transactions-2026.06.16
{
    "transaction_id": "uuid",
    "wallet_id": "uuid",
    "merchant_id": "uuid",
    "terminal_id": "TID001",
    "amount": 250.00,
    "status": "SUCCESS",
    "iso_resp_code": "00",
    "timestamp": "2026-06-16T10:05:00Z"
}
```

#### API Endpoint'leri:
| Method | Endpoint | Açıklama | Role |
|--------|----------|----------|------|
| GET | `/report/customer/statement` | Müşteri ekstre + harcama özeti | Customer |
| GET | `/report/merchant/daily-summary` | Gün sonu satış raporu | Merchant |
| GET | `/report/reconciliation` | Banka mutabakat raporu | Admin |
| GET | `/report/receipt/{tx_id}` | PDF makbuz (QuestPDF) | Customer |

#### Kafka Consumer'lar:
- `payment.completed` → Elasticsearch'e yaz + PDF makbuz oluştur + FCM push notification tetikle
- `payment.failed` → Elasticsearch'e yaz
- `payment.reversed` → Elasticsearch'e yaz + Reversal kaydı

#### Firebase FCM Entegrasyonu:
- `payment.completed` eventi geldiğinde `FirebaseAdmin` SDK ile push gönderilir
- Müşteri cihazındaki FCM token `customers` tablosunda saklanır
- Bildirim içeriği: "Ödemeniz tamamlandı — {merchant_name} | {amount} TRY"

---

### Faz 7 — React Native Müşteri Mobil Uygulaması

**Amaç:** Müşterinin elindeki uygulama. QR tarama + cüzdan yönetimi.

#### Ekranlar ve Geliştirme Sırası:
```
1. Splash & Onboarding
   └── SplashScreen → RegisterScreen → OtpScreen → KycScreen → HomeScreen

2. Auth Modülü
   └── LoginScreen (PIN + TOTP) → FaceID/TouchID entegrasyonu

3. Home (Ana Ekran)
   └── Bakiye kartı + Hızlı işlem butonları + Son işlemler listesi

4. QR Tarama Akışı
   └── CameraScreen (react-native-vision-camera) → PaymentConfirmSheet → ResultScreen

5. Cüzdan Yönetimi
   └── TopupScreen (tutar gir) → PaymentMethodSelect → SuccessScreen

6. Geçmiş & Makbuz
   └── StatementScreen (FlashList, infinite scroll) → ReceiptDetailScreen (PDF viewer)
```

#### Teknik Gereksinimler:
| Paket | Amaç |
|-------|------|
| `react-native-vision-camera` + `vision-camera-code-scanner` | QR kod okuma (kamera) |
| `react-native-biometrics` | FaceID / TouchID |
| `react-native-keychain` | JWT token güvenli saklama (Keychain/Keystore) |
| `axios` + interceptor | Token yenileme (refresh flow) |
| `@microsoft/signalr` | WebSocket — push bildirim ve ödeme durumu |
| `@react-native-firebase/messaging` | FCM push notification |
| `react-native-pdf` | PDF makbuz görüntüleme |
| `@react-navigation/native` + stack/bottom-tabs | Navigasyon |
| `zustand` | Global state management |
| `react-native-reanimated` | Akıcı animasyonlar |
| `@shopify/flash-list` | Performanslı liste (işlem geçmişi) |
| `react-hook-form` + `zod` | Form validasyonu |

---

### Faz 8 — Next.js İşyeri Web Paneli

**Amaç:** İşyeri yöneticilerinin raporları görüntülediği, KYC onayladığı, başvuru yaptığı panel.

#### Sayfalar:
```
/login                    → İşyeri girişi (email/şifre)
/dashboard                → Genel bakış (bugünkü satış, işlem sayısı)
/reports/daily            → Günlük satış raporu + grafik
/reports/transactions     → İşlem listesi (filtrelenebilir, aranabilir)
/reconciliation           → Banka mutabakat sayfası (Admin)
/kyc                      → Bekleyen KYC başvurularını listele ve onayla (Admin)
/onboarding               → İşyeri başvuru formu
/settings/terminals       → Terminal yönetimi
/settings/profile         → Profil & IBAN güncelleme
```

#### Teknik Gereksinimler:
- Next.js 14 App Router
- shadcn/ui + Tailwind CSS
- Recharts: Satış grafikleri
- TanStack Query: Server state yönetimi
- NextAuth.js: Session yönetimi
- React Hook Form + Zod: Form validasyonu

---

### Faz 9 — React POS Terminal UI

**Amaç:** Kasa ekranında çalışan, tutar girip QR üreten, sonucu gösteren web uygulaması.

#### Ekranlar:
```
1. AmountEntry    → Sayısal tuş takımı, tutar giriş
2. QrDisplay      → Üretilen QR kodu büyük ekranda göster (90sn countdown)
3. Processing     → "Ödeme Bekleniyor..." animasyonu (SignalR bekleme)
4. PaymentSuccess → Yeşil ekran "Ödeme Başarılı — Fiş Yazdırılıyor"
5. PaymentFailed  → Kırmızı ekran "İşlem Başarısız"
```

#### Teknik Gereksinimler:
- React + Vite + TypeScript
- `@microsoft/signalr`: WebSocket (ödeme sonucu anlık bildirim)
- `qrcode.react`: QR kodu gösterimi
- Tailwind CSS: Basit, okunabilir kasa arayüzü

---

### Faz 10 — Production Hardening

**Amaç:** Sistemin production'a hazır hale getirilmesi.

#### Görevler:
- [ ] Kubernetes Helm chart'ları hazırlama
- [ ] HPA (Horizontal Pod Autoscaler) konfigürasyonu
- [ ] Secrets yönetimi: Kubernetes Secrets / HashiCorp Vault
- [ ] Kong rate limiting kuralları
- [ ] mTLS sertifika zinciri kurulumu (terminal → API Gateway)
- [ ] ELK Stack log toplama (Logstash → Elasticsearch → Kibana)
- [ ] Distributed tracing: OpenTelemetry + Jaeger
- [ ] Veritabanı yedekleme politikası (MSSQL Always On / AG)
- [ ] Load testing: k6 ile stres testi
- [ ] BDDK / PCI DSS uyum kontrol listesi
- [ ] Penetrasyon testi senaryoları

---

## 4. Faz Sırası ve Bağımlılıklar

```
Faz 0  Altyapı & Dev Ortamı
    ↓
Faz 1  Auth Service
    ↓
Faz 2  Onboarding Service
    ↓
Faz 3  Wallet Service  ←→  Faz 4  QR Code Service
              ↓                          ↓
         Faz 5  Transaction Service + ISO 8583 Simülatörü
                        ↓
                   Faz 6  Reporting Service

Frontend (backend paralel ilerledikçe başlanabilir):
    Faz 9  POS Terminal UI    ← Faz 4 tamamlanınca
    Faz 7  React Native App   ← Faz 1 + 3 + 5 tamamlanınca
    Faz 8  Merchant Panel     ← Faz 2 + 6 tamamlanınca

Faz 10  Production Hardening  ← Tüm fazlar tamamlanınca
```

---

## 5. Test Stratejisi

| Katman | Araç | Kapsam |
|--------|------|--------|
| Unit Test | xUnit + Moq (.NET) | Service logic, domain kuralları |
| Integration Test | TestContainers | DB + Redis + Kafka ile gerçek entegrasyon |
| API Test | Bruno / Postman Collection | Tüm endpoint'ler |
| ISO 8583 Simülatör | BankSimulator (Faz 5) | `00`, `51`, `91`, timeout senaryoları |
| E2E Test | Playwright (web) | Merchant Panel ve POS Terminal akışları |
| Mobile Test | Jest + React Native Testing Library | Bileşen ve hook testleri |
| Load Test | k6 | Yüksek işlem yükü simülasyonu |

### Test Prensipleri:
- Veritabanı testleri mock'lanmaz, TestContainers ile gerçek MSSQL kullanılır
- Her PR'da CI pipeline otomatik test koşturur
- ISO 8583 için BankSimulator projesi geliştirilir, gerçek banka bağlantısı beklenmez

---

*Plan onaylandı. Kodlamaya Faz 0 ile başlanacak.*
