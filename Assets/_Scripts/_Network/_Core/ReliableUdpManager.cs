using System.Collections.Generic;
using System.Diagnostics;

namespace Arena.Network
{
    public class ReliableUdpManager
    {
        private class PendingPacket
        {
            public uint SequenceNumber;
            public byte[] Data;
            public double LastSendTime;
            public int RetryCount;
        }

        private readonly Dictionary<uint, PendingPacket> _pendingPackets = new();
        private readonly HashSet<uint> _acknowledgedPackets = new();
        private readonly Stopwatch _stopwatch = new();
        
        private const double RETRY_TIMEOUT = 0.2;
        private const int MAX_RETRIES = 5;

        public ReliableUdpManager()
        {
            _stopwatch.Start();
        }

        public void TrackPacket(uint sequenceNumber, byte[] data)
        {
            _pendingPackets[sequenceNumber] = new PendingPacket
            {
                SequenceNumber = sequenceNumber,
                Data = data,
                LastSendTime = _stopwatch.Elapsed.TotalSeconds,
                RetryCount = 0
            };
        }

        public bool ProcessAck(byte[] data)
        {
            if (data.Length == 8)
            {
                uint magic = System.BitConverter.ToUInt32(data, 0); // First 4 bytes
                if (magic == 0x41434B00)
                {
                    uint seq = System.BitConverter.ToUInt32(data, 4); // Next 4 bytes
                    _acknowledgedPackets.Add(seq);
                    _pendingPackets.Remove(seq);
                    return true;
                }
            }
            return false;
        }

        // not 'MonoBehaviour Update'
        public void Update()
        {
            double currentTime = _stopwatch.Elapsed.TotalSeconds;
            var toRemove = new List<uint>();

            foreach (var kvp in _pendingPackets)
            {
                var packet = kvp.Value;
                
                if (currentTime - packet.LastSendTime > RETRY_TIMEOUT)
                {
                    if (packet.RetryCount >= MAX_RETRIES)
                    {
                        UnityEngine.Debug.LogWarning($"[ReliableUDP] Packet {packet.SequenceNumber} failed after {MAX_RETRIES} retries");
                        toRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var seq in toRemove)
            {
                _pendingPackets.Remove(seq);
            }
        }

        public List<byte[]> GetPacketsToRetry()
        {
            var packetsToRetry = new List<byte[]>();
            double currentTime = _stopwatch.Elapsed.TotalSeconds;
            
            foreach (var kvp in _pendingPackets)
            {
                var packet = kvp.Value;
                
                if (currentTime - packet.LastSendTime > RETRY_TIMEOUT && packet.RetryCount < MAX_RETRIES)
                {
                    packet.LastSendTime = currentTime;
                    packet.RetryCount++;
                    packetsToRetry.Add(packet.Data);
                }
            }

            return packetsToRetry;
        }

        public void Clear()
        {
            _pendingPackets.Clear();
            _acknowledgedPackets.Clear();
        }
    }
}