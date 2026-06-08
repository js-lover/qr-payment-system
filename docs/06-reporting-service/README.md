# Reporting & Ledger Service — Mutabakat, Raporlama ve Makbuz Üretimi

> **Related Modules:**
> - [`../03-wallet-service/`](../03-wallet-service/README.md) — Ledger eventleri buradan tüketilir.
> - [`../05-transaction-service/`](../05-transaction-service/README.md) — `PaymentSuccessEvent`, `ReversalInitiatedEvent` consume edilir.
> - [`../07-infrastructure/`](../07-infrastructure/README.md) — Kafka consumer group konfigürasyonu, Elasticsearch setup.
> - [`../09-data-models/`](../09-data-models/README.md) — Reporting veri modeli ve sorgu optimizasyonları.

---

## 1. Purpose & Scope (Amaç ve Kapsam)

Reporting & Ledger Service, sistemin **geriye dönük hafızasıdır**. İşlem eventlerini Kafka'dan tüketerek sorgulama için optimize edilmiş bir veri deposuna yazar; düzenli mutabakat (reconciliation) yapar ve tüm taraflara (müşteri, merchant, operasyon ekibi) anlamlı raporlar sunar.

**Temel prensipler:**

| Prensip | Açıklama |
|---|---|
| **Read-Optimized** | Ledger Service write-heavy sistemden ayrı; Elasticsearch ile hızlı tam-metin arama. |
| **Eventual Consistency** | Kafka event'leri tüketilir; veriler milisaniyeler içinde (gerçek zamanlıya yakın) raporlanır. |
| **Immutable Events** | Kafka'dan gelen event'ler aynen saklanır; düzeltme yeni event ile yapılır. |
| **Daily Reconciliation** | Her gece banka ve wallet bakiyeleri karşılaştırılır; fark varsa uyarı üretilir. |

**Kapsam dahilindeki sorumluluklar:**
- Kafka consumer: `PaymentSuccessEvent`, `PaymentFailedEvent`, `ReversalInitiatedEvent`, `WalletCreditedEvent`
- Elasticsearch'e olay yazma ve indeksleme
- Müşteri ekstresi (özet ve detay)
- Merchant gün sonu raporu
- Operasyon dashboardı (işlem hacimleri, hata oranları)
- Dijital makbuz üretimi (PDF)
- Günlük mutabakat (Daily Reconciliation)

**Kapsam dışı:**
- Para hareketi ve bakiye → `03-wallet-service`
- ISO 8583 işlemi → `05-transaction-service`

---

## 2. Architecture & Bounded Context (Mimari ve Sınırlar)

```mermaid
graph TD
    subgraph Kafka [Apache Kafka - Event Streams]
        E1[PaymentSuccessEvent]
        E2[PaymentFailedEvent]
        E3[ReversalInitiatedEvent]
        E4[WalletCreditedEvent]
    end

    subgraph ReportingService [Reporting & Ledger Service - .NET 10]
        C1[Kafka Consumer Group - reporting-group]
        C2[Event Normalizer]
        C3[Elasticsearch Writer]
        C4[Reconciliation Worker]
        C5[Receipt Generator - PDF]
        C6[Report API]
    end

    subgraph Storage
        ES[(Elasticsearch - transactions index)]
        MSSQL[(MSSQL - reconciliation_db)]
    end

    subgraph Consumers
        D1["📱 Müşteri App - Ekstre"]
        D2["🏪 Merchant Portal"]
        D3["🛠️ Ops Dashboard"]
    end

    E1 & E2 & E3 & E4 --> C1
    C1 --> C2 --> C3 --> ES
    C2 --> MSSQL
    C4 --> MSSQL
    C5 --> ES
    C6 --> ES & MSSQL
    C6 --> D1 & D2 & D3
```

### Elasticsearch Index Mimarisi

```mermaid
graph LR
    subgraph Indices
        I1["transactions - gunluk rollover"]
        I2["wallet_events - Topup Provision Commit"]
        I3["merchant_summaries - Gunluk ozet"]
    end
```

---

## 3. Data Flow & Actors (Veri Akışı ve Aktörler)

### 3.1 Event Tüketimi ve Yazma Akışı

