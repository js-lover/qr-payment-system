# Research & Open Questions

> Yeni başlayan backend geliştiriciler için araştırma rehberi.
> Her madde; **ne öğreneceğini**, **neden önemli olduğunu** ve **nereden başlayacağını** gösterir.

---

## İçindekiler

- [Genel Mimari](#genel-mimari)
- [Auth Service](#auth-service)
- [Onboarding Service](#onboarding-service)
- [Wallet Service](#wallet-service)
- [QR Code Service](#qr-code-service)
- [Transaction Service](#transaction-service)
- [Reporting Service](#reporting-service)
- [Infrastructure](#infrastructure)
- [Security](#security)
- [Data Models](#data-models)
- [Deployment](#deployment)

---

## Genel Mimari

> İlgili dosyalar: [`00-overview`](00-overview/README.md), [`11-adr`](11-adr/README.md)

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

---

## Auth Service

> İlgili dosya: [`01-auth-service`](01-auth-service/README.md)

---

- **📚 JWT nedir ve nasıl çalışır?**
  Bu sistemde her API isteği bir JWT (JSON Web Token) taşıyor. Peki bu token nasıl üretiliyor, nasıl doğrulanıyor ve neden süresinin dolması gerekiyor?
  - JWT'nin 3 bölümünü (Header, Payload, Signature) bir JWT debugger ile incele: [jwt.io](https://jwt.io)
  - `HS256` (simetrik) ile `RS256` (asimetrik) arasındaki farkı araştır. Neden mikroservislerde RS256 tercih edilir?
  - **Anahtar soru:** Bir servis ele geçirilirse, HS256 ile RS256'nın güvenlik sonuçları neden farklıdır?

---

- **📚 Access Token neden 15 dakika, Refresh Token neden 7 gün?**
  Token süreleri rastgele seçilmedi — bu bir güvenlik dengesidir.
  - Kısa ömürlü Access Token çalınırsa zarar sınırlıdır. Refresh Token çalınırsa ne olur?
  - "Refresh Token Rotation" kavramını araştır: Her refresh isteğinde yeni bir refresh token verilirse, çalınan eski token otomatik geçersiz olur. Bu nasıl çalışır?
  - **Anahtar soru:** Kullanıcı logout olmadan token'ı nasıl geçersiz kılarsın? (Cevap: Token Blacklist)

---

- **📚 Şifreler veritabanında neden düz metin saklanamaz?**
  `password_hash` sütununu gördün — içinde şifre değil, hash var. Neden?
  - **BCrypt** nedir? MD5/SHA256'dan neden daha güvenlidir? "Cost factor" ne anlama gelir?
  - "Rainbow Table" saldırısını araştır: Neden salt eklenmesi bu saldırıyı işe yaramaz hale getirir?
  - **Dene:** BCrypt'i .NET'te [BCrypt.Net-Next](https://github.com/BcryptNet/bcrypt.net) kütüphanesiyle 3 satırda uygula.

---

- **📚 TOTP (Google Authenticator) nasıl çalışır?**
  Sistemde 2FA için TOTP kullanılıyor. Authenticator uygulaması her 30 saniyede yeni kod üretiyor — ama sunucuya bağlı değil. Bu nasıl mümkün?
  - RFC 6238 standardını araştır: TOTP = HMAC(secret_key, current_timestamp / 30)
  - Sunucu ve client aynı secret'ı bildiği için bağımsız olarak aynı kodu üretebilir.
  - **Anahtar soru:** Telefon saati 1 dakika geri olursa ne olur? (Cevap: Time window tolerance)

---

- **📚 mTLS nedir ve normal TLS'ten farkı nedir?**
  Normal TLS'te yalnızca sunucu kendini kanıtlar (HTTPS). mTLS'te istemci de sertifikayla kendini kanıtlar.
  - POS terminallerin mTLS kullandığını gördün. Neden sadece şifre yeterli değil?
  - "Man-in-the-Middle" saldırısını araştır: mTLS bunu nasıl engeller?
  - **Anahtar soru:** HMAC-SHA256 imzası ek olarak neden gerekli? (mTLS bağlantıyı doğrular, ama mesaj içeriğini değil)

---

## Onboarding Service

> İlgili dosya: [`02-onboarding-service`](02-onboarding-service/README.md)

---

- **📚 Event-Driven Architecture nedir ve neden Kafka kullandık?**
  Onboarding servisi onayladıktan sonra Auth ve Wallet servislerine **doğrudan HTTP çağrısı yapmak** yerine Kafka'ya event publish ediyor. Neden?
  - "Tight coupling" ile "Loose coupling" arasındaki farkı araştır. HTTP çağrısı sırasında Auth Service çökmüş olsaydı ne olurdu?
  - Event-Driven Architecture'da publisher consumer'ı tanımak zorunda değildir — bu ne anlama gelir?
  - **Anahtar soru:** Kafka ile HTTP arasındaki temel trade-off nedir? (Hint: Eventual consistency vs. immediate consistency)

---

- **📚 Outbox Pattern: Kafka'ya mesaj kaybetmeden nasıl gönderilir?**
  Servis veritabanına yazdıktan sonra Kafka'ya publish etmeye çalışırken çökerse ne olur? İşte Outbox Pattern tam bu problemi çözer.
  - "Two-phase commit" problemini araştır: Neden bir DB transaction'ı ile Kafka publish'i atomik yapamayız?
  - Outbox Pattern'ın çalışma mantığı: Kafka'ya yazmak yerine DB'ye yaz, bir Worker okuyup Kafka'ya ilet.
  - **Dene:** [MassTransit Outbox](https://masstransit.io/documentation/patterns/transactional-outbox) veya [Wolverine](https://wolverine.netlify.app/) dokümantasyonunu oku.

---

- **📚 KYC nedir? Gerçek hayatta nasıl çalışır?**
  KYC (Know Your Customer), finansal sistemlerin kimlik doğrulama zorunluluğudur. Bir banka veya ödeme sistemi neden kimliğini bilmek zorundadır?
  - "AML (Anti-Money Laundering)" ve "MASAK" kavramlarını araştır. Türkiye'de ödeme kuruluşları hangi regülasyona tabidir?
  - Face matching nasıl çalışır? "Liveness detection" neden önemlidir? (Fotoğrafla geçmeyi engeller)
  - **Anahtar soru:** Bu sistemde ham TCKN neden saklanmıyor, yalnızca hash'i tutuluyor? Birisi DB'ye erişse bile ne işe yarar?

---

- **📚 State Machine (Durum Makinesi) nedir ve neden kullanıyoruz?**
  Müşteri kaydının `REGISTERED → PHONE_PENDING → KYC_PENDING → APPROVED` gibi adımları var. Bu bir state machine.
  - State machine neden `if/else` yığınından daha iyi bir yaklaşımdır?
  - .NET'te state machine implement etmek için [Stateless](https://github.com/dotnet-state-machine/stateless) kütüphanesine bak.
  - **Anahtar soru:** Bir müşteri `KYC_REJECTED` durumundayken tekrar `APPROVED`'a geçebilir mi? Kuralı nerede tanımlarız?

---

- **📚 KVKK ve Kişisel Veri Güvenliği**
  Sistemde müşteri adı, telefon numarası, TCKN ve kimlik fotoğrafı işleniyor. Bunlar kişisel veri — yasal yükümlülükler var.
  - KVKK'nın "veri minimizasyonu" ilkesini araştır: Sadece ihtiyacın olan veriyi topla.
  - "Right to Erasure" (Silme hakkı) ne demek? Müşteri hesabını silmek istediğinde log'lardaki veriler ne olacak?
  - **Anahtar soru:** Log'larda `+90 5XX XXX XX 12` yerine `+90 5** *** ** 12` yazılması (maskeleme) neden yeterli değil, neden daha fazlası gerekir?

---

## Wallet Service

> İlgili dosya: [`03-wallet-service`](03-wallet-service/README.md)

---

- **📚 Double-Entry Bookkeeping (Çift Taraflı Muhasebe) nedir?**
  Finansal sistemlerin temel kuralı: Para yoktan var olamaz, yok da olamaz. Her para hareketi iki tarafı olan bir kayıttır.
  - 100 TL yüklendiğinde neden iki satır yazıyoruz? Sadece bakiyeyi `+100` yapmak yetmez mi?
  - "T-account" (T-hesabı) kavramını araştır. Debit ve Credit'in muhasebedeki anlamı, sezgisel anlamından farklıdır!
  - **Anahtar soru:** Eğer sadece `balance + 100` yapsan ve bir bug 50 TL'yi iki kez eklese, bunu nasıl fark ederdin? Double-entry bunu nasıl önler?

---

- **📚 ACID nedir? Neden finansal sistemlerde kritiktir?**
  Wallet Service'te her para hareketi `BEGIN TRANSACTION ... COMMIT` bloğu içinde. Bu neden gerekli?
  - **A**tomicity: Ya hepsi olur ya hiçbiri. Debit yazılıp Credit yazılmazsa ne olur?
  - **C**onsistency: `balance >= 0` constraint'ini araştır. DB katmanında kural koymak neden uygulama katmanından daha güvenlidir?
  - **I**solation: İki kullanıcı aynı anda aynı cüzdandan ödeme yaparsa ne olur? (Race condition)
  - **D**urability: Sunucu çöktükten sonra yazılan kayıt kaybolur mu?
  - **Dene:** Aynı anda iki thread ile aynı bakiyeyi düşürmeyi dene. `UPDLOCK` olmadan ne olur?

---

- **📚 Provision (Bloke) sistemi neden var? Neden direkt para kesmiyoruz?**
  Müşteri "Öde" dediğinde para hemen kesilmiyor — önce "rezerv" (bloke) alınıyor. Neden bu ek adım gerekli?
  - Şu senaryoyu düşün: Ödeme başlatıldı, banka yanıt verirken (30 saniye) müşteri ikinci bir ödeme daha başlattı. Her ikisi de "bakiye yeterli" görse ne olur?
  - `available_balance = balance - active_provisions` farkını kavra.
  - **Anahtar soru:** Provision alındıktan sonra banka "HAYIR" derse (yanıt kodu 51) provision nasıl geri alınır? Bu işlem başarısız olursa ne olur?

---

- **📚 Ledger (Muhasebe Defteri) neden değiştirilemez (Immutable) olmalı?**
  `ledger_entries` tablosunda `UPDATE` ve `DELETE` yetkisi uygulama kullanıcısından kaldırıldı. Neden?
  - Gerçek bir banka defteriyle kıyas yap: Muhasebeci yanlış kayıt yaparsa satırı silmez, tersine bir kayıt (reversal) ekler.
  - "Audit trail" nedir? Finansal denetimde her işlemin izlenebilir olması neden zorunludur?
  - **Anahtar soru:** `BIGINT IDENTITY` olan `id` sütunu neden önemli? UUID yerine neden sıralı bir ID kullandık?

---

- **📚 Database Deadlock nedir ve nasıl oluşur?**
  Aynı anda binlerce ödeme geldiğinde MSSQL'de "deadlock" oluşabilir. Bu ne demek?
  - Şu senaryoyu araştır: Thread A, Row 1'i kilitleyip Row 2'yi bekliyor. Thread B, Row 2'yi kilitleyip Row 1'i bekliyor. İkisi de sonsuza kadar bekler.
  - `ISOLATION LEVEL` seçeneklerini araştır: `READ COMMITTED`, `SNAPSHOT`, `SERIALIZABLE` farkları neler?
  - **Anahtar soru:** `WITH (UPDLOCK, ROWLOCK)` hint'i neden deadlock'u önler? (Hint: Tüm thread'ler aynı sırayla kilitleme yapar)

---

## QR Code Service

> İlgili dosya: [`04-qr-code-service`](04-qr-code-service/README.md)

---

- **📚 Redis nedir ve neden veritabanı yerine Redis kullandık?**
  QR token'ları MSSQL'de değil Redis'te tutuluyor. Neden? Her ikisi de veri saklar, farkı ne?
  - Redis bir "in-memory" veri deposudur — disk yerine RAM'de çalışır. Bu ne kadar hız farkı yaratır?
  - **TTL (Time To Live)** kavramını araştır: Redis'te bir key'e `EX 90` yazarsan, 90 saniye sonra Redis onu **otomatik siler**. Bunu MSSQL'de yapmak için ne gerekir?
  - **Anahtar soru:** Redis sunucusu yeniden başlatılırsa veriler ne olur? QR token'lar kaybolursa sistem ne yapmalı?

---

- **📚 QR kod içinde neden para miktarı yazmıyor?**
  "Static QR" ile "Dynamic QR" arasındaki farkı ve neden bu sistemde dinamik QR tercih edildiğini anlamak kritik bir güvenlik konusudur.
  - Static QR'da ne olur? Müşteri QR'ı fotoğraflayıp kaydederse, aynı QR ile tekrar ödeme yapabilir mi?
  - Bu sistemde QR yalnızca bir UUID taşıyor. Birisi QR'ı kopyalasa ne işine yarar? (Cevap: JWT olmadan hiçbir şey yapamaz)
  - **Anahtar soru:** Neden QR içindeki UUID yerine doğrudan `amount=50&merchant=Ahmet` yazmıyoruz? Bu bilgiyi QR'dan okusak ne riski doğar?

---

- **📚 Race Condition nedir? Aynı QR'ı iki kişi aynı anda taradığında ne olur?**
  İki müşteri aynı saniyede aynı QR'ı tarayabilir mi? Olabilir! Bu "race condition" örneğidir.
  - Redis `SET NX` (SET if Not eXists) komutunu araştır. Atomic işlemin ne anlama geldiğini anla.
  - `SET key value NX` sadece key yoksa yazar — bu operasyonu iki client aynı anda yaparsa yalnızca biri kazanır.
  - **Dene:** Redis CLI'da `SET qr:test VALIDATING NX EX 90` komutunu iki kez çalıştır. İkinci komut ne döndürür?

---

- **📚 UUID nedir? Neden random ID kullanıyoruz?**
  Token olarak `8f3b9a2c-d91e-4a2b-b3c1-7f9e2d4a8c3b` gibi bir UUID üretiyoruz. Neden `1, 2, 3` gibi sıralı ID kullanmıyoruz?
  - UUID v4 (122-bit random) ile tahmin edilebilir sıralı ID arasındaki güvenlik farkını araştır.
  - Eğer token `1, 2, 3` olsaydı, bir saldırgan henüz ödenmemiş başka işlemleri tahmin edip deneyebilir miydi?
  - **Anahtar soru:** `NEWID()` (MSSQL) veya `Guid.NewGuid()` (.NET) cryptographically random mi? Bu neden önemli?

---

- **📚 WebSocket nedir? Kasa neden polling yapmıyor?**
  Ödeme tamamlandığında kasa ekranı anında "✅ Ödeme Başarılı!" gösteriyor. Kasa bu bilgiyi nasıl alıyor?
  - "Polling" ile "WebSocket (Push)" arasındaki farkı araştır: Her 2 saniyede `GET /status` çağırmak yerine sunucunun mesaj göndermesi.
  - ASP.NET SignalR'ın WebSocket üzerindeki soyutlamasını incele.
  - **Anahtar soru:** WebSocket bağlantısı koptuğunda (müşteri Wi-Fi değiştirdi) kasa ödemeyi başarılı mı yoksa başarısız mı saymalı? Fallback mekanizması ne olmalı?

---

## Transaction Service

> İlgili dosyalar: [`05-transaction-service`](05-transaction-service/README.md), [`00-overview`](00-overview/README.md), [`SYSTEM-OVERVİEW`](../SYSTEM-OVERVİEW.md)

---

- **📚 ISO 8583 nedir? Neden JSON veya REST kullanmıyoruz?**
  Bankalarla konuşurken REST API veya JSON değil, 1987'den beri kullanılan ISO 8583 protokolü kullanılıyor. Neden?
  - ISO 8583'ün "bitmap" tabanlı yapısını araştır: Her bit, bir field'ın varlığını gösterir. Bu neden JSON'dan daha verimlidir?
  - `0200` (istek), `0210` (yanıt), `0420` (iptal) MTI (Message Type Indicator) kodlarını ezberle. Bunlar bankacılıkta "HTTP metotları" gibidir.
  - **Anahtar soru:** ISO 8583 neden TCP üzerinden çalışır, HTTP üzerinden değil? (Hint: Düşük latency ve persistent connection)

- **ISO 8583 .NET Library (ADR-004):** `OpenIso8583.Net` ile PoC geliştirilecek; banka spesifik encoding (ASCII/EBCDIC/binary) netleştirilmeden karar verilemez. Bkz. [`11-adr/#adr-004`](11-adr/README.md#adr-004).

- **ISO 8583 .NET 10 Entegrasyonu:** jPOS kütüphanesi ağırlıklı Java ekosistemine aittir. Backend'in tamamen .NET 10'a çevrilmesi kararı doğrultusunda, ISO 8583 mesaj ayrıştırması için açık kaynaklı bir .NET kütüphanesi (örn: `Truso.B1` veya custom parser) mi yazılacak yoksa ticari bir paket mi kullanılacak netleştirilmelidir.

---

- **📚 Idempotency nedir? Neden "aynı isteği iki kez gönder" problemi var?**
  Ağ sorunları nedeniyle aynı ödeme isteği bankaya iki kez ulaşabilir. Kullanıcının hesabından iki kez para kesilirse?
  - "Idempotent operation" kavramını araştır: Aynı işlemi 1 kez veya 5 kez yapsan da sonuç aynı olursa idempotent'tir.
  - Bu sistemde idempotency nasıl sağlandı? `qr_token UNIQUE` constraint'ine bak.
  - **Anahtar soru:** STAN (System Trace Audit Number) neden her gün sıfırlanıyor? 1 milyondan fazla işlem yapılan bir günde ne olur?

- **STAN Kapasitesi:** Günlük 999.999 işlem limiti. Yüksek hacimli ortamlarda STAN stratejisi (terminal bazlı veya sistem geneli) yeniden değerlendirilmeli.

---

- **📚 Reversal (0420) nedir? Para neden "geri alınır"?**
  Banka `91` kodu döndürdüğünde veya 30 saniye timeout olduğunda `0420 Reversal` mesajı gönderiliyor. Bu ne anlama gelir?
  - Şu senaryoyu düşün: Banka isteği aldı ve işledi, ama cevabı ağda kayboldu. Müşterinin blokesi açık kaldı. Ne yapılmalı?
  - Reversal bir "geri alma" talebidir: "O işlemi iptal et" denir — ama banka zaten işlememişse? Reversal zararsız mı?
  - **Anahtar soru:** Reversal da başarısız olursa? (5 deneme sonrası `MANUAL_INTERVENTION`) Operatör ne yapmalı?

- **Idempotency & Retry Mechanisms:** İşlem Timeout olduğunda gönderilecek `0420 Reversal` mesajlarının hedefe ulaşmaması durumunda, .NET Worker Service'ler üzerinde back-off retry politikalarının (Polly kütüphanesi vb.) nasıl yapılandırılacağı detaylandırılmalıdır.

---

- **📚 Circuit Breaker (Devre Kesici) nedir?**
  Polly kütüphanesinde `CircuitBreakerAsync` görüldü. Bu bir yazılım tasarım desenidir — elektrik devre kesicisinden ilham alır.
  - Normal durumda devre "kapalı" (closed) → istekler geçer. Sürekli hata olursa devre "açık" (open) → 60 saniye istekler reddedilir.
  - Neden sürekli başarısız olan bir servisi çağırmaya devam etmek zararlıdır? (Hint: Thread pool tükenir, tüm sistem yavaşlar)
  - **Anahtar soru:** Devre açıkken gelen isteklere ne dönmeliyiz? `503 Service Unavailable` mı, yoksa cached bir yanıt mı?

---

- **📚 Exponential Backoff nedir? Neden 1, 2, 4 saniye bekliyoruz?**
  Retry policy'de her denemede daha uzun bekleniyor: 2s, 4s, 8s... Bu "exponential backoff" stratejisidir.
  - Neden sabit aralıkla (her saniye) retry yapmak yerine artan beklemeler kullanılır? (Hint: Thundering herd problemi)
  - 1000 kullanıcı aynı anda hata alıp her biri 1 saniyede retry yaparsa sunucu ne olur? Backoff bunu nasıl dağıtır?
  - **Dene:** Polly kütüphanesini .NET projesine ekle ve `WaitAndRetryAsync` ile basit bir HTTP retry policy yaz.

---

- **Switch Servisi Ayrışması:** Banka hacimleri artarsa Transaction Service'ten bağımsız ölçeklendirilebilecek şekilde Switch Entegrasyon Servisi ayrılabilir. Eşik kriterleri: >500 TPS veya birden fazla banka/şema entegrasyonu.

---

## Reporting Service

> İlgili dosya: [`06-reporting-service`](06-reporting-service/README.md)

---

- **📚 Elasticsearch nedir ve neden MSSQL yetmez?**
  "Ahmet Market" adındaki tüm ödemeleri bul, veya bu haftaki en çok işlem yapan işyerini listele — bu tür sorgular MSSQL'de yavaş çalışır. Neden?
  - MSSQL bir "row store" (satır bazlı) veritabanıdır. Elasticsearch ise tam metin araması ve aggregation için optimize edilmiş bir "inverted index" kullanır.
  - `keyword` ve `text` field tipleri arasındaki farkı araştır: Neden `merchant_name`'i hem `text` hem `keyword` olarak indexledik?
  - **Anahtar soru:** `transactions-2026.05.25` gibi günlük index ne anlama gelir? Neden tek bir büyük index yerine birden fazla index kullanıyoruz?

---

- **📚 Kafka Consumer Group nasıl çalışır? Neden "reporting-group" ayrı?**
  Kafka'da aynı topic'i hem Wallet Service hem de Reporting Service okuyor. Her ikisi de tüm mesajları alıyor mu?
  - "Consumer group" kavramını araştır: Aynı group içindeki consumer'lar mesajları paylaşır (her mesaj bir kez okunur). Farklı group'lar bağımsız okur.
  - `reporting-group` bağımsız olduğu için Reporting Service'in yavaş kalması Wallet Service'i etkilemez.
  - **Anahtar soru:** Reporting Service 1 saat çökerse, yeniden başladığında kaçırdığı mesajları okuyabilir mi? (Cevap: Kafka offset ile evet!)

---

- **📚 Mutabakat (Reconciliation) nedir? Bankacılıkta neden kritiktir?**
  Her gece 02:00'de sistem iki farklı kaynaktaki tutarları karşılaştırıyor. Bu neden gerekli?
  - Şu senaryoyu düşün: Banka işlemi başarılı saydı ama sistemimiz kaydetmedi. Müşteri para ödedi ama işyerine yansımadı. Nasıl fark ederiz?
  - Reconciliation, iki sistemin "aynı gerçeği" görmesini doğrular.
  - **Anahtar soru:** Mutabakatta 1 TL'lik fark bulundu. Bu önemli mi? Büyük sistemlerde "tolerated difference" politikası neden olur?

---

- **📚 Eventual Consistency nedir? "Anlık" ile "neredeyse anlık" arasındaki fark?**
  Ödeme tamamlandı, ama Reporting'de görünmesi birkaç saniye alabilir — çünkü Kafka'dan event okunup Elasticsearch'e yazılması gerekiyor.
  - "Strong consistency" ile "Eventual consistency" arasındaki trade-off'u araştır.
  - Müşteri "Ödedim ama ekstremi göremiyorum" derse ne yapmalıyız? (Hint: "Pending" durumu veya birkaç saniye beklet)
  - **Anahtar soru:** Kafkadan gelen bir event'i Elasticsearch'e yazmadan önce servis çökerse ne olur? `enable.auto.commit=false` ne işe yarar?

---

- **📚 PDF oluşturma (.NET'te nasıl yapılır?)**
  Dijital makbuz PDF formatında üretiliyor. `.NET 10`'da bunu nasıl yaparsın?
  - `QuestPDF` kütüphanesini araştır: Fluent API ile C# kodu yazarak PDF üretmek nasıl çalışır?
  - Alternatif: HTML → PDF dönüşümü için `Puppeteer Sharp` veya `wkhtmltopdf` — hangi durumda hangisi tercih edilmeli?
  - **Dene:** QuestPDF ile "Merheba Dünya" yazan bir PDF belgesi oluştur: [questpdf.com](https://www.questpdf.com/getting-started.html)

---

## Infrastructure

> İlgili dosya: [`07-infrastructure`](07-infrastructure/README.md)

---

- **📚 API Gateway neden gerekli? Her servise doğrudan bağlansak olmaz mı?**
  Mobil uygulama doğrudan Auth Service, Wallet Service, QR Service'e bağlansa ne olur?
  - "Single Entry Point" neden güvenlik açısından önemlidir? Her servisi internete açmak ne riski doğurur?
  - JWT doğrulamasını her servis ayrı ayrı yapsa ne olur? Gateway bunu merkezi yapınca ne kazanılır?
  - **Anahtar soru:** Bir servisin adresi değişirse (ölçeklendirme) client'ın haberi olması gerekir mi? Gateway bu sorunu nasıl çözer?

---

- **📚 Kafka'da "partition" neden önemlidir?**
  `payment.success` topic'inde 6 partition var. Bu sayıyı neden 1 yapmadık?
  - Partition, paralel işlemenin temelidir: 6 partition → 6 consumer aynı anda çalışabilir.
  - "Message ordering" garantisi sadece aynı partition içinde geçerlidir. Bu neden önemlidir?
  - **Anahtar soru:** Aynı müşterinin iki işleminin sıralı işlenmesi gerekiyorsa (ör. önce provision, sonra commit) hangi partition stratejisi kullanılmalı?

---

- **📚 Redis `volatile-lru` eviction policy nedir?**
  Redis belleği dolduğunda hangi key'leri sileceğine karar vermek zorunda. `volatile-lru` ne anlama gelir?
  - `allkeys-lru` vs `volatile-lru` farkı: Biri TTL'li/TTL'siz tüm key'leri, diğeri yalnızca TTL'li key'leri temizler.
  - QR token'lar TTL'li, STAN counter TTL'siz. Bellek dolunca hangisi silinmeli?
  - **Dene:** Redis CLI'da `redis-cli info memory` komutunu çalıştır ve `used_memory_human` değerini incele.

---

- **📚 MSSQL Always On Availability Group nedir?**
  MSSQL Primary çöktüğünde sistem otomatik olarak Secondary'e geçiyor. Bu nasıl çalışır?
  - Primary-Secondary replikasyonunu araştır: Yazma işlemleri Primary'de, okuma Secondary'de yapılabilir.
  - "Failover" nedir? Otomatik failover ile manuel failover arasındaki fark nedir?
  - **Anahtar soru:** Failover sırasında 30 saniye boyunca bağlantı kesilirse uygulama ne yapmalı? Connection string'e `MultiSubnetFailover=True` eklemek neden yeterlidir?

---

## Security

> İlgili dosya: [`08-security`](08-security/README.md)

---

- **📚 HTTPS (TLS) nasıl çalışır? "Şifreli bağlantı" ne anlama gelir?**
  Sistemdeki tüm iletişim TLS ile şifreleniyor. Tarayıcının adres çubuğundaki kilit simgesi de bunu gösterir. Ama perde arkasında ne oluyor?
  - "TLS Handshake" sürecini araştır: İstemci ve sunucu nasıl ortak bir şifreleme anahtarı üretir (anahtar değişimi)?
  - Simetrik vs asimetrik şifreleme farkını öğren. TLS her ikisini neden birlikte kullanır?
  - **Anahtar soru:** Birisi ağ trafiğini "dinlese" (wireshark ile capture etse) şifreli veriyi okuyabilir mi? Neden değil?

---

- **📚 RBAC (Role-Based Access Control) neden önemlidir?**
  "Customer" rolündeki bir kullanıcı neden başka müşterinin bakiyesine bakamaz? Tek bir `isAdmin` flag'i yetmez mi?
  - RBAC modelini araştır: Rol → İzin → Kaynak hiyerarşisi.
  - "Principle of Least Privilege" (En Az Yetki İlkesi) ne demektir?
  - **Anahtar soru:** JWT'de `wallet_id` claim'i varken sunucu neden tekrar veritabanından kontrol yapmalı? JWT'ye güvenmek yeterli değil mi?

---

- **📚 SQL Injection nedir ve neden hâlâ önemlidir?**
  OWASP listesinde A03 olarak yer alıyor — yıllar geçmesine rağmen hâlâ yaygın bir açık.
  - Klasik SQL Injection örneği: `SELECT * FROM users WHERE username = ''; DROP TABLE users; --`
  - "Parametreli sorgu" (parameterized query) bu saldırıyı nasıl engeller?
  - **Dene:** Entity Framework Core'da raw SQL query ve parametreli query arasındaki farkı görmek için basit bir örnek yaz.

---

- **📚 Replay Attack nedir? HMAC tek başına yeterli değil mi?**
  Bir saldırgan HMAC imzalı bir isteği kopyalayıp 10 dakika sonra tekrar gönderirse ne olur?
  - Timestamp + Nonce kombinasyonu bu saldırıyı nasıl engeller?
  - Redis'teki `SET nonce:{uuid} 1 EX 300 NX` satırını adım adım açıkla.
  - **Anahtar soru:** Nonce'u Redis yerine veritabanında tutsan ne olur? Performans farkı neden önemlidir?

---

- **📚 Secrets (şifreler, API key'ler) kodda veya config dosyasında tutulabilir mi?**
  Sistem `<REDIS_PASSWORD>`, `<DB_PASSWORD>` gibi değerler kullanıyor. Bunları `appsettings.json`'a yazsan ne olur?
  - "Secret sprawl" problemini araştır: Şifreler kod deposuna girerse ne olur?
  - HashiCorp Vault veya Azure Key Vault nedir? Environment variable ile Secret Manager arasındaki fark nedir?
  - **Anahtar soru:** `.gitignore`'a `appsettings.Production.json` eklemek yeterli bir çözüm müdür?

---

## Data Models

> İlgili dosya: [`09-data-models`](09-data-models/README.md)

---

- **📚 UUID (GUID) vs sıralı integer ID — hangisi ne zaman kullanılır?**
  `wallet_id`, `transaction_id` gibi alanlar UUID (örn: `8f3b9a2c-d91e-4a2b-b3c1`) kullanıyor. Neden `1, 2, 3` gibi basit ID yok?
  - UUID'nin avantajı: Merkezi koordinasyon olmadan, birden fazla servis aynı anda ID üretebilir — çakışma olmaz.
  - UUID'nin dezavantajı: Sıralı olmadığı için B-tree index performansı düşer (page fragmentation).
  - **Anahtar soru:** `ledger_entries.id` sütunu neden `BIGINT IDENTITY` (sıralı)? Finansal defterde sıralı ID neden anlamlıdır?

---

- **📚 Neden her servisin ayrı veritabanı var? Tek büyük DB olmaz mı?**
  5 farklı veritabanı (auth_db, wallet_db, vb.) var. Hepsini tek bir DB'ye koysak daha kolay olmaz mıydı?
  - "Database per service" pattern'ini araştır. Servisler arası direkt JOIN yerine event kullanmak ne anlama gelir?
  - Tek DB'nin riski: Wallet Service'in yavaş sorgusu, Auth Service'i de yavaşlatır (shared resource).
  - **Anahtar soru:** Onboarding Service'teki bir şema değişikliği (yeni sütun ekleme) diğer servisleri etkiler mi? Neden etkilemez?

- **Merchant Alt Hesap Yapısı:** Her merchant için Settlement, Holding ve Commission olmak üzere 3 ayrı hesap tanımlanmıştır. Mevcut `wallet_accounts` tablosu bu yapıyı `owner_type` alanıyla desteklemekte ancak merchant bazlı alt hesap izolasyonu için ek servis tasarımı gerekebilir.

- **Double-Entry & Concurrency:** MSSQL üzerinde, aynı anda binlerce cüzdan hareketi (Top-up ve QR Ödeme) gelirken Deadlock veya Race Condition oluşmasını engellemek için `ISOLATION LEVEL` (örneğin Snapshot veya Serializable) stratejisi netleştirilmelidir.

---

- **📚 `DECIMAL(18,2)` neden para için `FLOAT`'tan daha iyi?**
  Para alanlarında `DECIMAL(18,2)` kullanılıyor. Neden `FLOAT` veya `DOUBLE` kullanılmıyor?
  - Klasik örnek: `0.1 + 0.2` işlemini bir programlama dilinde yap. Sonuç `0.30000000004` olabilir. Bu finansal sistemde ne anlama gelir?
  - DECIMAL fixed-point (sabit nokta) sayıdır — tam olarak temsil edilir.
  - **Dene:** C# REPL'de `(0.1m + 0.2m) == 0.3m` ile `(0.1 + 0.2) == 0.3` sonuçlarını karşılaştır (`m` suffix = decimal).

---

- **📚 ER diyagramı (Entity Relationship) nasıl okunur?**
  Bu belgede `||--o{` gibi semboller görüyorsun. Bunlar Crow's Foot notation (Kaz Ayağı notasyonu).
  - `||` = tam olarak bir (exactly one)
  - `o{` = sıfır veya çok (zero or many)
  - `||--o{` = "bir wallet_account'ın sıfır veya çok ledger_entry'si olabilir"
  - **Dene:** [dbdiagram.io](https://dbdiagram.io) sitesine gir ve basit bir şemayı görsel olarak çiz.

---

- **📚 Soft Delete nedir? Verileri neden gerçekten silmiyoruz?**
  Finansal veriler "silinse" bile veritabanından fiziksel olarak kaldırılmıyor — yalnızca `is_active = 0` veya `status = DELETED` yapılıyor.
  - Neden? Muhasebe denetiminde "bu işlem neden yok?" sorusuna cevap verebilmek gerekir.
  - KVKK "silme hakkı" ile soft delete nasıl bağdaşır? (Hint: PII alanları anonymize edilir, kayıt silinmez)
  - **Anahtar soru:** Silinen bir kullanıcının ledger kayıtları ne olmalı? Kullanıcı adı silinebilir ama para hareketi kaydı silinebilir mi?

---

## Deployment

> İlgili dosya: [`10-deployment`](10-deployment/README.md)

---

- **📚 Docker nedir? "Konteyner" ne anlama gelir?**
  "Bende çalışıyor ama sunucuda çalışmıyor" problemi yaşadın mı? Docker tam bu sorunu çözer.
  - "VM (Virtual Machine)" ile "Container" arasındaki farkı araştır: Container neden daha hafiftir?
  - Docker image, container, Dockerfile kavramlarını öğren. Image bir "şablon", container onun çalışan halidir.
  - **Dene:** `docker run -it --rm mcr.microsoft.com/dotnet/sdk:10.0 bash` komutuyla .NET SDK container'ı başlat ve `dotnet --version` çalıştır.

---

- **📚 Multi-stage Dockerfile neden kullanıyoruz?**
  Dockerfile'da `FROM ... AS build` ve `FROM ... AS runtime` var. Neden iki aşamalı?
  - Build stage: SDK (700 MB) ile derle. Runtime stage: Sadece runtime (200 MB) ile çalıştır.
  - Final image'a SDK girmez — güvenlik ve boyut avantajı.
  - **Dene:** Tek aşamalı ve çift aşamalı Dockerfile ile image boyutunu karşılaştır: `docker images`.

---

- **📚 Kubernetes nedir? Docker Compose yeterli değil mi?**
  Production'da binlerce istek geldiğinde 1 container yetmez — otomatik ölçeklendirme gerekir.
  - Kubernetes'in temel kavramlarını öğren: Pod, Deployment, Service, Namespace.
  - HPA (Horizontal Pod Autoscaler) CPU %70'i geçince yeni pod ekledi. Bu "scaling out" — "scaling up"dan farkı nedir?
  - **Anahtar soru:** Bir pod çökerse Kubernetes ne yapar? "Self-healing" nasıl çalışır?

---

- **📚 Readiness vs Liveness Probe — ikisi neden farklı?**
  `readinessProbe` ve `livenessProbe` iki ayrı health check — neden?
  - **Liveness:** "Bu pod hâlâ yaşıyor mu?" — Hayır ise restart et.
  - **Readiness:** "Bu pod trafik alabilir mi?" — Hayır ise traffic gönderme (ama restart etme).
  - **Senaryo:** Servis başladı ama DB bağlantısını henüz kuramadı. Liveness OK, Readiness FAIL olmalı. Neden?
  - **Anahtar soru:** Her ikisi de `/health` endpoint'i kullansa ne olur?

---

- **📚 CI/CD nedir? Neden kod her commit'te otomatik deploy edilmeli?**
  GitHub'a push ettikten sonra testler otomatik çalışıyor, Docker image otomatik build ediliyor. Bu CI/CD.
  - **CI (Continuous Integration):** Kod değişikliği her commit'te otomatik test edilir.
  - **CD (Continuous Delivery/Deployment):** Test geçtikten sonra otomatik staging'e, onay sonrası production'a gider.
  - **Anahtar soru:** "Production'a deployment sadece salı günleri yapılır" politikasının dezavantajı nedir? Sık deploy neden daha güvenlidir?
