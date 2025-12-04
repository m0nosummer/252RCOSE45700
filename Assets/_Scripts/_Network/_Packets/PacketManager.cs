using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Arena.Logging;

namespace Arena.Network
{
    internal class PacketManager
    {
        private readonly IGameLogger _logger;
        private readonly Dictionary<ushort, FragmentGroup> _fragmentBuffer = new();
        private readonly object _fragmentLock = new();

        private const float FragmentTimeoutSeconds = 5f;

        public PacketManager(IGameLogger logger)
        {
            _logger = logger;
        }

        public INetworkMessage ProcessFragmentedPacket(NetworkPacket packet)
        {
            lock (_fragmentLock)
            {
                var fragmentId = packet.Header.FragmentId;

                if (!_fragmentBuffer.ContainsKey(fragmentId))
                {
                    _fragmentBuffer[fragmentId] = new FragmentGroup
                    {
                        ReceivedTime = Time.time,
                        Packets = new List<NetworkPacket>()
                    };
                }

                var group = _fragmentBuffer[fragmentId];
                group.Packets.Add(packet);

                if (packet.Header.Flags.HasFlag(PacketFlags.LastFragment))
                {
                    group.ExpectedCount = group.Packets.Count;
                }

                if (group.ExpectedCount > 0 && group.Packets.Count >= group.ExpectedCount)
                {
                    _fragmentBuffer.Remove(fragmentId);
                    return ReassembleFragments(group, packet.Header.Flags.HasFlag(PacketFlags.Compressed));
                }

                CleanupOldFragments();
                return null;
            }
        }

        private INetworkMessage ReassembleFragments(FragmentGroup group, bool compressed)
        {
            group.Packets.Sort((a, b) => a.Header.SequenceNumber.CompareTo(b.Header.SequenceNumber));

            // Validate sequence
            for (int i = 1; i < group.Packets.Count; i++)
            {
                uint expected = group.Packets[i - 1].Header.SequenceNumber + 1;
                uint actual = group.Packets[i].Header.SequenceNumber;

                if (actual != expected)
                {
                    _logger.Log(LogLevel.Error, "Packet", "Missing fragment! Expected {0}, got {1}", expected, actual);
                    return null;
                }
            }

            using var stream = new MemoryStream();
            foreach (var fragment in group.Packets)
            {
                stream.Write(fragment.Payload, 0, fragment.Payload.Length);
            }

            var data = stream.ToArray();

            if (compressed)
            {
                // TODO : delta compression
            }

            return MessageFactory.CreateMessage(data, data.Length);
        }

        private void CleanupOldFragments()
        {
            var toRemove = new List<ushort>();

            foreach (var kvp in _fragmentBuffer)
            {
                if (Time.time - kvp.Value.ReceivedTime > FragmentTimeoutSeconds)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                _logger.Log(LogLevel.Warning, "Packet", "Fragment group {0} timed out", id);
                _fragmentBuffer.Remove(id);
            }
        }

        private class FragmentGroup
        {
            public List<NetworkPacket> Packets;
            public float ReceivedTime;
            public int ExpectedCount = -1;
        }
    }
}