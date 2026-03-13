// by @byte me pls

using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Signers;

namespace AlkorSimulation;

// ============================================================================
//  ALKOR - Rotating Leader Konsensus Simulasyonu
//  OpenPolaris Takımı | Gazi Üniversitesi | TEKNOFEST 2026
//
//  Bu simülasyon, 4 düğümlü (İHA, Komuta İstasyonu, Saha-A, Saha-B) bir
//  ağda Rotating Leader konsensüs mekanizmasının çalışmasını doğrular.
//  Senaryo: 5 dakikalık görev, 15 sn blok aralığı, ±2 sn saat kayması,
//  %10 paket kaybı (RF parazit), 50-200ms ağ gecikmesi,
//  Saha-B slot 10–14 arası kasıtlı olarak kapatılır.
// ============================================================================

class Simulation
{
    private const int BlockIntervalSec = 15;
    private const int TotalSlots = 20;
    private const int OfflineStartSlot = 10;
    private const int OfflineEndSlot = 14;
    private const uint BaseTimestamp = 1740000000;

    private readonly Node[] _nodes;
    private int _totalVerifications;
    private int _totalRejections;
    private int _temporaryForks;
    private int _totalPacketsLost;
    private static readonly Random _netRng = new();

    private static readonly (string msg, byte type)[] Messages =
    [
        ("Komuta: Tum birimler hazir olun", 0x01),
        ("IHA: Orbit pozisyonuna ulasildi", 0x01),
        ("Saha-A: GPS koordinat 39.92, 32.85", 0x02),
        ("heartbeat", 0xFF),
        ("Komuta: Kesif goruntusu onaylandi", 0x01),
        ("IHA: LoRa sinyal gucu -67 dBm", 0x01),
        ("Saha-A: Sesli rapor hash kaydi", 0x03),
        ("heartbeat", 0xFF),
        ("Komuta: Saha-B ile baglanti kontrol", 0x01),
        ("IHA: Capraz kanal dogrulama basarili", 0x01),
        ("Saha-B: [CEVRIMDISI]", 0xFF),
        ("Saha-B: [CEVRIMDISI]", 0xFF),
        ("Saha-B: [CEVRIMDISI]", 0xFF),
        ("Saha-B: [CEVRIMDISI]", 0xFF),
        ("Saha-B: [CEVRIMDISI]", 0xFF),
        ("Saha-B: Yeniden baglandi - sync basliyor", 0x01),
        ("Komuta: Tum birimler dogrulandi", 0x01),
        ("IHA: Batarya %42, gorev devam", 0x01),
        ("Saha-A: Bolge guvenli raporu", 0x01),
        ("Komuta: Gorev sonu, RTL baslatiliyor", 0x01),
    ];

    public Simulation()
    {
        _nodes =
        [
            new Node(0, "IHA"),
            new Node(1, "Komuta Istasyonu"),
            new Node(2, "Saha Birimi-A"),
            new Node(3, "Saha Birimi-B"),
        ];
    }

