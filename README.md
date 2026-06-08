# QR Payment System

Banka, Üye İşyeri (Merchant) ve Müşteri arasında güvenli, hızlı ve **QR kod tabanlı anlık ödeme altyapısı**. Finansal veriler QR kod içerisinde taşınmaz; bunun yerine 90 saniye ömrü olan dinamik UUID token'lar kullanılır. Sistem KVKK, MASAK (AML) ve PCI-DSS prensipleri gözetilerek tasarlanmıştır.

---

## Mimari Genel Bakış

```
Müşteri Mobil App
        │
        ▼
Kong API Gateway (TLS 1.3 · RBAC · Rate Limit)
        │
        ├──▶ Auth Service
        ├──▶ Onboarding Service
        ├──▶ Wallet & Account Service
        ├──▶ QR Code Service
        ├──▶ Transaction Service
        └──▶ Reporting & Reconciliation Service
                      │
              Apache Kafka (Event Bus)
                      │
              Elasticsearch (Raporlama)

Transaction Service ──── ISO 8583 / TCP-TLS ────▶ Banka Core Sistemi
QR Code Service    ──── WebSocket (SignalR) ────▶ POS / Kasa Ekranı
```

**Temel prensipler:**
- **Mikroservis Mimarisi** — her servis kendi veritabanını yönetir, servisler arası doğrudan DB JOIN yapılmaz
- **Event-Driven (Kafka)** — servisler Kafka event'leri üzerinden gevşek bağlı iletişim kurar
- **Double-Entry Bookkeeping** — her finansal hareket DEBIT + CREDIT çiftiyle ACID garantisiyle yazılır
- **Database-per-Service** — veri izolasyonu, bağımsız ölçekleme

---

## Servisler

### 1. Auth Service
JWT (RS256 asimetrik imzalama) ve OAuth 2.0 tabanlı kimlik doğrulama merkezi. Private Key yalnızca bu serviste tutulur; diğer servisler Public Key ile token doğrular.

- Access Token ömrü: **15 dakika**
- Refresh Token ömrü: **7 / 30 gün**
- POS terminal doğrulaması: **mTLS + HMAC-SHA256**

### 2. Onboarding Service
Yeni müşteri ve üye işyeri kayıt süreçlerini yönetir. KYC belgeleri AES-256 ile Blob Storage'da şifreli saklanır. MASAK uyumlu AML kontrolleri bu servis tarafından çalıştırılır.

### 3. Wallet & Account Service
Sistemin finansal çekirdeği. Bakiye yükleme (top-up), QR okutulduğunda tutarın geçici bloke edilmesi (provizyon) ve çift taraflı muhasebe kayıtları bu servise aittir.

**Temel tablolar:**
| Tablo | Açıklama |
|---|---|
| `wallets` | `balance` ve `blocked_balance` anlık takibi |
| `ledger_entries` | Her harekete karşılık DEBIT + CREDIT çifti (BIGINT IDENTITY ile sıralı) |

Finansal kayıtlar asla silinmez veya güncellenmez; ters kayıtla düzeltilir.

### 4. QR Code Service
Redis üzerinde 90 saniyelik TTL ile dinamik UUID QR token üretir. Token içinde finansal veri taşınmaz. QR okunduktan sonra tek kullanımlık hale getirilir; süresi dolunca Redis TTL mekanizmasıyla otomatik silinir.

**Endpoint'ler:**
- `POST /v1/qr/generate` — Terminal dinamik QR üretir
- `GET /qr/validate/:token` — Müşteri QR geçerliliğini sorgular

### 5. Transaction Service
ISO 8583 formatında TCP/TLS soket bağlantısı üzerinden banka ana sistemiyle haberleşen ödeme çekirdeği. Timeout veya hata durumunda `0420 Auto-Reversal` mesajı otomatik tetiklenir.

**Kafka event'leri:**
- `payment.success` → Wallet Service blokeyi kesinleştirir, Reporting makbuz üretir
- `payment.failed` → Wallet Service blokeyi serbest bırakır
- `payment.reversed` → Çift taraflı ters kayıt atılır

### 6. Reporting & Reconciliation Service
Kafka event'lerini dinleyerek Elasticsearch 8'e yazar. MASAK zorunluluğuyla veriler **10 yıl** ILM politikasıyla saklanır. Her gece banka bakiyeleriyle cüzdan bakiyelerini karşılaştıran mutabakat (reconciliation) çalıştırılır.

**Endpoint'ler:**
- `GET /report/customer/statement`
- `GET /report/merchant/daily-summary`
- `GET /report/reconciliation`

---

## Teknoloji Yığını

