# Architecture Decision Records (ADR)

> ADR'lar, sistemin tasarımı sırasında alınan önemli mimari kararları ve bu kararların gerekçelerini belgelemek için kullanılır.
> Her karar bir bağlam, değerlendirilen alternatifler ve seçilen çözümü içerir.

---

## ADR Listesi

| # | Başlık | Durum | Tarih |
|---|---|---|---|
| [ADR-001](#adr-001) | Mikroservis Mimarisi Seçimi | ✅ Kabul Edildi | 2026-05-01 |
| [ADR-002](#adr-002) | Message Broker: Kafka vs RabbitMQ | ✅ Kabul Edildi | 2026-05-05 |
| [ADR-003](#adr-003) | JWT İmzalama: RS256 vs HS256 | ✅ Kabul Edildi | 2026-05-08 |
| [ADR-004](#adr-004) | ISO 8583 için .NET Library Seçimi | 🔄 Araştırılıyor | 2026-05-10 |
| [ADR-005](#adr-005) | QR Token Deposu: Redis vs MSSQL | ✅ Kabul Edildi | 2026-05-12 |
| [ADR-006](#adr-006) | Container Orchestration: Kubernetes | ✅ Kabul Edildi | 2026-05-15 |
| [ADR-007](#adr-007) | Raporlama için Elasticsearch | ✅ Kabul Edildi | 2026-05-18 |

---

## ADR-001

### Mikroservis Mimarisi Seçimi

**Durum:** ✅ Kabul Edildi — 2026-05-01

#### Bağlam

Sistem, 6 farklı iş domainini (Auth, Onboarding, Wallet, QR, Transaction, Reporting) kapsamaktadır. Başlangıçta monolitik veya mikroservis mimarisi arasında karar verilmesi gerekiyordu.

#### Değerlendirilen Alternatifler

| Seçenek | Artılar | Eksiler |
|---|---|---|
| **Monolitik** | Basit deployment, tek DB, az latency | Tüm domain'ler birbirine bağlı; bir alan değişince tümü deploy edilmeli |
| **Mikroservis** | Bağımsız ölçeklendirme, ekip bağımsızlığı, hata izolasyonu | Dağıtık sistem karmaşıklığı, network latency, eventual consistency |
| **Modüler Monolit** | Monolitin basitliği + modüler kod organizasyonu | Ölçeklendirme sınırlı; modüller arası bağımlılık riski |

#### Karar

**Mikroservis Mimarisi** seçildi.

#### Gerekçe

- Transaction Service (ISO 8583, banka bağlantısı) ile Reporting Service tamamen farklı ölçeklendirme ihtiyaçlarına sahiptir.
- Wallet Service finansal kritiklik nedeniyle diğer servislerden izole edilmelidir.
- Takım büyüdüğünde her servis bağımsız ekip tarafından geliştirilebilir.

#### Sonuçlar

- Her servis kendi veritabanını yönetir (database-per-service).
- Servisler arası iletişim Kafka event'leri veya HTTP (internal) üzerinden yapılır.
- Dağıtık sistem gerektiren Outbox Pattern, Circuit Breaker gibi desenler uygulanmalıdır.

---

## ADR-002

### Message Broker: Kafka vs RabbitMQ

**Durum:** ✅ Kabul Edildi — 2026-05-05

#### Bağlam

Mikroservisler arası asenkron iletişim için bir message broker gereklidir. İki popüler seçenek değerlendirilmiştir.

#### Değerlendirilen Alternatifler

| Özellik | Apache Kafka | RabbitMQ |
|---|---|---|
| **Model** | Log-based (mesajlar kalıcı) | Queue-based (mesaj tüketilince silinir) |
| **Replay** | ✅ Geçmiş eventleri tekrar okuyabilir | ❌ Tüketilen mesaj geri alınamaz |
| **Throughput** | Çok yüksek (milyonlarca/sn) | Yüksek (yüz binlerce/sn) |
| **Consumer Group** | Farklı gruplar bağımsız okur | Exchange + binding ile benzer yapı |
| **Öğrenme Eğrisi** | Yüksek | Düşük |
| **Operasyonel Karmaşıklık** | Yüksek (Zookeeper/KRaft) | Düşük |

#### Karar

**Apache Kafka** seçildi.

#### Gerekçe

- Reporting Service, Transaction Service ve Wallet Service aynı `payment.success` event'ini **bağımsız** olarak işlemeli. Kafka'nın consumer group yapısı bunu doğal olarak destekler.
- Günlük mutabakat (reconciliation) için geçmiş eventlerin yeniden işlenebilmesi (replay) kritik gereksinimdir — Kafka log-based yapısıyla bunu sağlar.
- Sistem büyüdüğünde yüksek throughput gereksinimi Kafka'ya ihtiyaç doğurur.

#### Sonuçlar

- 6 Kafka topic oluşturulacak (bkz. [`../07-infrastructure/`](../07-infrastructure/README.md)).
- Mesaj kaybı riskine karşı Outbox Pattern uygulanacak.
- Geliştirme ortamında Kafka yerine daha basit bir çözüm kullanılabilir (ör. Docker Compose ile tek node).

---

## ADR-003

### JWT İmzalama: RS256 vs HS256

**Durum:** ✅ Kabul Edildi — 2026-05-08

#### Bağlam

JWT token'ları imzalamak için iki yaygın algoritma bulunmaktadır. Sistemin mikroservis yapısı bu kararı önemli hale getirir.

#### Değerlendirilen Alternatifler

| Özellik | HS256 (Simetrik) | RS256 (Asimetrik) |
|---|---|---|
| **Anahtar** | Tek secret key | Private key (imzalar) + Public key (doğrular) |
| **Hız** | Daha hızlı | Biraz daha yavaş |
| **Secret paylaşımı** | Tüm servisler secret'ı bilmeli | Servisler yalnızca public key bilir |
| **Güvenlik riski** | Bir servis ele geçirilirse tüm sistem risk altında | Ele geçirilen servis token üretemez |
| **Key rotation** | Karmaşık (tüm servisleri güncelleme) | Kolayca yönetilebilir (JWKS endpoint) |

#### Karar

**RS256** seçildi.

#### Gerekçe

- Sistemde 6 mikroservis var. HS256 ile bunların hepsi aynı secret'ı bilmek zorunda kalır — bu bir güvenlik riski.
- RS256 ile yalnızca Auth Service private key'e sahip; diğer servisler yalnızca public key ile doğrulama yapar.
- JWKS (JSON Web Key Set) endpoint sayesinde public key rotasyonu servisler yeniden başlatılmadan yapılabilir.

#### Sonuçlar

- Auth Service bir RSA anahtar çifti üretir ve yönetir.
- Public key `/auth/.well-known/jwks.json` endpoint'inden yayınlanır.
- Downstream servisler token doğrulaması için bu endpoint'i kullanır.
- Key rotation için `kid` (Key ID) claim kullanılır — eski ve yeni key geçiş döneminde birlikte desteklenir.

---

## ADR-004

### ISO 8583 için .NET Library Seçimi

**Durum:** 🔄 Araştırılıyor — 2026-05-10

#### Bağlam

Transaction Service, bankacılık sistemiyle ISO 8583 protokolü üzerinden iletişim kuracak. Sistem .NET 10 tabanlıdır, ancak ISO 8583 ekosistemi ağırlıklı olarak Java'da gelişmiştir.

#### Değerlendirilen Alternatifler

| Seçenek | Artılar | Eksiler |
|---|---|---|
| **OpenIso8583.Net** | Açık kaynak, .NET native | Aktif bakım durumu belirsiz |
| **Custom Parser** | Tam kontrol, banka spesifikasyonuna uyum | Geliştirme süresi yüksek |
| **Ticari kütüphane** | Destek ve dokümantasyon | Maliyet, vendor lock-in |
| **Java jPOS + gRPC** | Olgun ve kanıtlanmış | Java servisi ek karmaşıklık; polyglot mimari |

#### Karar

⏳ **Karar Ertelendi** — Banka'nın spesifik ISO 8583 varyantı (ASCII/EBCDIC/binary encoding, bitmap tipi, field uzunluk formatı) netleştirilmeden kütüphane seçimi yapılamaz.

#### Sonraki Adımlar

1. Banka entegrasyon ekibinden teknik spesifikasyon belgesi alınacak.
2. `OpenIso8583.Net` ile bir Proof-of-Concept (PoC) geliştirilecek.
3. Custom parser gerekliyse banka spesifikasyonuna göre yazılacak.

---

## ADR-005

### QR Token Deposu: Redis vs MSSQL

**Durum:** ✅ Kabul Edildi — 2026-05-12

#### Bağlam

QR token'larının 90 saniyelik TTL ile depolanması gerekiyor. Bu kısa ömürlü veri için en uygun depolama seçeneği belirlenmeli.

#### Değerlendirilen Alternatifler

| Özellik | Redis | MSSQL |
|---|---|---|
| **TTL (Otomatik silme)** | ✅ Yerleşik, atomik | ❌ Background job gerektirir |
| **Okuma hızı** | <1ms (in-memory) | ~5-20ms (disk I/O) |
| **Atomic operasyonlar** | ✅ `SET NX`, `HSET`, `EXPIRE` | ❌ Transaction + lock gerekir |
| **Kalıcılık** | Opsiyonel (AOF/RDB) | ✅ Her zaman kalıcı |
| **Race condition yönetimi** | ✅ Atomic `SET NX` ile kolay | Karmaşık locking gerektirir |

#### Karar

**Redis** seçildi.

#### Gerekçe

- 90 saniyelik TTL Redis'in yerleşik `EXPIRE` komutuyla tek satırda yönetilir. MSSQL'de background job + index gerektirir.
- QR taramasındaki race condition (`SET NX` atomik operasyonu) Redis'te doğal olarak çözülür.
- QR token'ların kalıcı olmaya ihtiyacı yoktur — Redis yeniden başlarsa süresi dolmuş sayılır.
- Okuma hızı kritiktir: Her QR taramasında token anında sorgulanmalı.

#### Sonuçlar

- Redis AOF persistence aktif edilir (ani restart sonrası aktif token'lar korunur).
- Eviction policy `volatile-lru` olarak ayarlanır (TTL'li key'ler önce silinir).
- QR token HASH veri yapısı (`HSET`) olarak saklanır.

---

## ADR-006

### Container Orchestration: Kubernetes

**Durum:** ✅ Kabul Edildi — 2026-05-15

#### Bağlam

6 mikroservis, production ortamında çalıştırılacak. Otomatik ölçeklendirme, self-healing ve sıfır-downtime deployment gereksinimleri var.

#### Değerlendirilen Alternatifler

| Seçenek | Artılar | Eksiler |
|---|---|---|
| **Docker Compose** | Basit, öğrenmesi kolay | Production için yetersiz (scaling yok, self-healing yok) |
| **Kubernetes (K8s)** | Güçlü orkestrasyon, HPA, rolling update | Yüksek öğrenme eğrisi, karmaşık konfigürasyon |
| **AWS ECS** | AWS ile entegre, yönetilen | AWS lock-in |
| **Nomad** | Hafif, K8s'ten basit | Daha az yaygın ekosistem |

#### Karar

**Kubernetes** seçildi.

#### Gerekçe

- HPA ile CPU/memory bazlı otomatik ölçeklendirme gereksinimi K8s ile doğal karşılanır.
- Rolling deployment ile sıfır-downtime güncelleme kritik gereksinimdir.
- Managed K8s (AKS veya EKS) ile operasyonel yük azaltılabilir.
- Geniş ekosistem: Helm, ArgoCD, Prometheus, Grafana entegrasyonları.

#### Sonuçlar

- Her servis için `Deployment` + `Service` + `HPA` manifest dosyaları yazılacak.
- Secret yönetimi için External Secrets Operator + HashiCorp Vault entegrasyonu.
- Monitoring için Prometheus + Grafana stack kurulacak.

---

## ADR-007

### Raporlama için Elasticsearch

**Durum:** ✅ Kabul Edildi — 2026-05-18

#### Bağlam

Müşteri ekstresi, merchant raporları ve operasyonel dashboardlar için hızlı full-text arama ve aggregation kapasitesi gerekiyor.

#### Değerlendirilen Alternatifler

| Seçenek | Artılar | Eksiler |
|---|---|---|
| **Elasticsearch** | Full-text arama, aggregation, ILM | Operasyonel karmaşıklık, maliyet |
| **MSSQL (raporlama DB)** | Mevcut altyapı, basit | Büyük veri setlerinde yavaş aggregation |
| **ClickHouse** | OLAP için yüksek performans | Ekosistem daha sınırlı, öğrenme eğrisi |
| **Apache Druid** | Gerçek zamanlı analitik | Çok yüksek operasyonel karmaşıklık |

#### Karar

**Elasticsearch** seçildi.

#### Gerekçe

- "Ahmet Market" gibi merchant ismiyle arama, tarih aralığı filtreleme, saatlik işlem dağılımı aggregation gibi sorgular Elasticsearch'in güçlü olduğu alanlardır.
- MSSQL daily_summaries tablosu fallback olarak tutulur; Elasticsearch down olduğunda temel raporlar sunulabilir.
- ILM (Index Lifecycle Management) ile 10 yıllık MASAK zorunluluğu otomatik yönetilir.
- Kibana ile operasyonel dashboard kurulumu hızlıdır.

#### Sonuçlar

- `transactions-YYYY.MM.DD` daily rollover indeks stratejisi uygulanır.
- Hot → Warm → Cold → Frozen yaşam döngüsü ILM policy ile yönetilir.
- Reporting Service, Kafka event'lerini Elasticsearch'e yazar (eventual consistency).

---

## ADR Şablonu (Yeni Kararlar İçin)

```markdown
## ADR-XXX

### [Karar Başlığı]

**Durum:** 🔄 Öneriliyor / ✅ Kabul Edildi / ❌ Reddedildi / ♻️ Değiştirildi

#### Bağlam

[Kararı zorunlu kılan durum nedir? Hangi problemi çözüyor?]

#### Değerlendirilen Alternatifler

| Seçenek | Artılar | Eksiler |
|---|---|---|
| Seçenek A | ... | ... |
| Seçenek B | ... | ... |

#### Karar

[Seçilen çözüm]

#### Gerekçe

[Neden bu seçenek? Trade-off'lar neler?]

#### Sonuçlar

[Bu kararın sisteme etkisi nedir? Hangi başka kararları tetikler?]
```

---

## Research & Open Questions (Yeni Başlayanlar İçin Araştırma Rehberi)

> Bu bölüm, mimari tasarım ve karar alma süreçlerine yeni başlayan backend geliştiriciler için hazırlanmıştır.

---

- **📚 ADR (Architecture Decision Record) neden tutulur?**
  "Kafka neden kullandık?" sorusunu 6 ay sonra yeni katılan bir geliştirici sorduğunda cevabı nerede bulacak?
  - ADR, bir kararın "ne" değil "neden" alındığını belgeler. Bağlamı kaydetmek neden kritiktir?
  - [adr.github.io](https://adr.github.io/) sitesini ziyaret et ve farklı ADR formatlarını incele.
  - **Anahtar soru:** "Biz her zaman böyle yaptık" ile "ADR'de şöyle yazıyor" arasındaki fark nedir?

---

- **📚 "Trade-off" kavramı neden mühendisliğin merkezindedir?**
  Her ADR'de "Artılar / Eksiler" tablosu var. Mühendislikte "mükemmel çözüm" neden yoktur?
  - CAP Teoremi'ni araştır: Consistency, Availability, Partition Tolerance — üçünü birden elde edebilir misin?
  - Redis'i seçince ne kazandık, ne kaybettik? Kafka'yı seçince?
  - **Anahtar soru:** ADR-002'de Kafka seçildi ama "operasyonel karmaşıklık yüksek" yazıyor. Bu karmaşıklığı kim yönetecek? Bu sorumluluğu baştan görmek neden önemlidir?

---

- **📚 Monolitik vs Mikroservis — "her zaman mikroservis" doğru mu?**
  ADR-001'de mikroservis seçildi. Ama her proje için bu doğru tercih midir?
  - Martin Fowler'ın "MonolithFirst" makalesini araştır: Neden büyük projeler bile monolitik başlamalı?
  - "Distributed monolith" nedir? Mikroservis yaparken düşülen en yaygın tuzak nedir?
  - **Anahtar soru:** Bu sistemde mikroservis mimarisi doğru karar mıydı? Hangi ölçütte "evet" veya "hayır" diyebilirsin?

---

- **📚 "Proof of Concept" (PoC) nedir ve neden ADR-004'te gerekli?**
  ISO 8583 kütüphanesi seçimi için önce PoC yapılacak. Bu ne demek?
  - PoC, bir teknolojinin veya yaklaşımın belirli bir problemde çalışıp çalışmayacağını küçük ölçekte test etmektir.
  - PoC ile production kodu arasındaki fark nedir? PoC kodu production'a gidebilir mi?
  - **Anahtar soru:** PoC başarısız olursa ne yapılır? ADR durumu "❌ Reddedildi" olarak mı güncellenir?