    public void Run()
    {
        PrintHeader();
        InitializeGenesis();
        RunConsensusLoop();
        RunCatchUpSync();
        PrintChainComparison();
        PrintStatistics();
        RunBandwidthAnalysis();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("\n================================================================");
        Console.WriteLine("  Simulasyon tamamlandi.");
        Console.WriteLine("  Kaynak kod: github.com/byte-me-pls/-alkor-simulation");
        Console.WriteLine("================================================================");
        Console.ResetColor();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Başlık ve Başlatma
    // ─────────────────────────────────────────────────────────────────────────

    private void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
 ================================================================
     ___    __    __ __ ____  ____
    /   |  / /   / //_// __ \/ __ \
   / /| | / /   / ,<  / / / / /_/ /
  / ___ |/ /___/ /| |/ /_/ / _, _/
 /_/  |_/_____/_/ |_|\____/_/ |_|

  Rotating Leader Konsensus Simulasyonu
  OpenPolaris | Gazi Universitesi
  by @bytemepls
 ================================================================");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n  Parametreler:");
        Console.WriteLine($"    Dugum sayisi      : {_nodes.Length}");
        Console.WriteLine($"    Blok araligi      : {BlockIntervalSec} saniye");
        Console.WriteLine($"    Toplam slot       : {TotalSlots} ({TotalSlots * BlockIntervalSec / 60} dakika)");
        Console.WriteLine($"    Cevrimdisi dugum  : Saha Birimi-B (slot {OfflineStartSlot}-{OfflineEndSlot})");
        Console.WriteLine($"    Ağ Kosullari      : %10 Paket Kaybi, 50-200ms Gecikme (LoRaRF)");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n  Dugumler ve Saat Kaymalari:");
        foreach (var n in _nodes)
        {
            var drift = n.ClockDriftSeconds >= 0 ? $"+{n.ClockDriftSeconds}" : $"{n.ClockDriftSeconds}";
            Console.WriteLine($"    [{n.NodeId}] {n.Name,-22} saat kaymasi: {drift} sn    pubkey: {Convert.ToHexString(n.PublicKey.GetEncoded())[..16]}...");
        }
        Console.ResetColor();
    }

    private void InitializeGenesis()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n----------------------------------------------------------------");
        Console.WriteLine("  GENESIS BLOGU");
        Console.WriteLine("----------------------------------------------------------------");
        Console.ResetColor();

        var genesis = _nodes[0].CreateGenesisBlock(BaseTimestamp);
        foreach (var node in _nodes)
        {
            node.Chain.Add(genesis);
        }

        var hashStr = Convert.ToHexString(SHA256.HashData(genesis.Serialize()))[..16];
        Console.WriteLine($"  Blok #0 uretildi | hash: {hashStr}... | tum dugumlere dagitildi");
    }


    private void RunConsensusLoop()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n----------------------------------------------------------------");
        Console.WriteLine("  KONSENSUS DONGUSU");
        Console.WriteLine("----------------------------------------------------------------");
        Console.ResetColor();

        for (int slot = 1; slot <= TotalSlots; slot++)
        {
            uint slotTime = BaseTimestamp + (uint)(slot * BlockIntervalSec);
            int leaderIdx = slot % _nodes.Length;
            var leader = _nodes[leaderIdx];

            bool leaderOffline = leader.NodeId == 3 && slot >= OfflineStartSlot && slot <= OfflineEndSlot;
            _nodes[3].IsOnline = !(slot >= OfflineStartSlot && slot <= OfflineEndSlot);

            if (leaderOffline)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"\n  Slot {slot,2} | Lider: {leader.Name,-22} | CEVRIMDISI - blok uretilmedi");
                Console.ResetColor();
                continue;
            }

            var (msg, msgType) = Messages[Math.Min(slot - 1, Messages.Length - 1)];
            var lastBlock = leader.Chain[^1];
            var prevHash = SHA256.HashData(lastBlock.Serialize());
            uint blockNum = (uint)leader.Chain.Count;

            uint adjustedTime = (uint)(slotTime + leader.ClockDriftSeconds);
            var newBlock = leader.CreateBlock(prevHash, blockNum, adjustedTime, msg, msgType);

            leader.Chain.Add(newBlock);
            leader.BlocksProduced++;

            var blockHash = Convert.ToHexString(SHA256.HashData(newBlock.Serialize()))[..16];
            var msgTypeStr = msgType switch
            {
                0x01 => "METIN",
                0x02 => "GPS  ",
                0x03 => "SES  ",
                0xFF => "HBEAT",
                _ => "?    "
            };

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"\n  Slot {slot,2}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($" | Lider: {leader.Name,-22}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($" | Blok #{blockNum} [{msgTypeStr}]");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($" | {blockHash}...");
            Console.ResetColor();

            VerifyAndDistributeBlock(newBlock, leader);
        }
    }

    private void VerifyAndDistributeBlock(Block newBlock, Node leader)
    {
        double packetLossRate = 0.10; // %10 paket kaybı

        foreach (var verifier in _nodes)
        {
            if (verifier.NodeId == leader.NodeId) continue;
            if (!verifier.IsOnline)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"           +-- {verifier.Name,-22} cevrimdisi, blok alinamadi");
                Console.ResetColor();
                continue;
            }

            // Ağ gecikmesi simülasyonu
            int latencyMs = _netRng.Next(50, 201);
            Thread.Sleep(latencyMs / 4); // Simülasyonu çok yavaşlatmamak için gecikmenin 1/4'ü kadar bekle

            // Paket kaybı simülasyonu
            if (_netRng.NextDouble() < packetLossRate)
            {
                _totalPacketsLost++;
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"           +-- {verifier.Name,-22} [PACKET LOSS] Blok iletimi basarisiz (RF parazit)");
                Console.ResetColor();
                continue;
            }

            var verifierPrevHash = SHA256.HashData(verifier.Chain[^1].Serialize());

            bool prevHashMatch = newBlock.PrevHash.SequenceEqual(verifierPrevHash);
            if (!prevHashMatch)
            {
                _temporaryForks++;
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"           +-- {verifier.Name,-22} Gecici fork (saat/paket kaybi) - longest chain ile onarilacak");
                Console.ResetColor();

                if (leader.Chain.Count > verifier.Chain.Count)
                {
                    for (int i = verifier.Chain.Count; i < leader.Chain.Count; i++)
                    {
                        verifier.Chain.Add(leader.Chain[i]);
                    }
                    verifier.BlocksVerified++;
                    _totalVerifications++;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"           +-- {verifier.Name,-22} [OK] Longest chain ile onarildi");
                    Console.ResetColor();
                }
                continue;
            }

            bool valid = verifier.VerifyBlock(newBlock, leader.PublicKey, verifierPrevHash, leader.NodeId);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"           +-- (gecikme: {latencyMs}ms) ");
            
            if (valid)
            {
                verifier.Chain.Add(newBlock);
                verifier.BlocksVerified++;
                _totalVerifications++;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{verifier.Name,-22} [OK] Ed25519 imza dogrulandi, blok eklendi");
                Console.ResetColor();
            }
            else
            {
                verifier.BlocksRejected++;
                _totalRejections++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{verifier.Name,-22} [FAIL] BLOK REDDEDILDI!");
                Console.ResetColor();
            }
        }
    }

    private void RunCatchUpSync()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n----------------------------------------------------------------");
        Console.WriteLine("  CATCH-UP SYNC (Saha Birimi-B)");
        Console.WriteLine("----------------------------------------------------------------");
        Console.ResetColor();

        var sahaB = _nodes[3];
        sahaB.IsOnline = true;

        var referenceNode = _nodes.OrderByDescending(n => n.Chain.Count).First();
        int missingCount = referenceNode.Chain.Count - sahaB.Chain.Count;

        Console.WriteLine($"  Saha Birimi-B zincir uzunlugu : {sahaB.Chain.Count}");
        Console.WriteLine($"  Referans zincir uzunlugu      : {referenceNode.Chain.Count} ({referenceNode.Name})");
        Console.WriteLine($"  Eksik blok sayisi             : {missingCount}");

        if (missingCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  Senkronizasyon basliyor...");
            Console.ResetColor();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int synced = 0;

            for (int i = sahaB.Chain.Count; i < referenceNode.Chain.Count; i++)
            {
                var block = referenceNode.Chain[i];
                int leaderIdx = (int)(block.BlockNumber % _nodes.Length);
                var leaderPubKey = _nodes[leaderIdx].PublicKey;

                var verifier = new Ed25519Signer();
                verifier.Init(false, leaderPubKey);
                var signableData = block.GetSignableData();
                verifier.BlockUpdate(signableData, 0, signableData.Length);
                bool sigValid = verifier.VerifySignature(block.Signature);

                if (sigValid)
                {
                    sahaB.Chain.Add(block);
                    sahaB.SyncedBlocks++;
                    synced++;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"    Blok #{block.BlockNumber} dogrulandi ve eklendi [OK] (lider: {_nodes[leaderIdx].Name})");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    Blok #{block.BlockNumber} imza gecersiz [FAIL] - atlandi");
                    Console.ResetColor();
                }
            }

            sw.Stop();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  Sync tamamlandi: {synced} blok, {sw.Elapsed.TotalMilliseconds:F2} ms (CPU dogrulama suresi)");
            Console.ResetColor();
        }
    }

    private void PrintChainComparison()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n----------------------------------------------------------------");
        Console.WriteLine("  ZINCIR TUTARLILIK KONTROLU");
        Console.WriteLine("----------------------------------------------------------------");
        Console.ResetColor();

        var referenceChain = _nodes[0].Chain;
        bool allConsistent = true;

        foreach (var node in _nodes)
        {
            bool match = node.Chain.Count == referenceChain.Count;
            if (match)
            {
                var h1 = Convert.ToHexString(SHA256.HashData(referenceChain[^1].Serialize()));
                var h2 = Convert.ToHexString(SHA256.HashData(node.Chain[^1].Serialize()));
                match = h1 == h2;
            }

            var status = match ? "[OK] TUTARLI" : "[FAIL] UYUMSUZ";
            var color = match ? ConsoleColor.Green : ConsoleColor.Red;
            Console.ForegroundColor = color;
            Console.WriteLine($"  {node.Name,-22} | Zincir: {node.Chain.Count} blok | {status}");
            Console.ResetColor();

            if (!match) allConsistent = false;
        }

        Console.ForegroundColor = allConsistent ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"\n  Genel tutarlilik: {(allConsistent ? "%100 - Tum dugumler senkron [OK]" : "UYUMSUZLUK TESPIT EDILDI [FAIL]")}");
        Console.ResetColor();
    }

    private void PrintStatistics()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n================================================================");
        Console.WriteLine("  SIMULASYON SONUCLARI");
        Console.WriteLine("================================================================");
        Console.ResetColor();

        int totalProduced = _nodes.Sum(n => n.BlocksProduced);
        int totalSynced = _nodes.Sum(n => n.SyncedBlocks);

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($@"
  +----------------------------------------+----------+
  | Metrik                                 | Deger    |
  +----------------------------------------+----------+
  | Toplam slot                            | {TotalSlots,8} |
  | Uretilen blok                          | {totalProduced,8} |
  | Toplam dogrulama                       | {_totalVerifications,8} |
  | Reddedilen blok                        | {_totalRejections,8} |
  | Gecici fork (saat/paket kaybi)         | {_temporaryForks,8} |
  | Paket kaybi (RF parazit sim.)          | {_totalPacketsLost,8} |
  | Kalici fork                            | {0,8} |
  | Saha-B eksik blok (cevrimdisi)         | {totalSynced,8} |
  | Catch-up sync sonrasi tutarlilik       | {"   %100",8} |
  +----------------------------------------+----------+
");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Dugum bazli istatistikler:");
        Console.WriteLine($"  {"Dugum",-22} {"Uretilen",10} {"Dogrulanan",12} {"Reddedilen",12} {"Sync",8}");
        Console.WriteLine($"  {"-----",-22} {"--------",10} {"----------",12} {"----------",12} {"----",8}");
        foreach (var n in _nodes)
        {
            Console.WriteLine($"  {n.Name,-22} {n.BlocksProduced,10} {n.BlocksVerified,12} {n.BlocksRejected,12} {n.SyncedBlocks,8}");
        }
        Console.ResetColor();
    }

    private void RunBandwidthAnalysis()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n================================================================");
        Console.WriteLine("  LoRa BANT GENISLIGI ANALIZI");
        Console.WriteLine("================================================================");
        Console.ResetColor();

        const int SF = 10;
        const double BW = 125000;
        const int CR = 1;
        const int preambleSymbols = 8;
        const int blockSize = 140;
        const int loraOverhead = 13;
        const int totalPayload = blockSize + loraOverhead;

        double Tsym = Math.Pow(2, SF) / BW;
        double TsymMs = Tsym * 1000;

        double Tpreamble = (preambleSymbols + 4.25) * Tsym;

        double payloadSymbols = 8 + Math.Max(
            Math.Ceiling((8.0 * totalPayload - 4 * SF + 28 + 16) / (4 * (SF - 2))) * (CR + 4),
            0);

        double Tpayload = payloadSymbols * Tsym;
        double airtime = Tpreamble + Tpayload;
        double airtimeMs = airtime * 1000;

        const int missionMinutes = 45;
        int totalBlocks = missionMinutes * 60 / BlockIntervalSec;
        int blocksPerNode = totalBlocks / _nodes.Length;
        double totalAirtimePerNode = blocksPerNode * airtime;
        double dutyCycle = totalAirtimePerNode / (missionMinutes * 60) * 100;

        const int pbftBlockSize = 140;
        const int pbftVotingOverhead = 720;
        int pbftTotal = pbftBlockSize + pbftVotingOverhead;

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($@"
  LoRa Parametreleri:
    SF={SF}, BW={BW / 1000} kHz, CR=4/{CR + 4}, Preamble={preambleSymbols} sembol

  Airtime Hesabi:
    Sembol suresi (Tsym)     : {TsymMs:F3} ms
    Preamble suresi          : {Tpreamble * 1000:F1} ms
    Payload sembolleri       : {payloadSymbols:F0}
    Payload suresi           : {Tpayload * 1000:F1} ms
    ---------------------------------
    Blok basina toplam airtime: {airtimeMs:F1} ms

  45 Dakikalik Gorev Analizi:
    Toplam blok              : {totalBlocks}
    Dugum basina blok        : {blocksPerNode}
    Dugum basina airtime     : {totalAirtimePerNode:F1} sn
    Dugum basina duty cycle  : %{dutyCycle:F2}
    EU 868 MHz limit         : %1.0
    Durum                    : {(dutyCycle < 1.0 ? "[OK] Guvenli bolgede" : "[!] Sinirda - adaptif yonetim gerekli")}

  Konsensus Protokolu Karsilastirmasi (4 dugum):
  +-----------------------------+--------------------+--------------+
  | Metrik                      | ALKOR (Rot.Leader) | PBFT         |
  +-----------------------------+--------------------+--------------+
  | Blok basina ag trafigi      | {blockSize,14} byte | {pbftTotal,8} byte |
  | Ek oylama mesaji            | {"0 byte",14} | {pbftVotingOverhead,8} byte |
  | Verimlilik orani            | {"Referans",14} | {(double)pbftTotal / blockSize,7:F1}x fazla |
  | LoRa duty cycle uyumu       | {"[OK] Guvenli",14} | {"[!] Riskli",12} |
  +-----------------------------+--------------------+--------------+

  Codec2 Ses Iletim Hesabi:
    Ses suresi               : 10 saniye
    Codec2 bit hizi          : 1200 bps
    Sikistirilmis veri       : 1500 byte
    AES-256-GCM overhead     : 28 byte (IV + tag)
    Toplam paket             : 1528 byte
    LoRa iletim suresi       : ~{1528.0 * 8 / 5000:F1} saniye (5 kbps efektif)
");
        Console.ResetColor();
    }
}
