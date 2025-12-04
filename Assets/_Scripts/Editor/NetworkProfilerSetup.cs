#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using Arena.Network.UI;

namespace Arena.Editor
{
    /// <summary>
    /// One-click setup for NetworkProfiler UI with TextMeshPro.
    /// Menu: Tools/Network/Setup Profiler UI
    /// </summary>
    public static class NetworkProfilerSetup
    {
        [MenuItem("Tools/Network/Setup Profiler UI")]
        public static void SetupProfilerUI()
        {
            // 1. Canvas 찾기 또는 생성
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                
                var canvasScaler = canvasObj.AddComponent<CanvasScaler>();
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(1920, 1080);
                
                canvasObj.AddComponent<GraphicRaycaster>();
                
                Debug.Log("[NetworkProfilerSetup] Canvas created");
            }

            // 2. Panel 생성
            GameObject panelObj = new GameObject("NetworkProfilerPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            
            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.7f); // 반투명 검정
            
            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(10, -10);
            panelRect.sizeDelta = new Vector2(350, 180);

            // 3. TextMeshPro 4개 생성
            CreateTMPText(panelObj.transform, "LatencyText", new Vector2(10, -10), new Vector2(330, 25));
            CreateTMPText(panelObj.transform, "BandwidthText", new Vector2(10, -40), new Vector2(330, 40));
            CreateTMPText(panelObj.transform, "PacketLossText", new Vector2(10, -85), new Vector2(330, 25));
            CreateTMPText(panelObj.transform, "MessagesText", new Vector2(10, -115), new Vector2(330, 60));

            // 4. NetworkProfilerUI 컴포넌트 추가
            NetworkProfilerUI profilerUI = panelObj.AddComponent<NetworkProfilerUI>();
            
            // 5. 자동 연결 (Reflection 사용)
            var latencyText = panelObj.transform.Find("LatencyText").GetComponent<TextMeshProUGUI>();
            var bandwidthText = panelObj.transform.Find("BandwidthText").GetComponent<TextMeshProUGUI>();
            var packetLossText = panelObj.transform.Find("PacketLossText").GetComponent<TextMeshProUGUI>();
            var messagesText = panelObj.transform.Find("MessagesText").GetComponent<TextMeshProUGUI>();
            
            var type = profilerUI.GetType();
            var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            
            type.GetField("latencyText", bindingFlags)?.SetValue(profilerUI, latencyText);
            type.GetField("bandwidthText", bindingFlags)?.SetValue(profilerUI, bandwidthText);
            type.GetField("packetLossText", bindingFlags)?.SetValue(profilerUI, packetLossText);
            type.GetField("messagesText", bindingFlags)?.SetValue(profilerUI, messagesText);
            type.GetField("panel", bindingFlags)?.SetValue(profilerUI, panelObj);

            // 6. 완료
            Selection.activeGameObject = panelObj;
            EditorUtility.SetDirty(panelObj);
            
            Debug.Log("[NetworkProfilerSetup] ✅ Profiler UI setup complete with TextMeshPro! Press F3 in Play Mode to toggle.");
            EditorUtility.DisplayDialog(
                "Success!", 
                "Network Profiler UI created with TextMeshPro!\n\n" +
                "• Panel: Top-left corner\n" +
                "• Press F3 in Play Mode to toggle\n" +
                "• Font: TextMeshPro", 
                "OK");
        }

        private static void CreateTMPText(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            
            TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.fontSize = 12;
            tmpText.color = Color.white;
            tmpText.alignment = TextAlignmentOptions.TopLeft;
            tmpText.overflowMode = TextOverflowModes.Overflow;
            tmpText.enableWordWrapping = false;
            tmpText.text = name;
            
            // TMP 기본 폰트 사용
            tmpText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (tmpText.font == null)
            {
                // TMP 기본 폰트가 없으면 첫 번째로 찾은 폰트 사용
                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (fonts.Length > 0)
                {
                    tmpText.font = fonts[0];
                }
            }
            
            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        [MenuItem("Tools/Network/Remove Profiler UI")]
        public static void RemoveProfilerUI()
        {
            var profilerPanel = GameObject.Find("NetworkProfilerPanel");
            if (profilerPanel != null)
            {
                Object.DestroyImmediate(profilerPanel);
                Debug.Log("[NetworkProfilerSetup] Profiler UI removed");
                EditorUtility.DisplayDialog("Removed", "Network Profiler UI has been removed.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Not Found", "Network Profiler UI not found in scene.", "OK");
            }
        }
    }
}
#endif