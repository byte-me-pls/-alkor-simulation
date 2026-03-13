# ALKOR Konsensüs Simülasyonu

**OpenPolaris Takımı | Gazi Üniversitesi | TEKNOFEST 2026**

Bu proje, ALKOR İHA (İnsansız Hava Aracı) haberleşme projesi için geliştirilen **Rotating Leader** (Dönen Lider) konsensüs mekanizmasının bir C# simülasyonudur. Simülasyon, İHA, Komuta İstasyonu ve iki Saha Biriminden (A ve B) oluşan 4 düğümlü bir LoRa mesh ağını modellemektedir.

## Özellikler

- **Rotating Leader Konsensüs Algoritması:** Düğümler, sırayla liderlik görevini üstlenerek bloklar üretir. Lider çevrimdışı olduğunda sıra bir sonrakine geçer.
- **Ed25519 Kriptografik İmzalar:** Gerçek dünya güvenliğini simüle etmek için her düğümün kendi Ed25519 anahtar çifti vardır (BouncyCastle kütüphanesi kullanılarak).
- **Zorlu Ağ Koşullarının Simülasyonu:**
  - **Saat Kayması (Clock Drift):** Düğümler arasında rastgele saat farkları simüle edilir.
  - **Düğüm Çevrimdışılığı:** Önceden belirlenmiş aralıklarda bazı düğümlerin (örn. Saha-B) çevrimdışı olması durumu test edilir.
  - **Paket Kaybı:** RF parazitlenmesini modellemek için rastgele paket kayıpları oluşturulur (%10 kayıp oranı).
  - **Ağ Gecikmesi (Latency):** LoRa modülasyonuna uygun olarak 50ms - 200ms arasında rastgele gecikmeler simüle edilir.
- **Otomatik Senkronizasyon (Catch-up Sync):** Çevrimdışı kalan düğümler veya paket kaybeden düğümler, ağın en güncel zincirinden eksik bloklarını tamamlar; geçici çatallanmalar (fork) otomatik onarılır.
- **LoRa Bant Genişliği Analizi:** Uygulamanın bant genişliği analizi modülü, konsensüs iletimi için harcanan havada kalma (airtime) süresini, görev duty cycle uyumluluğunu (EU 868 MHz bandı için %1 limit vb.) ve standart PBFT ile blok boyutu (overhead) kıyaslamasını raporlar.

## Gereksinimler

- .NET 9.0 SDK veya daha yeni bir sürüm.

## Çalıştırma

Projeyi çalıştırmak için konsola aşağıdaki komutu girin:

```bash
dotnet run
```

veyahut Windows ortamında `run.bat` dosyasını çalıştırabilirsiniz.

## Simülasyon Çıktısı

Simülasyon çalıştığında aşağıdaki adımları konsola yazdırır:
1. Başlama mesajları ve düğüm konfigürasyonu (saat kaymaları vs.).
2. Genesis (Sıfırıncı) bloğun oluşturulması.
3. Her slot için konsensüs döngüsü (blok üretimi, gecikme, paket kaybı kayıtları, imzaların doğrulanması).
4. Çevrimdışı olan düğümlerin ağa dönme ve eksik blokları onarma işlemi (Catch-up Sync).
5. Tüm düğümlerdeki zincirlerin tutarlılık kontrolü.
6. Simülasyon istatistikleri ve genel hata toleransı raporu.
7. LoRa bant genişliği detaylı raporu.

## Lisans

MIT Lisansı - Bu proje OpenPolaris takımı tarafından geliştirilmiş olup eğitim ve araştırma amaçlı açık kaynaklıdır.
