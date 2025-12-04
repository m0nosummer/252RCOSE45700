using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace Arena.Editor
{
    /// <summary>
    /// Automatically generates Network Test UI with TextMeshPro.
    /// Menu: Arena → Generate Network Test UI
    /// </summary>
    public static class NetworkUIGenerator
    {
        private static TMP_FontAsset defaultFont;

        [MenuItem("Arena/Generate Network Test UI")]
        public static void GenerateNetworkTestUI()
        {
            // TMP 기본 폰트 로드
            LoadDefaultFont();

            // 1. Canvas 생성
            GameObject canvasObj = new GameObject("NetworkTestUI");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasObj.AddComponent<GraphicRaycaster>();

            // 2. EventSystem 생성 (없으면)
            if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // 3. Panel 배경 (선택사항)
            CreatePanel(canvasObj.transform);

            // 4. Buttons 생성
            Button serverButton = CreateButton(canvasObj.transform, "ServerButton", "Start Server", -300, -400);
            Button clientButton = CreateButton(canvasObj.transform, "ClientButton", "Connect to Server", 300, -400);
            Button sendButton = CreateButton(canvasObj.transform, "SendMessageButton", "Send Message", 0, -400);

            // 5. Status Text 생성
            TextMeshProUGUI statusText = CreateText(canvasObj.transform, "StatusText", "Status: Ready", 0, 400, 1000, 50);
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.fontSize = 24;
            statusText.color = Color.white;

            // 6. Log Text 생성 (스크롤 가능)
            GameObject logPanel = CreateScrollView(canvasObj.transform);
            TextMeshProUGUI logText = logPanel.GetComponentInChildren<TextMeshProUGUI>();

            // 7. Input Field 생성
            TMP_InputField messageInput = CreateInputField(canvasObj.transform, "MessageInput", "Type message...", 0, -300);

            // 8. NetworkTestManager 찾아서 연결
            var testManager = GameObject.FindObjectOfType<Arena.Network.NetworkTestManager>();
            if (testManager != null)
            {
                SerializedObject so = new SerializedObject(testManager);
                
                so.FindProperty("serverButton").objectReferenceValue = serverButton;
                so.FindProperty("clientButton").objectReferenceValue = clientButton;
                so.FindProperty("sendMessageButton").objectReferenceValue = sendButton;
                so.FindProperty("statusText").objectReferenceValue = statusText;
                so.FindProperty("logText").objectReferenceValue = logText;
                so.FindProperty("messageInput").objectReferenceValue = messageInput;
                
                so.ApplyModifiedProperties();
                
                Debug.Log("[NetworkUIGenerator] ✅ UI generated and connected to NetworkTestManager!");
            }
            else
            {
                Debug.LogWarning("[NetworkUIGenerator] NetworkTestManager not found. Please assign references manually.");
            }

            // 선택
            Selection.activeGameObject = canvasObj;
            
            Debug.Log("[NetworkUIGenerator] ✅ Network Test UI (TMP) generated successfully!");
        }

        // ==================== Helper Methods ====================

        private static void LoadDefaultFont()
        {
            // TMP 기본 폰트 로드 (LiberationSans SDF)
            defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            
            if (defaultFont == null)
            {
                // TMP Essentials가 없으면 경고
                Debug.LogWarning("[NetworkUIGenerator] TMP default font not found. Importing TMP Essentials...");
                // TMP Essentials 자동 import는 불가능하므로 사용자에게 알림
            }
        }

        private static void CreatePanel(Transform parent)
        {
            GameObject panel = new GameObject("Background");
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        }

        private static Button CreateButton(Transform parent, string name, string text, float x, float y)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent, false);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(220, 60);

            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.3f, 0.5f, 1f);

            Button button = buttonObj.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.3f, 0.5f, 1f);
            colors.highlightedColor = new Color(0.3f, 0.4f, 0.6f, 1f);
            colors.pressedColor = new Color(0.15f, 0.25f, 0.45f, 1f);
            colors.selectedColor = new Color(0.25f, 0.35f, 0.55f, 1f);
            button.colors = colors;

            // TextMeshPro Text
            GameObject textObj = new GameObject("Text (TMP)");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.font = defaultFont;
            textComp.fontSize = 18;
            textComp.alignment = TextAlignmentOptions.Center;
            textComp.color = Color.white;

            return button;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float x, float y, float width, float height)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);

            TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.font = defaultFont;
            textComp.fontSize = 20;
            textComp.alignment = TextAlignmentOptions.Center;
            textComp.color = Color.white;

            return textComp;
        }

        private static GameObject CreateScrollView(Transform parent)
        {
            // Scroll View Container
            GameObject scrollViewObj = new GameObject("LogScrollView");
            scrollViewObj.transform.SetParent(parent, false);

            RectTransform scrollRect = scrollViewObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.05f, 0.2f);
            scrollRect.anchorMax = new Vector2(0.95f, 0.75f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            Image scrollBg = scrollViewObj.AddComponent<Image>();
            scrollBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            ScrollRect scroll = scrollViewObj.AddComponent<ScrollRect>();
            scroll.vertical = true;
            scroll.horizontal = false;

            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollViewObj.transform, false);

            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = new Vector2(-20, 0); // 스크롤바 공간
            viewportRect.pivot = new Vector2(0, 1);

            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Content
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 2000);

            // Log Text (TMP)
            GameObject logText = new GameObject("LogText (TMP)");
            logText.transform.SetParent(content.transform, false);

            RectTransform logRect = logText.AddComponent<RectTransform>();
            logRect.anchorMin = new Vector2(0, 0);
            logRect.anchorMax = new Vector2(1, 1);
            logRect.sizeDelta = Vector2.zero;
            logRect.offsetMin = new Vector2(10, 10);
            logRect.offsetMax = new Vector2(-10, -10);

            TextMeshProUGUI tmp = logText.AddComponent<TextMeshProUGUI>();
            tmp.text = "=== Network Logs ===\nWaiting for connection...\n";
            tmp.font = defaultFont;
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Scrollbar
            GameObject scrollbar = new GameObject("Scrollbar Vertical");
            scrollbar.transform.SetParent(scrollViewObj.transform, false);

            RectTransform scrollbarRect = scrollbar.AddComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = new Vector2(1, 1);
            scrollbarRect.pivot = new Vector2(1, 0.5f);
            scrollbarRect.sizeDelta = new Vector2(20, 0);

            Image scrollbarBg = scrollbar.AddComponent<Image>();
            scrollbarBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            Scrollbar scrollbarComp = scrollbar.AddComponent<Scrollbar>();
            scrollbarComp.direction = Scrollbar.Direction.BottomToTop;

            // Sliding Area
            GameObject slidingArea = new GameObject("Sliding Area");
            slidingArea.transform.SetParent(scrollbar.transform, false);

            RectTransform slidingRect = slidingArea.AddComponent<RectTransform>();
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.sizeDelta = new Vector2(-20, -20);

            // Handle
            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(slidingArea.transform, false);

            RectTransform handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 20);

            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.4f, 0.5f, 0.6f, 1f);

            scrollbarComp.targetGraphic = handleImage;
            scrollbarComp.handleRect = handleRect;

            // Connect ScrollRect
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.verticalScrollbar = scrollbarComp;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.verticalScrollbarSpacing = -3;

            return scrollViewObj;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, string placeholder, float x, float y)
        {
            GameObject inputObj = new GameObject(name);
            inputObj.transform.SetParent(parent, false);

            RectTransform rect = inputObj.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(600, 60);

            Image bg = inputObj.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            TMP_InputField input = inputObj.AddComponent<TMP_InputField>();

            // Text Area
            GameObject textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputObj.transform, false);

            RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.sizeDelta = new Vector2(-20, -20);

            RectMask2D mask = textArea.AddComponent<RectMask2D>();

            // Placeholder
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(textArea.transform, false);

            RectTransform phRect = placeholderObj.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI phText = placeholderObj.AddComponent<TextMeshProUGUI>();
            phText.text = placeholder;
            phText.font = defaultFont;
            phText.fontSize = 16;
            phText.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            phText.fontStyle = FontStyles.Italic;
            phText.alignment = TextAlignmentOptions.MidlineLeft;

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(textArea.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "";
            text.font = defaultFont;
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            input.textViewport = textAreaRect;
            input.textComponent = text;
            input.placeholder = phText;

            return input;
        }
    }
}