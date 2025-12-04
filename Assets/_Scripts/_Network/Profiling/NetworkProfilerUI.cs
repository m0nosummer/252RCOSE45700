using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Arena.Core.DependencyInjection;
using Arena.Network.Profiling;
using TMPro;

namespace Arena.Network.UI
{
    /// <summary>
    /// In-game UI display for network profiler metrics.
    /// Shows real-time performance data during gameplay.
    /// Automatically displays delta compression stats if enabled.
    /// Press F3 to toggle display.
    /// </summary>
    public class NetworkProfilerUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI latencyText;
        [SerializeField] private TextMeshProUGUI bandwidthText;
        [SerializeField] private TextMeshProUGUI packetLossText;
        [SerializeField] private TextMeshProUGUI messagesText;
        [SerializeField] private GameObject panel;
        
        [Header("Settings")]
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private float updateInterval = 1.0f;
        
        private NetworkProfiler _profiler;
        private float _updateTimer;
        private Keyboard _keyboard;
        private float _startTime;

        private void Start()
        {
            var container = GameInstaller.Container;
            if (container != null && container.IsRegistered<NetworkProfiler>())
            {
                _profiler = container.Resolve<NetworkProfiler>();
            }
            
            if (panel != null)
                panel.SetActive(showOnStart);
            
            _keyboard = Keyboard.current;
            _startTime = Time.time;
        }

        private void Update()
        {
            // F3 토글은 항상 체크
            if (_keyboard != null && _keyboard.f3Key.wasPressedThisFrame && panel != null)
            {
                panel.SetActive(!panel.activeSelf);
            }
            
            if (_profiler == null || panel == null || !panel.activeSelf)
                return;
            
            _updateTimer += Time.deltaTime;
            
            if (_updateTimer >= updateInterval)
            {
                UpdateUI();
                _updateTimer = 0f;
            }
        }

        private void UpdateUI()
        {
            var metrics = _profiler.GetMetrics();
    
            if (latencyText != null)
            {
                latencyText.text = $"Latency: {metrics.AverageLatencyMs:F1}ms " +
                                   $"({metrics.MinLatencyMs:F0}-{metrics.MaxLatencyMs:F0})";
                latencyText.color = GetLatencyColor(metrics.AverageLatencyMs);
            }
    
            if (bandwidthText != null)
            {
                bandwidthText.text = $"Bandwidth: ↑{metrics.BandwidthSentKBps:F1} ↓{metrics.BandwidthReceivedKBps:F1} KB/s\n" +
                                     $"Total: ↑{FormatBytes(metrics.TotalBytesSent)} ↓{FormatBytes(metrics.TotalBytesReceived)}";
            }
    
            if (packetLossText != null)
            {
                packetLossText.text = $"Packet Loss: {metrics.PacketLossRate:P2}";
                packetLossText.color = GetPacketLossColor(metrics.PacketLossRate);
            }
    
            if (messagesText != null)
            {
                string baseInfo = $"Messages: {metrics.MessagesPerSecond:F0}/s\n" +
                                  $"Packets: Sent {metrics.TotalPacketsSent} / Recv {metrics.TotalPacketsReceived}\n";
        
                // 델타 압축이 활성화되어 있으면 자동으로 표시
                if (metrics.IsDeltaCompressionEnabled)
                {
                    baseInfo += $"<color=yellow>Delta Compression: {metrics.BandwidthSavedPercent:F0}% saved</color>\n" +
                                $"Delta: {metrics.DeltaPacketsSent} | Full: {metrics.FullPacketsSent}\n";
                }
        
                baseInfo += $"Elapsed: {metrics.ElapsedTime:F1}s";
        
                messagesText.text = baseInfo;
            }
        }

        private Color GetLatencyColor(float latency)
        {
            if (latency < 50) return Color.green;
            if (latency < 100) return Color.yellow;
            return Color.red;
        }

        private Color GetPacketLossColor(float loss)
        {
            if (loss < 0.01f) return Color.green;
            if (loss < 0.05f) return Color.yellow;
            return Color.red;
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024f:F1} KB";
            else
                return $"{bytes / (1024f * 1024f):F2} MB";
        }
    }
}