```mermaid
sequenceDiagram
    participant Kafka as Apache Kafka
    participant Consumer as Kafka Consumer
    participant Normalizer as Event Normalizer
    participant ES as Elasticsearch
    participant DB as MSSQL (reconciliation_db)

    Kafka->>Consumer: PaymentSuccessEvent\n{transaction_id, qr_token, amount,\nmerchant_id, wallet_id, fee, timestamp}

    Consumer->>Normalizer: Enrich event
    Note over Normalizer: merchant_name lookup (cache)\nCustomer display name\nFormatted amount string

    Normalizer->>ES: Index document\n{index: "transactions-2026.05.25"}\n{\n  "transaction_id": "...",\n  "type": "QR_PAYMENT",\n  "status": "SUCCESS",\n  "amount": 50.00,\n  "fee": 1.50,\n  "merchant_name": "Ahmet Market",\n  "timestamp": "2026-05-25T18:00:00Z"\n}

    Normalizer->>DB: INSERT daily_summaries (upsert)\n{date, merchant_id, total_amount, tx_count}

    Consumer-->>Kafka: Commit offset
```

### 3.2 Müşteri Ekstresi Sorgusu

```mermaid
sequenceDiagram
    participant App as 📱 Müşteri App
    participant API as Report API
    participant ES as Elasticsearch

    App->>API: GET /report/customer/statement\n?wallet_id=X&from=2026-05-01&to=2026-05-25\nAuthorization: Bearer <JWT>

    API->>API: JWT doğrula (sub == wallet_id.owner)

    API->>ES: POST /transactions/_search\n{\n  "query": {\n    "bool": {\n      "must": [\n        {"term": {"wallet_id": "X"}},\n        {"range": {"timestamp": {"gte": "...", "lte": "..."}}}\n      ]\n    }\n  },\n  "sort": [{"timestamp": "desc"}],\n  "size": 50\n}

    ES-->>API: [{...}, {...}, ...]

    API-->>App: 200 OK {\n  "total_spent": 450.00,\n  "transactions": [\n    {type: QR_PAYMENT, merchant: "Ahmet Market", amount: -50.00, date: "..."},\n    {type: TOPUP, amount: +200.00, date: "..."}\n  ]\n}
```

### 3.3 Merchant Gün Sonu Raporu

```mermaid
sequenceDiagram
    participant Portal as 🏪 Merchant Portal
    participant API as Report API
    participant ES as Elasticsearch
    participant DB as MSSQL

    Portal->>API: GET /report/merchant/daily-summary\n?merchant_id=MERCH-XOX-999&date=2026-05-25

    API->>DB: SELECT * FROM daily_summaries\nWHERE merchant_id = ? AND date = ?
    DB-->>API: {total_amount: 12500.00, tx_count: 248, fee: 375.00, net: 12125.00}

    API->>ES: Aggregation query — saatlik dağılım
    ES-->>API: hourly_buckets: [{hour:9, count:45}, ...]

    API-->>Portal: 200 OK {\n  "date": "2026-05-25",\n  "gross_revenue": 12500.00,\n  "total_fee": 375.00,\n  "net_revenue": 12125.00,\n  "transaction_count": 248,\n  "hourly_distribution": [...]\n}
```

### 3.4 Günlük Mutabakat (Daily Reconciliation)

```mermaid
sequenceDiagram
    participant Worker as Reconciliation Worker\n(Her gece 02:00)
    participant ES as Elasticsearch
    participant WalletDB as MSSQL (wallet_db)
    participant DB as MSSQL (reconciliation_db)
    participant Alert as Alert System

    Note over Worker: Gece 02:00 — Cron trigger

    Worker->>ES: SUM(amount) WHERE date=yesterday AND status=SUCCESS
    ES-->>Worker: es_total = 1.234.567,00 TL

    Worker->>WalletDB: SELECT SUM(amount) FROM ledger_entries\nWHERE date=yesterday AND reference_type=QR_PAYMENT
    WalletDB-->>Worker: wallet_total = 1.234.567,00 TL

    alt Tutarlar Eşleşiyor
        Worker->>DB: INSERT reconciliation_report\n{date, status=MATCHED, es_total, wallet_total, diff=0}
    else Tutarsızlık Var
        Worker->>DB: INSERT reconciliation_report\n{date, status=MISMATCH, diff=X}
        Worker->>Alert: 🚨 PagerDuty / Slack Alert\n"Mutabakat farkı: X TL"
    end
```

### 3.5 Dijital Makbuz Üretimi

