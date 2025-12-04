using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Arena.Core;
using Arena.Logging;

namespace Arena.Network
{
    public class PacketProcessor : IPacketProcessor
    {
        private readonly IGameLogger _logger;
        private readonly PacketManager _packetManager;
        private uint _sequenceNumber;
        private readonly object _sequenceLock = new();

        public PacketProcessor(IGameLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _packetManager = new PacketManager(logger);
        }

        // Message -> Packet
        public NetworkPacket[] CreatePackets(INetworkMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            try
            {
                var messageData = message.Serialize();
                bool compress = ShouldCompress(message.Type, messageData.Length);
                
                if (compress)
                {
                    messageData = CompressData(messageData);
                }

                var packets = new List<NetworkPacket>();

                if (messageData.Length <= GameConstants.Network.MaxPayloadSize)
                {
                    var packet = CreateSinglePacket(messageData, compress, message.Type);
                    packet.Header.TargetClientId = message.TargetId;
                    packets.Add(packet);
                }
                else
                {
                    var fragmentedPackets = CreateFragmentedPackets(messageData, compress, message.Type);
                    foreach (var packet in fragmentedPackets)
                    {
                        packet.Header.TargetClientId = message.TargetId;
                    }
                    packets.AddRange(fragmentedPackets);
                }

                return packets.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "CreatePackets");
                throw new NetworkException("Failed to create packets", NetworkErrorCode.PacketCreationFailed, ex);
            }
        }

        public INetworkMessage ProcessPacket(NetworkPacket packet)
        {
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            try
            {
                INetworkMessage message;
        
                if (packet.Header.Flags.HasFlag(PacketFlags.Fragment))
                {
                    message = _packetManager.ProcessFragmentedPacket(packet);
                }
                else
                {
                    message = ProcessSinglePacket(packet);
                }
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "ProcessPacket");
                return null;
            }
        }


        private NetworkPacket CreateSinglePacket(byte[] data, bool compressed, MessageType messageType)
        {
            var flags = compressed ? PacketFlags.Compressed : PacketFlags.None;
            if (IsCriticalMessageType(messageType))
            {
                flags |= PacketFlags.Reliable;
            }
            
            var packet = new NetworkPacket
            {
                Header = new PacketHeader
                {
                    Type = GetPacketTypeFromMessage(messageType),
                    SequenceNumber = GetNextSequenceNumber(),
                    Flags = flags
                },
                Payload = data
            };

            return packet;
        }

        private List<NetworkPacket> CreateFragmentedPackets(byte[] data, bool compressed, MessageType messageType)
        {
            var packets = new List<NetworkPacket>();
            var fragmentId = (ushort)(GetNextSequenceNumber() & 0xFFFF);
            var totalFragments = (int)Math.Ceiling((double)data.Length / GameConstants.Network.MaxPayloadSize);

            for (int i = 0; i < totalFragments; i++)
            {
                var offset = i * GameConstants.Network.MaxPayloadSize;
                var length = Math.Min(GameConstants.Network.MaxPayloadSize, data.Length - offset);
                var fragmentData = new byte[length];
                Array.Copy(data, offset, fragmentData, 0, length);

                var flags = PacketFlags.Fragment;
                if (compressed) flags |= PacketFlags.Compressed;
                if (i == totalFragments - 1) flags |= PacketFlags.LastFragment;
                if (IsCriticalMessageType(messageType)) flags |= PacketFlags.Reliable;

                var packet = new NetworkPacket
                {
                    Header = new PacketHeader
                    {
                        Type = GetPacketTypeFromMessage(messageType),
                        SequenceNumber = GetNextSequenceNumber(),
                        FragmentId = fragmentId,
                        Flags = flags
                    },
                    Payload = fragmentData
                };

                packets.Add(packet);
            }

            _logger.Log(LogLevel.Debug, "Packet", "Created {0} fragments for message", totalFragments);
            return packets;
        }

        private INetworkMessage ProcessSinglePacket(NetworkPacket packet)
        {
            var data = packet.Payload;

            if (packet.Header.Flags.HasFlag(PacketFlags.Compressed))
            {
                data = DecompressData(data);
            }

            return MessageFactory.CreateMessage(data, data.Length);
        }

        private byte[] CompressData(byte[] data)
        {
            try
            {
                using var output = new MemoryStream();
                using (var gzip = new GZipStream(output, CompressionMode.Compress))
                {
                    gzip.Write(data, 0, data.Length);
                }
                
                var compressed = output.ToArray();
                _logger.Log(LogLevel.Debug, "Compression", "Compressed {0} -> {1} bytes ({2:P1})", 
                    data.Length, compressed.Length, (float)compressed.Length / data.Length);
                
                return compressed;
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "CompressData");
                return data;
            }
        }

        private byte[] DecompressData(byte[] data)
        {
            try
            {
                using var input = new MemoryStream(data);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                
                gzip.CopyTo(output);
                return output.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "DecompressData");
                throw new NetworkException("Decompression failed", NetworkErrorCode.DecompressionFailed, ex);
            }
        }

        private bool ShouldCompress(MessageType type, int dataLength)
        {
            return dataLength > 100 && (type == MessageType.VisionData);
        }

        private uint GetNextSequenceNumber()
        {
            lock (_sequenceLock)
            {
                return ++_sequenceNumber;
            }
        }

        private bool IsCriticalMessageType(MessageType type)
        {
            return type == MessageType.Fire 
                   || type == MessageType.BulletSpawn 
                   || type == MessageType.BulletDestroy
                   || type == MessageType.PlayerDeath
                   || type == MessageType.Respawn;
        }

        private PacketType GetPacketTypeFromMessage(MessageType messageType)
        {
            return messageType switch
            {
                MessageType.Handshake => PacketType.Handshake,
                MessageType.Heartbeat => PacketType.Heartbeat,
                MessageType.PlayerJoined => PacketType.PlayerLifecycle,
                MessageType.PlayerLeft => PacketType.PlayerLifecycle,
                _ => PacketType.GameData
            };
        }
    }
}