using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Afterglow.Tutorial;

namespace Afterglow.Tutorial.Editor
{
    public static class AfterglowWelcomeBuilder
    {
        private const string Root = "Assets/Ming/Generated";
        private const string PrefabPath = Root + "/Welcome Page.prefab";
        private const string SpritePath = Root + "/Rounded Button.png";
        private const string PageName = "Welcome Page";
        private const string Menu = "Tools/Afterglow Tutorial/";
        private static readonly Color32 Background = new Color32(25, 24, 32, 255);
        private static readonly Color32 Gold = new Color32(197, 160, 121, 255);
        private static readonly Color32 Accent = new Color32(186, 149, 105, 255);
        private static readonly Color32 TitleColor = new Color32(246, 238, 228, 255);
        private static readonly Color32 BodyColor = new Color32(209, 203, 195, 255);
        private static readonly Color32 HintColor = new Color32(167, 161, 156, 255);
        private static readonly Color32 Ink = new Color32(32, 27, 25, 255);

        [MenuItem(Menu + "1. Create Welcome Prefab")]
        public static void CreatePrefabMenu()
        {
            var prefab = GetOrCreatePrefab(null);
            if (prefab == null) return;
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("Afterglow: Welcome prefab ready at " + PrefabPath);
        }

        [MenuItem(Menu + "2. Install Welcome on Selected Screen")]
        public static void Install()
        {
            var selected = Selection.activeGameObject;
            if (selected == null || EditorUtility.IsPersistent(selected) || !selected.scene.IsValid() || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog("Afterglow", "Open the Tutorial scene (not Prefab Mode) and select Tutorial Info Screen in the Hierarchy.", "OK");
                return;
            }
            var canvas = selected.GetComponent<Canvas>();
            if (selected.name != "Tutorial Info Screen" || canvas == null || canvas.renderMode != RenderMode.WorldSpace)
            {
                EditorUtility.DisplayDialog("Afterglow", "Select the existing Tutorial Info Screen World Space Canvas in the Hierarchy, not Tutorial Canvas.", "OK");
                return;
            }
            var rect = (RectTransform)canvas.transform;
            if (Mathf.Abs(rect.rect.width - 900) > 1 || Mathf.Abs(rect.rect.height - 340) > 1)
            {
                if (!EditorUtility.DisplayDialog("Screen size differs", "The layout is designed for 900 x 340. Continue without changing your screen size? Inspect the result afterward.", "Continue", "Cancel")) return;
            }
            var old = rect.Find(PageName);
            if (old != null)
            {
                Selection.activeGameObject = old.gameObject;
                Debug.Log("Afterglow: Welcome Page already exists. Nothing overwritten.", old);
                return;
            }
            var heading = rect.Find("Heading");
            var existingFont = heading != null ? heading.GetComponent<TMP_Text>() : null;
            var prefab = GetOrCreatePrefab(existingFont != null ? existingFont.font : null);
            if (prefab == null) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install Afterglow Welcome Page");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rect);
            Undo.RegisterCreatedObjectUndo(instance, "Add Welcome Page");
            var page = (RectTransform)instance.transform;
            Undo.RecordObject(page, "Fit Welcome Page");
            Stretch(page);
            page.SetAsLastSibling();
            PrefabUtility.RecordPrefabInstancePropertyModifications(page);
            foreach (string name in new[] { "Heading", "Body Text", "Amber Trim" })
            {
                var child = rect.Find(name);
                if (child == null) continue;
                Undo.RecordObject(child.gameObject, "Hide placeholder " + name);
                child.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            }
            // Keep the original screen backing and frame. Match its color to the new page.
            var backing = selected.GetComponent<Image>();
            if (backing != null)
            {
                Undo.RecordObject(backing, "Match screen background");
                backing.color = Background;
                backing.raycastTarget = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(backing);
            }
            ConfigureRaycaster(canvas);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(selected.scene);
            Selection.activeGameObject = instance;
            Debug.Log("Afterglow: Welcome installed as a scene override. Save the scene (Ctrl+S); do not Apply All to the lobby prefab.", instance);
        }

