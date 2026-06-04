# QR Ödeme Sistemi — Dokümantasyon Index

> **Status:** ✅ Tamamlandı  
> **Son Güncelleme:** 2026-06-04  
> **Mimari:** Mikroservis | Event-Driven | ISO 8583 | .NET 10

---

## Modül Listesi

| # | Klasör | Kapsam | Durum |
|---|---|---|---|
| 00 | `00-overview/` | Genel sistem mimarisi, aktörler, tech stack | ✅ [SYSTEM-OVERVIEW](../SYSTEM-OVERVİEW.md) |
| 01 | [`01-auth-service/`](01-auth-service/README.md) | OAuth 2.0, JWT, mTLS, Terminal doğrulama | ✅ Tamamlandı |
| 02 | [`02-onboarding-service/`](02-onboarding-service/README.md) | Müşteri kaydı, KYC, İşyeri tanımlama | ✅ Tamamlandı |
| 03 | [`03-wallet-service/`](03-wallet-service/README.md) | Double-Entry Bookkeeping, Top-up, Bloke/Provision | ✅ Tamamlandı |
| 04 | [`04-qr-code-service/`](04-qr-code-service/README.md) | Dinamik QR üretimi, Redis TTL, UUID lifecycle | ✅ Tamamlandı |
| 05 | [`05-transaction-service/`](05-transaction-service/README.md) | ISO 8583 akışı, 0200/0210/0420, Kafka events | ✅ Tamamlandı |
| 06 | [`06-reporting-service/`](06-reporting-service/README.md) | Mutabakat, Ledger, dijital makbuz, Elasticsearch | ✅ Tamamlandı |
| 07 | [`07-infrastructure/`](07-infrastructure/README.md) | API Gateway, Kafka topic tasarımı, Redis, MSSQL | ✅ Tamamlandı |
| 08 | [`08-security/`](08-security/README.md) | RBAC, HMAC-SHA256, OWASP Top 10, veri koruma | ✅ Tamamlandı |
| 09 | [`09-data-models/`](09-data-models/README.md) | ER diyagramları, tablo şemaları, Ledger akışı | ✅ Tamamlandı |
| 10 | [`10-deployment/`](10-deployment/README.md) | Docker, Kubernetes, CI/CD, deployment fazları | ✅ Tamamlandı |
| 11 | [`11-adr/`](11-adr/README.md) | 7 Architecture Decision Record (ADR-001 → ADR-007) | ✅ Tamamlandı |

