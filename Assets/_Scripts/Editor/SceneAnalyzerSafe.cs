using UnityEngine;
using UnityEditor;
using System.Text;
using System.Reflection;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace Editor
{
    /// <summary>
    /// 씬 구조 및 컴포넌트 분석기 (안전 버전)
    /// </summary>
    public class SceneAnalyzerSafe : EditorWindow
    {
        private Vector2 scrollPosition;
        private string analysisResult = "";
        private bool showComponents = true;
        private bool showFields = false; // 기본적으로 끄기
        private GameObject targetObject;
        
        [MenuItem("EditorScript/Analyze Scene (Safe)")]
        public static void ShowWindow()
        {
            GetWindow<SceneAnalyzerSafe>("Scene Analyzer Safe");
        }
        
        private void OnGUI()
        {
            GUILayout.BeginVertical();
            
            EditorGUILayout.LabelField("Scene Structure Analyzer (Safe)", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // 옵션들
            showComponents = EditorGUILayout.Toggle("Show Components", showComponents);
            showFields = EditorGUILayout.Toggle("Show Component Fields (Risky)", showFields);
            
            EditorGUILayout.Space();
            
            // 특정 오브젝트만 분석
            EditorGUILayout.LabelField("Analyze Specific Object:");
            targetObject = (GameObject)EditorGUILayout.ObjectField(targetObject, typeof(GameObject), true);
            
            EditorGUILayout.Space();
            
            // --- 수정된 부분: 버튼 레이아웃 변경 및 'Analyze Entire Scene' 버튼 추가 ---
            if (GUILayout.Button("Analyze Entire Scene"))
            {
                AnalyzeEntireScene();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Analyze UICanvas"))
            {
                AnalyzeUICanvas();
            }
            
            if (GUILayout.Button("Analyze Selected"))
            {
                if (targetObject != null)
                    AnalyzeSpecificObject(targetObject);
                else if (Selection.activeGameObject != null)
                    AnalyzeSpecificObject(Selection.activeGameObject);
                else
                    EditorUtility.DisplayDialog("Selection Error", "Please select an object in the Hierarchy or assign it to the field above.", "OK");
            }
            EditorGUILayout.EndHorizontal();
            // --- 수정 끝 ---
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Copy to Clipboard"))
            {
                EditorGUIUtility.systemCopyBuffer = analysisResult;
                Debug.Log("Analysis copied to clipboard!");
            }
            
            EditorGUILayout.Space();
            
            // 결과 표시
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea);
            textAreaStyle.wordWrap = false;
            analysisResult = EditorGUILayout.TextArea(analysisResult, textAreaStyle, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            
            GUILayout.EndVertical();
        }

        // --- 추가된 부분: 씬 전체를 분석하는 함수 ---
        private void AnalyzeEntireScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                analysisResult = "No active scene found!";
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== Entire Scene Analysis: '{activeScene.name}' ===");
            sb.AppendLine($"Analysis Time: {System.DateTime.Now}");
            sb.AppendLine();

            GameObject[] rootObjects = activeScene.GetRootGameObjects();

            foreach (GameObject rootObj in rootObjects)
            {
                try
                {
                    AnalyzeGameObject(rootObj, sb, 0);
                    sb.AppendLine(); // 최상위 오브젝트 사이에 한 줄 띄어쓰기
                }
                catch (System.Exception ex)
                {
                    sb.AppendLine($"Error analyzing root object '{rootObj.name}': {ex.Message}");
                    Debug.LogError($"Analysis error: {ex}");
                }
            }
            analysisResult = sb.ToString();
        }
        // --- 추가 끝 ---

        private void AnalyzeUICanvas()
        {
            // (기존 코드와 동일)
            Canvas uiCanvas = FindObjectOfType<Canvas>();
            if (uiCanvas == null)
            {
                analysisResult = "UICanvas not found!";
                return;
            }
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== UICanvas Structure Analysis ===");
            sb.AppendLine($"Analysis Time: {System.DateTime.Now}");
            sb.AppendLine();
            
            try
            {
                AnalyzeGameObject(uiCanvas.gameObject, sb, 0);
            }
            catch (System.Exception ex)
            {
                sb.AppendLine($"Error during analysis: {ex.Message}");
                Debug.LogError($"Analysis error: {ex}");
            }
            
            analysisResult = sb.ToString();
        }
        
        private void AnalyzeSpecificObject(GameObject obj)
        {
            // (기존 코드와 동일)
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== Analysis of '{obj.name}' ===");
            sb.AppendLine($"Analysis Time: {System.DateTime.Now}");
            sb.AppendLine();
            
            try
            {
                AnalyzeGameObject(obj, sb, 0);
            }
            catch (System.Exception ex)
            {
                sb.AppendLine($"Error during analysis: {ex.Message}");
                Debug.LogError($"Analysis error: {ex}");
            }
            
            analysisResult = sb.ToString();
        }
        
        private void AnalyzeGameObject(GameObject obj, StringBuilder sb, int depth)
        {
            // (기존 코드와 동일)
            if (obj == null) return;
            
            string indent = new string(' ', depth * 2);
            // --- 수정된 부분: 최상위 오브젝트 표시를 일관성 있게 변경 ---
            string prefix = depth == 0 ? "■ " : "├─ ";
            
            // 기본 정보
            sb.AppendLine($"{indent}{prefix}{obj.name}" + (obj.activeInHierarchy ? "" : " (inactive)"));
            
            if (showComponents)
            {
                AnalyzeComponents(obj, sb, depth + 1);
            }
            
            // 자식 오브젝트들 재귀 분석
            try
            {
                for (int i = 0; i < obj.transform.childCount; i++)
                {
                    Transform childTransform = obj.transform.GetChild(i);
                    if (childTransform != null && childTransform.gameObject != null)
                    {
                        AnalyzeGameObject(childTransform.gameObject, sb, depth + 1);
                    }
                }
            }
            catch (System.Exception ex)
            {
                sb.AppendLine($"{indent}  [Error analyzing children: {ex.Message}]");
            }
        }
        
        // --- 이하 코드는 수정된 부분 없음 ---
        private void AnalyzeComponents(GameObject obj, StringBuilder sb, int depth)
        {
            if (obj == null) return;
            string indent = new string(' ', depth * 2);
            try
            {
                Component[] components = obj.GetComponents<Component>();
                foreach (Component comp in components)
                {
                    if (comp == null)
                    {
                        sb.AppendLine($"{indent}  • [Missing Component]");
                        continue;
                    }
                    string compType = comp.GetType().Name;
                    if (IsBasicComponent(compType))
                    {
                        sb.AppendLine($"{indent}  • {compType}");
                    }
                    else
                    {
                        sb.AppendLine($"{indent}  ★ {compType}");
                        if (showFields)
                        {
                            AnalyzeComponentFieldsSafe(comp, sb, depth + 1);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                sb.AppendLine($"{indent}  [Error analyzing components: {ex.Message}]");
            }
        }

        private void AnalyzeComponentFieldsSafe(Component comp, StringBuilder sb, int depth)
        {
            if (comp == null) return;
            string indent = new string(' ', depth * 2);
            try
            {
                SerializedObject serializedObject = new SerializedObject(comp);
                SerializedProperty property = serializedObject.GetIterator();
                if (property.NextVisible(true))
                {
                    do
                    {
                        if (property.name.StartsWith("m_")) continue;
                        string valueStr = GetSerializedPropertyValueString(property);
                        sb.AppendLine($"{indent}    - {property.name}: {valueStr}");
                    } while (property.NextVisible(false));
                }
            }
            catch (System.Exception ex)
            {
                sb.AppendLine($"{indent}    [Error reading fields: {ex.Message}]");
            }
        }

        private string GetSerializedPropertyValueString(SerializedProperty property)
        {
            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer: return property.intValue.ToString();
                    case SerializedPropertyType.Boolean: return property.boolValue.ToString();
                    case SerializedPropertyType.Float: return property.floatValue.ToString("F2");
                    case SerializedPropertyType.String: return string.IsNullOrEmpty(property.stringValue) ? "Empty" : $"\"{property.stringValue}\"";
                    case SerializedPropertyType.Color: Color color = property.colorValue; return $"RGBA({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})";
                    case SerializedPropertyType.ObjectReference: return property.objectReferenceValue != null ? $"{property.objectReferenceValue.GetType().Name}({property.objectReferenceValue.name})" : "null";
                    case SerializedPropertyType.LayerMask: return property.intValue.ToString();
                    case SerializedPropertyType.Enum: return property.enumNames[property.enumValueIndex];
                    case SerializedPropertyType.Vector2: Vector2 v2 = property.vector2Value; return $"({v2.x:F1}, {v2.y:F1})";
                    case SerializedPropertyType.Vector3: Vector3 v3 = property.vector3Value; return $"({v3.x:F1}, {v3.y:F1}, {v3.z:F1})";
                    case SerializedPropertyType.Rect: Rect rect = property.rectValue; return $"({rect.x:F1}, {rect.y:F1}, {rect.width:F1}, {rect.height:F1})";
                    case SerializedPropertyType.ArraySize: return $"Array[{property.intValue}]";
                    case SerializedPropertyType.Character: return property.intValue.ToString();
                    case SerializedPropertyType.AnimationCurve: return "AnimationCurve";
                    case SerializedPropertyType.Bounds: return "Bounds";
                    case SerializedPropertyType.Gradient: return "Gradient";
                    default: return property.propertyType.ToString();
                }
            }
            catch { return "Error reading value"; }
        }

        private bool IsBasicComponent(string componentType)
        {
            string[] basicComponents = {
                "Transform", "RectTransform", "CanvasRenderer", "Image", "Text",
                "TextMeshProUGUI", "Button", "Toggle", "Slider", "Dropdown",
                "ScrollRect", "InputField", "TMP_InputField", "TMP_Dropdown",
                "CanvasGroup", "Canvas", "GraphicRaycaster", "ContentSizeFitter",
                "LayoutElement", "HorizontalLayoutGroup", "VerticalLayoutGroup",
                "GridLayoutGroup", "Mask", "RectMask2D", "Scrollbar", "Animator",
                "AudioSource", "ParticleSystem", "Collider2D", "Rigidbody2D",
                "UniversalAdditionalCameraData"
            };
            foreach (string basic in basicComponents)
            {
                if (componentType.Contains(basic)) return true;
            }
            return false;
        }
    }
}