| Katman | Teknoloji | Neden |
|---|---|---|
| Backend | **.NET 10 (C#)** | Native AOT performansı, ekip yetkinliği |
| Finansal DB | **MSSQL Server 2022** | ACID garantisi, DECIMAL(18,2) hassasiyeti |
| Cache / TTL | **Redis 7** | 90s QR TTL, atomik operasyonlar, token blacklist |
| Message Broker | **Apache Kafka** | Event replay, yüksek throughput, log-based yapı |
| Arama / Raporlama | **Elasticsearch 8** | Full-text arama, ILM ile 10 yıl saklama |
| Gerçek Zamanlı | **SignalR (WebSocket)** | POS ekranına milisaniyelik ödeme bildirimi |
| API Gateway | **Kong** | Tek giriş noktası, RBAC, rate limiting |
| Container | **Docker + Kubernetes** | Bağımsız ölçekleme, HPA, self-healing |

---

## Güvenlik Mimarisi

| Katman | Yöntem |
|---|---|
| Tüm iletişim | TLS 1.3 |
| Kullanıcı token | RS256 asimetrik JWT |
| POS terminal | mTLS + HMAC-SHA256, Replay Attack koruması |
| Parola saklama | BCrypt (cost=12) |
| TCKN saklama | SHA-256 hash (düz metin yok) |
| KYC belgeleri | AES-256 şifrelemeli Blob Storage |
| Log maskeleme | Telefon numarası ve kişisel veriler maskelenir |
| Yetkilendirme | RBAC + Row-Level Security (JWT'deki `wallet_id`) |

---

## API Endpoint'leri (Özet)

```
POST   /auth/token                    # Token üretimi
GET    /wallet/balance                # Bakiye sorgulama
POST   /wallet/topup                  # Bakiye yükleme
POST   /wallet/provision              # Provizyon / bloke koyma
POST   /v1/qr/generate                # Dinamik QR üretimi
GET    /qr/validate/:token            # QR doğrulama
POST   /v1/payments/confirm           # ISO 8583 ödeme başlatma
GET    /report/customer/statement     # Müşteri ekstresi
GET    /report/merchant/daily-summary # İşyeri gün sonu
GET    /report/reconciliation         # Günlük mutabakat
```

---

## Deployment

Sıfır kesinti (zero-downtime) için Docker + Kubernetes. Multi-stage Dockerfile ile build imajı ağır SDK içerirken runtime imajı minimal tutulur.

**5 Fazlı Yol Haritası:**

| Faz | Kapsam |
|---|---|
| 1 | Temel altyapı: Auth Service, Kong Gateway, MSSQL |
| 2 | QR üretimi, Wallet Service, Redis entegrasyonu |
| 3 | Transaction Service, ISO 8583 banka ödeme akışı |
| 4 | Kafka asenkron iletişim, SignalR kasa bildirimleri |
| 5 | Elasticsearch raporlama, mutabakat, yük testleri, Go-live |

**Kubernetes yapılandırması:**
- HPA (Horizontal Pod Autoscaler) — CPU/bellek eşiğine göre otomatik ölçekleme
- Readiness + Liveness probları
- Her servis bağımsız namespace

---

## Architecture Decision Records (ADR)

| ADR | Karar | Gerekçe |
|---|---|---|
| ADR-001 | Mikroservis Mimarisi | Servisler farklı trafik yükü taşır; bağımsız ölçekleme gerekli |
| ADR-002 | Apache Kafka | RabbitMQ'ya göre event replay ve log-based yapı avantajı |
| ADR-003 | RS256 JWT | Diğer servislerin private key'i bilmemesi güvenlik izolasyonu sağlar |
| ADR-004 | Custom ISO 8583 Parser | Banka spesifikasyonuna bağımlılık; karar PoC sonrası netleşecek |
| ADR-005 | Redis (QR Token) | TTL ile otomatik silme, atomik operasyon, milisaniyelik hız |
| ADR-006 | Kubernetes | Otomatik ölçekleme ve self-healing kabiliyeti |
| ADR-007 | Elasticsearch | MSSQL'in yavaş kaldığı text-based aramalar ve ILM ile uzun süreli saklama |
| ADR-008 | .NET 10 (C#) | Native AOT performansı ve ekip yetkinliği |

---

## Dokümantasyon ve Yapay Zeka Entegrasyonu

Bu proje, mimari tasarım ve dokümantasyon sürecinde **Claude (Anthropic)** ve **Google NotebookLM** entegrasyonundan yararlanmaktadır.

### Nasıl Çalışıyor?

```
Proje Dosyaları (.md)
        │
        ▼
  notebooklm-py CLI           Claude Code (Terminal)
        │                              │
        ▼                              ▼
Google NotebookLM ◀────────── notebooklm ask / generate
(16 kaynak, RAG motoru)
        │
        ├──▶ Doğal dil sorgulama (notebooklm ask "...")
        ├──▶ Podcast, quiz, flashcard üretimi
        ├──▶ İnfografik oluşturma
        └──▶ Rapor ve study guide üretimi
```

**Claude Code** terminal üzerinden `notebooklm-py` CLI'ı yöneterek:
- Proje dökümanlarını NotebookLM'e otomatik olarak yükler
- NotebookLM'e doğal dil sorguları gönderir ve yanıtları koda/belgeye dönüştürür
- Görsel içerik (infografik), ses içeriği (podcast) ve quiz gibi çıktılar üretir

**Google NotebookLM** ise 16 markdown kaynaklarıyla beslenen bir **RAG (Retrieval-Augmented Generation)** motoru olarak çalışır. Mimari kararlar, servis detayları ve veri modellerine Claude Code üzerinden `notebooklm ask` komutuyla anlık erişilebilir.

**Aktif notebook:** `QR Payment System — Full Documentation`
- 16 kaynak (README dosyaları, sistem genel bakışı, araştırma notları)
- Durum: Tüm kaynaklar `ready`

**Örnek kullanım:**
```bash
# Servis hakkında soru sor
notebooklm ask "Transaction Service ISO 8583 mesajlarını nasıl işliyor?"

# İnfografik oluştur
notebooklm generate infographic --orientation landscape --detail detailed

# Podcast üret
notebooklm generate audio "QR ödeme akışı üzerine teknik deep-dive"
```

---

## Geliştirme Ortamı

```bash
# NotebookLM CLI kurulu ise dokümanlara doğrudan sor
export PATH="$HOME/bin:$PATH"
notebooklm use 39faa48f-4583-4878-b7d5-6d93a912012d
notebooklm ask "Wallet Service provizyon akışını açıkla"
```

Detaylı kurulum için: [notebooklm-py](https://github.com/jgravelle/notebooklm-py)

---

## Lisans

Bu proje özel (proprietary) kullanım içindir.
