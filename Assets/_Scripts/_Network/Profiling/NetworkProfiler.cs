using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Arena.Core;
using Arena.Logging;

namespace Arena.Network.Profiling
{
    public class NetworkProfiler
    {
        private readonly IGameLogger _logger;
        private readonly Stopwatch _stopwatch = new();
        
        // Metrics
        private readonly Queue<float> _latencySamples = new(100);
        
        // Message counting
        private int _messageCount;
        
        // Bandwidth calculation (resets every second)
        private long _bytesSent;
        private long _bytesReceived;
        
        // Cumulative totals (never reset)
        private long _totalBytesSentCumulative;
        private long _totalBytesReceivedCumulative;
        
        // Packet counters (never reset)
        private int _totalPacketsSent;
        private int _totalPacketsReceived;
        private int _packetsLost;
        
        // Delta Compression Metrics
        private long _deltaPacketsSent;
        private long _fullPacketsSent;
        private long _deltaBytesSent;
        private long _fullBytesSent;
        
        // Timings
        private float _lastResetTime;
        private float _startTime;
        private const float ResetInterval = 1f;

        public NetworkProfiler(IGameLogger logger)
        {
            _logger = logger;
            _stopwatch.Start();
            _lastResetTime = Time.time;
            _startTime = Time.time;
        }

        public void RecordPacketSent(int bytes)
        {
            _bytesSent += bytes;
            _totalBytesSentCumulative += bytes;
            _totalPacketsSent++;
        }

        public void RecordPacketReceived(int bytes)
        {
            _bytesReceived += bytes;
            _totalBytesReceivedCumulative += bytes;
            _totalPacketsReceived++;
        }

        public void RecordPacketLoss()
        {
            _packetsLost++;
        }

        public void RecordMessage(MessageType type)
        {
            _messageCount++;  // ← 단순 증가만!
        }

        public void RecordLatency(float milliseconds)
        {
            _latencySamples.Enqueue(milliseconds);
            
            if (_latencySamples.Count > 100)
                _latencySamples.Dequeue();
        }

        public void RecordDeltaPacket(int bytes)
        {
            _deltaPacketsSent++;
            _deltaBytesSent += bytes;
        }

        public void RecordFullPacket(int bytes)
        {
            _fullPacketsSent++;
            _fullBytesSent += bytes;
        }

        public NetworkMetrics GetMetrics()
        {
            float deltaTime = Time.time - _lastResetTime;
            float elapsedTime = Time.time - _startTime;
            
            var metrics = new NetworkMetrics
            {
                AverageLatencyMs = CalculateAverageLatency(),
                MinLatencyMs = CalculateMinLatency(),
                MaxLatencyMs = CalculateMaxLatency(),
                PacketLossRate = CalculatePacketLossRate(),
                BandwidthSentKBps = (_bytesSent / 1024f) / Mathf.Max(deltaTime, 0.001f),
                BandwidthReceivedKBps = (_bytesReceived / 1024f) / Mathf.Max(deltaTime, 0.001f),
                MessagesPerSecond = _messageCount / Mathf.Max(deltaTime, 0.001f),  // ← 단순 계산!
                TotalPacketsSent = _totalPacketsSent,
                TotalPacketsReceived = _totalPacketsReceived,
                TotalBytesSent = _totalBytesSentCumulative,
                TotalBytesReceived = _totalBytesReceivedCumulative,
                ElapsedTime = elapsedTime,
                
                // Delta stats
                DeltaPacketsSent = _deltaPacketsSent,
                FullPacketsSent = _fullPacketsSent,
                DeltaBytesSent = _deltaBytesSent,
                FullBytesSent = _fullBytesSent
            };

            if (deltaTime >= ResetInterval)
            {
                ResetCounters();
            }

            return metrics;
        }

