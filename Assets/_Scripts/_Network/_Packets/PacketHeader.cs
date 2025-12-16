using System;
using System.IO;
using Arena.Core;

namespace Arena.Network
{
    public class PacketHeader
    {
        public uint MagicNumber { get; set; } = GameConstants.Protocol.MagicNumber;
        public byte Version { get; set; } = GameConstants.Protocol.ProtocolVersion;
        public PacketType Type { get; set; }
        public PacketFlags Flags { get; set; }
        
        public int ClientId { get; set; } = -1;
        public int TargetClientId { get; set; } = -1;
        
        public uint SequenceNumber { get; set; }
        public ushort PayloadSize { get; set; }
        public ushort FragmentId { get; set; } = 0;
    
        public void Serialize(BinaryWriter writer)
        {
            writer.Write(MagicNumber);
            writer.Write(Version);
            writer.Write((byte)Type);
            writer.Write((byte)Flags);
            writer.Write(ClientId);
            writer.Write(TargetClientId);
            writer.Write(SequenceNumber);
            writer.Write(PayloadSize);
            writer.Write(FragmentId);
        }
    
        public static PacketHeader Deserialize(BinaryReader reader)
        {
            var header = new PacketHeader();
        
            header.MagicNumber = reader.ReadUInt32();
            if (header.MagicNumber != 0x41524E41)
            {
                throw new InvalidDataException("Invalid packet magic number!");
            }
        
            header.Version = reader.ReadByte();
            header.Type = (PacketType)reader.ReadByte();
            header.Flags = (PacketFlags)reader.ReadByte();
            header.ClientId = reader.ReadInt32();
            header.TargetClientId = reader.ReadInt32();
            header.SequenceNumber = reader.ReadUInt32();
            header.PayloadSize = reader.ReadUInt16();
            header.FragmentId = reader.ReadUInt16();
        
            return header;
        }
    }
    
    public enum PacketType : byte
    {
        Handshake = 0,
        GameData = 1,
        Heartbeat = 2,
        Disconnect = 3,
        Acknowledgment = 4,
        Fragment = 5,
        PlayerLifecycle = 6 
    }
    
    [Flags]
    public enum PacketFlags : byte
    {
        None = 0,
        Reliable = 1 << 0,
        Compressed = 1 << 1,
        Encrypted = 1 << 2,
        Fragment = 1 << 3,
        LastFragment = 1 << 4,
        Duplicate = 1 << 5,
        Priority = 1 << 6
    }
}