```mermaid
sequenceDiagram
    participant App as 📱 Müşteri App
    participant API as Report API
    participant ES as Elasticsearch
    participant PDF as PDF Generator

    App->>API: GET /report/receipt/{transaction_id}

    API->>ES: GET transaction by ID
    ES-->>API: {merchant_name, amount, timestamp, auth_code, ...}

    API->>PDF: Generate PDF\n[Logo | İşyeri Adı | Tutar | Tarih | İşlem No | QR Onay Kodu]
    PDF-->>API: receipt.pdf (binary)

    API-->>App: 200 OK Content-Type: application/pdf
```

---

## 4. Dependencies & Integrations (Bağımlılıklar)

| Bileşen | Teknoloji | Kullanım Amacı |
|---|---|---|
| **Event Tüketimi** | Apache Kafka | Tüm ödeme olaylarını consume etme. |
| **Arama Motoru** | Elasticsearch | Hızlı filtreleme, agregasyon, özet sorgular. |
| **İlişkisel Veri** | MSSQL Server | Mutabakat tabloları, günlük özetler. |
| **PDF Üretimi** | `QuestPDF` (.NET) | Dijital makbuz oluşturma. |
| **Zamanlama** | .NET BackgroundService + Cron | Gece mutabakat işi. |
| **Cache** | Redis | Merchant adı lookup cache (ES önünde). |

### Elasticsearch Index Mapping (transactions)

```json
{
  "mappings": {
    "properties": {
      "transaction_id":  { "type": "keyword" },
      "qr_token":        { "type": "keyword" },
      "type":            { "type": "keyword" },
      "status":          { "type": "keyword" },
      "wallet_id":       { "type": "keyword" },
      "merchant_id":     { "type": "keyword" },
      "merchant_name":   { "type": "text", "fields": { "keyword": { "type": "keyword" } } },
      "amount":          { "type": "double" },
      "fee":             { "type": "double" },
      "iso_resp_code":   { "type": "keyword" },
      "timestamp":       { "type": "date" }
    }
  }
}
```

### MSSQL Şema — Reconciliation DB

```sql
CREATE TABLE daily_summaries (
    id              BIGINT IDENTITY PRIMARY KEY,
    summary_date    DATE NOT NULL,
    merchant_id     NVARCHAR(64),
    total_amount    DECIMAL(18,2) NOT NULL DEFAULT 0,
    total_fee       DECIMAL(18,2) NOT NULL DEFAULT 0,
    net_amount      DECIMAL(18,2) NOT NULL DEFAULT 0,
    tx_count        INT NOT NULL DEFAULT 0,
    UNIQUE (summary_date, merchant_id)
);

CREATE TABLE reconciliation_reports (
    id              BIGINT IDENTITY PRIMARY KEY,
    report_date     DATE NOT NULL UNIQUE,
    es_total        DECIMAL(18,2) NOT NULL,
    wallet_total    DECIMAL(18,2) NOT NULL,
    difference      DECIMAL(18,2) NOT NULL,
    status          VARCHAR(20) NOT NULL,    -- MATCHED | MISMATCH | PENDING
    created_at      DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

---

## 5. Failure Scenarios & Resiliency (Hata Senaryoları)

| Senaryo | Etki | Çözüm |
|---|---|---|
| **Elasticsearch down** | Raporlar erişilemiyor | Circuit Breaker; MSSQL daily_summaries fallback verisi. |
| **Kafka consumer lag** | Raporlar gecikmeli | Consumer group monitoring (Grafana); lag alert (>1000 msg). |
| **PDF oluşturma hatası** | Makbuz alınamıyor | Async retry; HTML fallback (makbuz yerine ekran görüntüsü). |
| **Mutabakat farkı** | Finansal tutarsızlık | Otomatik alert + detaylı diff raporu; ops manuel incelemesi. |
| **Event kayıp (at-most-once)** | Bazı işlemler raporlanmıyor | `enable.auto.commit=false`; manuel offset commit sonrası yaz. |

---

## 6. Security & Compliance (Güvenlik)

| Konu | Uygulama |
|---|---|
| **Müşteri Verisi Erişimi** | Ekstre sorguları JWT `sub` claim ile wallet_id kontrolü; başkasının verisine erişilemez. |
| **Merchant Verisi İzolasyonu** | Merchant raporları API Key ile kimlik doğrulamalı; başka merchant verisi görülemez. |
| **Elasticsearch Güvenliği** | X-Pack Security aktif; role-based index erişimi. |
| **Veri Saklama Süresi** | İşlem kayıtları MASAK gereği 10 yıl; ES lifecycle policy (hot → warm → cold). |
| **PII Maskeleme** | Elasticsearch'te müşteri adı tokenized; kişisel arama yalnızca admin rolünde. |