        public void LogMetrics()
        {
            var metrics = GetMetrics();
            
            string baseLog = $"Latency: {metrics.AverageLatencyMs:F1}ms | " +
                           $"Loss: {metrics.PacketLossRate:P2} | " +
                           $"Bandwidth: ↑{metrics.BandwidthSentKBps:F1} KB/s ↓{metrics.BandwidthReceivedKBps:F1} KB/s | " +
                           $"Messages: {metrics.MessagesPerSecond:F0}/s";
            
            if (metrics.IsDeltaCompressionEnabled)
            {
                baseLog += $" | Delta: {metrics.BandwidthSavedPercent:F0}% saved";
            }
            
            _logger.Log(LogLevel.Info, "Profiler", baseLog);
        }

        private float CalculateAverageLatency()
        {
            if (_latencySamples.Count == 0) return 0f;
            
            float sum = 0f;
            foreach (var sample in _latencySamples)
                sum += sample;
            
            return sum / _latencySamples.Count;
        }

        private float CalculateMinLatency()
        {
            if (_latencySamples.Count == 0) return 0f;
            
            float min = float.MaxValue;
            foreach (var sample in _latencySamples)
                if (sample < min) min = sample;
            
            return min;
        }

        private float CalculateMaxLatency()
        {
            if (_latencySamples.Count == 0) return 0f;
            
            float max = 0f;
            foreach (var sample in _latencySamples)
                if (sample > max) max = sample;
            
            return max;
        }

        private float CalculatePacketLossRate()
        {
            int total = _totalPacketsSent + _packetsLost;
            return total > 0 ? (float)_packetsLost / total : 0f;
        }

        private void ResetCounters()
        {
            // Only reset rate-calculation variables
            _bytesSent = 0;
            _bytesReceived = 0;
            _messageCount = 0;
            _deltaPacketsSent = 0;
            _fullPacketsSent = 0;
            _deltaBytesSent = 0;
            _fullBytesSent = 0;
            _lastResetTime = Time.time;
        }
    }

    public class NetworkMetrics
    {
        public float AverageLatencyMs { get; set; }
        public float MinLatencyMs { get; set; }
        public float MaxLatencyMs { get; set; }
        public float PacketLossRate { get; set; }
        public float BandwidthSentKBps { get; set; }
        public float BandwidthReceivedKBps { get; set; }
        public float MessagesPerSecond { get; set; }
        public int TotalPacketsSent { get; set; }
        public int TotalPacketsReceived { get; set; }
        public long TotalBytesSent { get; set; }
        public long TotalBytesReceived { get; set; }
        public float ElapsedTime { get; set; }
        
        // Delta compression stats (optional)
        public long DeltaPacketsSent { get; set; }
        public long FullPacketsSent { get; set; }
        public long DeltaBytesSent { get; set; }
        public long FullBytesSent { get; set; }
        
        // Computed properties
        public bool IsDeltaCompressionEnabled => (DeltaPacketsSent + FullPacketsSent) > 0;
        public long TotalDeltaPackets => DeltaPacketsSent + FullPacketsSent;
        public long EstimatedFullBytes => TotalDeltaPackets * 80;
        public long ActualBytes => DeltaBytesSent + FullBytesSent;
        public long BytesSaved => EstimatedFullBytes - ActualBytes;
        public float CompressionRatio => EstimatedFullBytes > 0 ? 1f - ((float)ActualBytes / EstimatedFullBytes) : 0f;
        public float BandwidthSavedPercent => CompressionRatio * 100f;

        public override string ToString()
        {
            string baseStr = $"Latency: {AverageLatencyMs:F1}ms | Loss: {PacketLossRate:P2} | " +
                           $"Bandwidth: ↑{BandwidthSentKBps:F1} ↓{BandwidthReceivedKBps:F1} KB/s";
            
            if (IsDeltaCompressionEnabled)
            {
                baseStr += $" | Delta: {BandwidthSavedPercent:F0}% saved";
            }
            
            return baseStr;
        }
    }
}