// by @byte me pls

using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace AlkorSimulation;

class Node
{
    public byte NodeId { get; }
    public string Name { get; }
    public List<Block> Chain { get; } = new();
    public bool IsOnline { get; set; } = true;

    public Ed25519PrivateKeyParameters PrivateKey { get; }
    public Ed25519PublicKeyParameters PublicKey { get; }

    public int BlocksProduced { get; set; }
    public int BlocksVerified { get; set; }
    public int BlocksRejected { get; set; }
    public int SyncedBlocks { get; set; }

    public int ClockDriftSeconds { get; }

    private static readonly Random _rng = new(42);

    public Node(byte id, string name)
    {
        NodeId = id;
        Name = name;
        ClockDriftSeconds = _rng.Next(-2, 3);

        var keyGen = new Org.BouncyCastle.Crypto.Generators.Ed25519KeyPairGenerator();
        keyGen.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var keyPair = keyGen.GenerateKeyPair();
        PrivateKey = (Ed25519PrivateKeyParameters)keyPair.Private;
        PublicKey = (Ed25519PublicKeyParameters)keyPair.Public;
    }

    public Block CreateGenesisBlock(uint timestamp)
    {
        var genesisData = Encoding.UTF8.GetBytes("ALKOR Genesis - OpenPolaris 2026");
        var block = new Block
        {
            PrevHash = new byte[32],
            Timestamp = timestamp,
            LeaderId = NodeId,
            MsgType = 0x00,
            PayloadHash = SHA256.HashData(genesisData),
            BlockNumber = 0,
            Nonce = (ushort)_rng.Next(0, 65536),
            Signature = new byte[64]
        };
        block.Signature = Sign(block.GetSignableData());
        return block;
    }

    public Block CreateBlock(byte[] prevHash, uint blockNum, uint timestamp, string message, byte msgType)
    {
        var payloadHash = SHA256.HashData(Encoding.UTF8.GetBytes(message));
        var block = new Block
        {
            PrevHash = prevHash,
            Timestamp = timestamp,
            LeaderId = NodeId,
            MsgType = msgType,
            PayloadHash = payloadHash,
            BlockNumber = blockNum,
            Nonce = (ushort)_rng.Next(0, 65536),
            Signature = new byte[64]
        };
        block.Signature = Sign(block.GetSignableData());
        return block;
    }

    public bool VerifyBlock(Block block, Ed25519PublicKeyParameters leaderPubKey, byte[] expectedPrevHash, byte expectedLeaderId)
    {
        if (block.LeaderId != expectedLeaderId)
            return false;

        if (!VerifySignature(block.GetSignableData(), block.Signature, leaderPubKey))
            return false;

        if (!block.PrevHash.SequenceEqual(expectedPrevHash))
            return false;

        return true;
    }

    private byte[] Sign(byte[] data)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, PrivateKey);
        signer.BlockUpdate(data, 0, data.Length);
        return signer.GenerateSignature();
    }

    private static bool VerifySignature(byte[] data, byte[] signature, Ed25519PublicKeyParameters pubKey)
    {
        var verifier = new Ed25519Signer();
        verifier.Init(false, pubKey);
        verifier.BlockUpdate(data, 0, data.Length);
        return verifier.VerifySignature(signature);
    }
}