        private static GameObject GetOrCreatePrefab(TMP_FontAsset preferredFont)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            var font = preferredFont != null ? preferredFont : TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                // Same default TMP font referenced by the uploaded lobby.
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("8f586378b4e144a9851e7b34d9b748ee"));
            }
            if (font == null)
            {
                EditorUtility.DisplayDialog("TextMeshPro font missing", "Import TMP Essential Resources (Window > TextMeshPro > Import TMP Essential Resources), then retry.", "OK");
                return null;
            }
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var sprite = GetOrCreateRoundedSprite();
            if (sprite == null) { Debug.LogError("Afterglow: rounded button sprite could not be imported."); return null; }
            var root = new GameObject(PageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AfterglowWelcomePage));
            try
            {
                root.layer = 5;
                var panel = (RectTransform)root.transform;
                panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
                panel.sizeDelta = Vector2.zero; panel.anchoredPosition = Vector2.zero;
                var bg = root.GetComponent<Image>(); bg.color = Background; bg.raycastTarget = false;

                // Normalized y anchors allow the layout to fill the existing 900 x 340 screen.
                Label(panel, "University", "UBC OKANAGAN", font, 16, Gold, FontStyles.Normal, 0.90f, 25, 520);
                Label(panel, "Title", "AFTERGLOW", font, 37, TitleColor, FontStyles.Bold, 0.75f, 53, 620);
                var line = Child(panel, "Gold Divider", 0.635f, 64, 2);
                var lineImage = line.gameObject.AddComponent<Image>(); lineImage.color = Accent; lineImage.raycastTarget = false;
                Label(panel, "Welcome Message", "Welcome to your journey.", font, 22, BodyColor, FontStyles.Normal, 0.535f, 34, 620);

                var buttonRect = Child(panel, "Start Button", 0.265f, 410, 57);
                var image = buttonRect.gameObject.AddComponent<Image>();
                image.sprite = sprite; image.type = Image.Type.Sliced; image.color = Gold; image.raycastTarget = true;
                var button = buttonRect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.ColorTint;
                // Image holds the reference gold color, including in Edit Mode.
                // Button tint provides a distinct hover and press feedback without changing input bindings.
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
                colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.12f;
                button.colors = colors;
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
                Label(buttonRect, "Label", "START", font, 24, Ink, FontStyles.Bold, 0.5f, 50, 400);
                UnityEventTools.AddPersistentListener(button.onClick, root.GetComponent<AfterglowWelcomePage>().StartTutorial);
                Label(panel, "Trigger Hint", "Point and press the trigger.", font, 17, HintColor, FontStyles.Normal, 0.095f, 30, 600);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static RectTransform Child(RectTransform parent, string name, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = 5;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, y);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string name, string value, TMP_FontAsset font,
            float size, Color color, FontStyles style, float y, float height, float width)
        {
            var rect = Child(parent, name, y, width, height);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = size; text.fontStyle = style;
            text.color = color; text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = false; text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
            rect.anchoredPosition3D = Vector3.zero;
        }

        private static Sprite GetOrCreateRoundedSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (existing != null) return existing;
            const int n = 64; const float radius = 9f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float qx = Mathf.Abs(x + .5f - n / 2f) - (n / 2f - radius);
                float qy = Mathf.Abs(y + .5f - n / 2f) - (n / 2f - radius);
                float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(.5f - d)));
            }
            tex.SetPixels32(pixels); tex.Apply();
            File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.spriteBorder = new Vector4(12, 12, 12, 12);
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        private static void ConfigureRaycaster(Canvas canvas)
        {
            // Resolve the XRI type without requiring an assembly reference or hard-coding an XR Origin hierarchy.
            const string typeName = "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster";
            Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName, false)).FirstOrDefault(t => t != null);
            if (type != null && typeof(Component).IsAssignableFrom(type))
            {
                if (canvas.GetComponent(type) == null) Undo.AddComponent(canvas.gameObject, type);
            }
            else Debug.LogWarning("Afterglow: Tracked Device Graphic Raycaster not found. Add it manually after XRI is installed.", canvas);
            var serialized = new SerializedObject(canvas);
            var receives = serialized.FindProperty("m_ReceivesEvents");
            if (receives != null && receives.propertyType == SerializedPropertyType.Boolean && !receives.boolValue)
            {
                receives.boolValue = true; serialized.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
            }
            if (canvas.worldCamera == null)
                Debug.LogWarning("Afterglow: assign the current XR Origin camera to Tutorial Info Screen > Event Camera if needed. Do not use the old standalone Main Camera.", canvas);
            var systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            if (systems.Length != 1) Debug.LogWarning("Afterglow: verify there is exactly one active EventSystem with XR UI Input Module. The installer does not create or replace one.", canvas);
        }
    }
}
