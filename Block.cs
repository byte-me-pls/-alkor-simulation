// by @byte me pls

using System.Security.Cryptography;

namespace AlkorSimulation;

/// <summary>140 byte sabit boyutlu ALKOR bloğu</summary>
struct Block
{
    public byte[] PrevHash;      // 32 byte – SHA-256
    public uint   Timestamp;     // 4 byte  – UNIX epoch
    public byte   LeaderId;      // 1 byte  – düğüm kimliği
    public byte   MsgType;       // 1 byte  – 0x01=metin, 0x03=ses, 0xFF=heartbeat
    public byte[] PayloadHash;   // 32 byte – SHA-256
    public byte[] Signature;     // 64 byte – Ed25519
    public uint   BlockNumber;   // 4 byte  – sıra numarası
    public ushort Nonce;         // 2 byte  – replay koruması

    public byte[] Serialize()
    {
        using var ms = new MemoryStream(140);
        using var bw = new BinaryWriter(ms);
        bw.Write(PrevHash);          // 32
        bw.Write(Timestamp);         // 4
        bw.Write(LeaderId);          // 1
        bw.Write(MsgType);           // 1
        bw.Write(PayloadHash);       // 32
        bw.Write(Signature);         // 64
        bw.Write(BlockNumber);       // 4
        bw.Write(Nonce);             // 2
        return ms.ToArray();         // 140
    }

    /// <summary>İmza hesaplanacak veri (imza alanı hariç, 76 byte)</summary>
    public byte[] GetSignableData()
    {
        using var ms = new MemoryStream(76);
        using var bw = new BinaryWriter(ms);
        bw.Write(PrevHash);
        bw.Write(Timestamp);
        bw.Write(LeaderId);
        bw.Write(MsgType);
        bw.Write(PayloadHash);
        bw.Write(BlockNumber);
        bw.Write(Nonce);
        return ms.ToArray();
    }
}
