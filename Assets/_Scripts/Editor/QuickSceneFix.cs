// Assets/_Scripts/Editor/QuickSceneFix.cs (전체 교체)

using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace Arena.Editor
{
    public static class QuickSceneFix
    {
        [MenuItem("Arena/Restore Original Settings")]
        public static void RestoreOriginal()
        {
            // 1. 카메라 복구
            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0, 15, -8);
                camera.transform.rotation = Quaternion.Euler(60, 0, 0);
                camera.orthographic = false;
                camera.fieldOfView = 60;
                
                var cameraFollow = camera.GetComponent<Arena.Gameplay.CameraFollow>();
                if (cameraFollow != null)
                {
                    var offsetField = typeof(Arena.Gameplay.CameraFollow).GetField("offset", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    offsetField?.SetValue(cameraFollow, new Vector3(0, 15, -8));
                }
                
                Debug.Log("✓ Camera restored");
            }
            
            // 2. UI 투명 제거
            var uiCanvas = GameObject.Find("NetworkTestUI");
            if (uiCanvas != null)
            {
                var canvasGroup = uiCanvas.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    Object.DestroyImmediate(canvasGroup);
                    Debug.Log("✓ UI transparency removed");
                }
            }
            
            Debug.Log("=== Restored to Original ===");
        }
        
        [MenuItem("Arena/Just Minimize UI")]
        public static void MinimizeUI()
        {
            var ui = GameObject.Find("NetworkTestUI");
            if (ui != null)
            {
                var rect = ui.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(10, -10);
                rect.sizeDelta = new Vector2(350, 250);
                
                Debug.Log("✓ UI minimized to top-left");
            }
        }
        
        [MenuItem("Arena/Hide UI Completely")]
        public static void HideUI()
        {
            var ui = GameObject.Find("NetworkTestUI");
            if (ui != null)
            {
                ui.SetActive(false);
                Debug.Log("✓ UI hidden (F1 to show in NetworkTestManager)");
            }
        }
    }
}