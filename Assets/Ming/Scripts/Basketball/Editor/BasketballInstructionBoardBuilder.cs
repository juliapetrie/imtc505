using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates a fully editable Unity UI prefab from bundled textures.
/// Imports missing prefab automatically. The menu command patches existing
/// prefabs in place, preserving references and interaction listeners.
/// </summary>
[InitializeOnLoad]
public static class BasketballInstructionBoardBuilder
{
    private const string PrefabName = "StreetBasketballInstructions.prefab";

    private static readonly Color DarkOuter = Html("#263138");
    private static readonly Color DarkInside = Html("#111B22");
    private static readonly Color Orange = Html("#E9924B");
    private static readonly Color OrangeHighlight = Html("#FFAF6A");
    private static readonly Color Pale = Html("#E1E8ED");
    private static readonly Color Muted = Html("#AABCC8");
    private static readonly Color Rule = Html("#54636C");

    static BasketballInstructionBoardBuilder()
    {
        // The generated prefab appears automatically after copying the folder into Assets.
        EditorApplication.delayCall += AutoGenerateIfMissing;
    }

    [MenuItem("Tools/Basketball/Generate Instruction Board Prefab")]
    public static void GenerateFromMenu()
    {
        Generate(true);
    }

    private static void AutoGenerateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        string baseFolder = LocateRootFolder();
        if (string.IsNullOrEmpty(baseFolder))
            return;

        string prefabPath = baseFolder + "/Prefabs/" + PrefabName;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            Generate(false);
    }

    private static string LocateRootFolder()
    {
        // Allows the files to be stored anywhere under Assets, not only at a fixed path.
        string[] matches = AssetDatabase.FindAssets("basketball_card_bg t:Texture2D");
        foreach (string guid in matches)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.EndsWith("/basketball_card_bg.png", StringComparison.OrdinalIgnoreCase))
                continue;
            return Path.GetDirectoryName(Path.GetDirectoryName(p)).Replace('\\', '/');
        }
        return null;
    }

    private static void Generate(bool overwrite)
    {
        string rootPath = LocateRootFolder();
        if (string.IsNullOrEmpty(rootPath))
        {
            Debug.LogError("[Basketball UI] Missing Textures/basketball_card_bg.png. Reimport the complete folder.");
            return;
        }

        string destinationFolder = rootPath + "/Prefabs";
        if (!AssetDatabase.IsValidFolder(destinationFolder))
            AssetDatabase.CreateFolder(rootPath, "Prefabs");

        string output = destinationFolder + "/" + PrefabName;
        // For an existing prefab, edit its contents in place instead of rebuilding it.
        // This preserves child file IDs, scene references and the Button.onClick listener.
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(output);
        if (existing != null)
        {
            if (overwrite)
                UpdateExistingPrefab(rootPath, output);
            return;
        }

        // Sliced assets preserve corner radii when the board is resized.
        Sprite solid = LoadSprite(rootPath, "basketball_card_bg", true);
        Sprite outline = LoadSprite(rootPath, "basketball_card_outline", true);
        Sprite grip = LoadSprite(rootPath, "basketball_icon_grip", false);
        Sprite walk = LoadSprite(rootPath, "basketball_icon_walk", false);
        Sprite turn = LoadSprite(rootPath, "basketball_icon_turn", false);
        Sprite teleport = LoadSprite(rootPath, "basketball_icon_teleport", false);
        if (solid == null || outline == null || grip == null || walk == null || turn == null || teleport == null)
        {
            Debug.LogError("[Basketball UI] One or more sprite textures could not be loaded.");
            return;
        }

        TMP_FontAsset font = FindFont();
        if (font == null)
        {
            Debug.LogError("[Basketball UI] TextMeshPro font unavailable. Import TMP Essential Resources first.");
            return;
        }

        GameObject root = null;
        try
        {
            root = new GameObject("StreetBasketballInstructions", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler));
            SetLayer(root);

            RectTransform canvasRect = root.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(600, 400);
            canvasRect.localScale = Vector3.one * 0.002f;

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            scaler.referencePixelsPerUnit = 100f;

            // XRI raycaster is added if installed, without a hard compile-time dependency.
            Type xrRaycaster = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                xrRaycaster = assembly.GetType(
                    "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster", false);
                if (xrRaycaster != null) break;
            }
            if (xrRaycaster != null)
                root.AddComponent(xrRaycaster);
            else
            {
                root.AddComponent<GraphicRaycaster>();
                Debug.LogWarning("[Basketball UI] XRI Tracked Device Graphic Raycaster not found. Add it to the prefab Canvas after installing XRI.");
            }

            var board = root.AddComponent<BasketballInstructionBoard>();
            GameObject panel = CreateRect("InstructionsPanel", root.transform, 600, 400, 0, 0);

            // Outer dark rounded plate.
            Image plate = CreateImage("OuterPlate", panel.transform, solid,
                DarkOuter, 580, 386, 0, 0, false, true);
            // Inner orange perimeter, with a fully editable stroke sprite.
            CreateImage("OrangeOutline", panel.transform, outline,
                Orange, 552, 359, 0, 0, false, true);
            // Inner content area matches the exact screenshot's dark treatment.
            CreateImage("InnerSurface", panel.transform, solid,
                DarkInside, 544, 351, 0, 0, false, true);

            Text("TitleText", panel.transform, font, "STREET BASKETBALL", 24f,
                Orange, FontStyles.Bold, TextAlignmentOptions.Center,
                530, 35, 0, 131);
            Text("SubtitleText", panel.transform, font, "HOW TO PLAY", 15f,
                Muted, FontStyles.Normal, TextAlignmentOptions.Center,
                530, 27, 0, 96);
            Image rule = CreateImage("Divider", panel.transform, null,
                Rule, 515, 1.5f, 0, 60, false, false);

            Control(panel.transform, font, grip, "Grip", "Grab & Throw", -242, 22);
            Control(panel.transform, font, walk, "Physical Walking", "Walk to Move", 22, 22);
            Control(panel.transform, font, turn, "Right Stick", "Turn", -242, -38, "Right Stick L/R");
            Control(panel.transform, font, teleport, "Right Stick Up", "Teleport", 22, -38);

            Text("HintText", panel.transform, font,
                "Get the ball through the hoop to score.", 16f,
                Pale, FontStyles.Normal, TextAlignmentOptions.Center,
                515, 27, 0, -102);

            GameObject buttonGo = CreateRect("CloseButton", panel.transform, 515, 54, 0, -150);
            Image buttonVisual = buttonGo.AddComponent<Image>();
            buttonVisual.sprite = solid;
            buttonVisual.type = Image.Type.Sliced;
            buttonVisual.color = Orange;
            buttonVisual.raycastTarget = true;
            Button button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonVisual;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1.11f, 1.12f, 1f);
            colors.pressedColor = new Color(0.83f, 0.83f, 0.83f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            Text("CloseButtonText", buttonGo.transform, font, "TRIGGER TO CLOSE", 20f,
                DarkInside, FontStyles.Bold, TextAlignmentOptions.Center,
                490, 45, 0, 0);

            // Persistent callback lives inside prefab; works after scene insertion.
            UnityEventTools.AddPersistentListener(button.onClick, board.CloseBoard);

            SerializedObject boardSerialized = new SerializedObject(board);
            boardSerialized.FindProperty("panel").objectReferenceValue = panel;
            boardSerialized.FindProperty("worldCanvas").objectReferenceValue = canvas;
            boardSerialized.ApplyModifiedPropertiesWithoutUndo();

            // save native prefab with editable Image, TMP_Text, Button objects
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, output);
            if (saved == null)
                Debug.LogError("[Basketball UI] Could not save prefab at " + output);
            else
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Basketball UI] Ready: " + output + ". Drag the prefab into your StreetBasketball scene.");
                if (overwrite)
                {
                    Selection.activeObject = saved;
                    EditorGUIUtility.PingObject(saved);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[Basketball UI] Prefab generation failed: " + e);
        }
        finally
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// Update existing UI children in place: retain their serialized IDs and button events.
    /// LeftStick is converted into PhysicalWalking; AButton or RightStickUp
    /// becomes the Teleport control. No scene or input actions are modified.
    /// </summary>
    private static void UpdateExistingPrefab(string rootPath, string prefabPath)
    {
        Sprite walkSprite = LoadSprite(rootPath, "basketball_icon_walk", false);
        Sprite teleportSprite = LoadSprite(rootPath, "basketball_icon_teleport", false);
        if (walkSprite == null || teleportSprite == null)
        {
            Debug.LogError("[Basketball UI] Missing walk or teleport icon texture.");
            return;
        }

        GameObject prefabRoot = null;
        try
        {
            prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            Transform panel = prefabRoot.transform.Find("InstructionsPanel");
            if (panel == null)
            {
                Debug.LogError("[Basketball UI] InstructionsPanel not found. Existing prefab was not changed.");
                return;
            }

            // Validate child objects before saving anything; the game scene's existing
            // CloseButton, callback, and RectTransforms are left untouched.
            if (!UpdateControl(panel, "PhysicalWalking", "LeftStick",
                walkSprite, "Physical Walking", "Walk to Move") ||
                !UpdateControl(panel, "RightStickUp", "AButton",
                teleportSprite, "Right Stick Up", "Teleport"))
            {
                Debug.LogError("[Basketball UI] One or more expected control objects are missing. " +
                    "Please inspect the existing prefab before regenerating it.");
                return;
            }

            SetControlText(panel, "Grip", "Grip", "Grab & Throw");
            SetControlText(panel, "RightStick", "Right Stick L/R", "Turn");
            SetControlText(panel, "PhysicalWalking", "Physical Walking", "Walk to Move");
            SetControlText(panel, "RightStickUp", "Right Stick Up", "Teleport");

            Transform closeTextTransform = panel.Find("CloseButton/CloseButtonText");
            if (closeTextTransform != null &&
                closeTextTransform.TryGetComponent(out TextMeshProUGUI closeText))
            {
                closeText.text = "TRIGGER TO CLOSE";
                closeText.fontSize = 20f;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Basketball UI] Updated prefab in place: " + prefabPath +
                ". No player inputs or controller configuration was changed.");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
        catch (Exception ex)
        {
            Debug.LogError("[Basketball UI] Failed to update existing prefab: " + ex);
        }
        finally
        {
            if (prefabRoot != null)
                PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool UpdateControl(Transform panel, string newId, string previousId,
        Sprite sprite, string label, string explanation)
    {
        Transform icon = panel.Find(newId + "Icon") ?? panel.Find(previousId + "Icon");
        Transform labelObject = panel.Find(newId + "Label") ?? panel.Find(previousId + "Label");
        Transform descriptionObject = panel.Find(newId + "Description") ?? panel.Find(previousId + "Description");
        if (icon == null || labelObject == null || descriptionObject == null)
            return false;

        Image iconImage = icon.GetComponent<Image>();
        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI descText = descriptionObject.GetComponent<TextMeshProUGUI>();
        if (iconImage == null || labelText == null || descText == null)
            return false;

        icon.name = newId + "Icon";
        labelObject.name = newId + "Label";
        descriptionObject.name = newId + "Description";
        iconImage.sprite = sprite;
        labelText.text = label;
        descText.text = explanation;
        return true;
    }

    private static void SetControlText(Transform panel, string id, string label, string explanation)
    {
        Transform labelObject = panel.Find(id + "Label");
        Transform explanationObject = panel.Find(id + "Description");
        if (labelObject != null && labelObject.TryGetComponent(out TextMeshProUGUI labelText))
            labelText.text = label;
        if (explanationObject != null && explanationObject.TryGetComponent(out TextMeshProUGUI explanationText))
            explanationText.text = explanation;
    }

    private static GameObject CreateRect(string name, Transform parent, float width,
        float height, float x, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        SetLayer(go);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(x, y);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        return go;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite,
        Color color, float width, float height, float x, float y, bool raycast, bool sliced)
    {
        GameObject go = CreateRect(name, parent, width, height, x, y);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font,
        string content, float fontSize, Color color, FontStyles style, TextAlignmentOptions alignment,
        float width, float height, float x, float y)
    {
        GameObject go = CreateRect(name, parent, width, height, x, y);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = content;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private static void Control(Transform parent, TMP_FontAsset font, Sprite icon,
        string name, string explanation, float x, float y, string labelOverride = null)
    {
        CreateImage(name.Replace(" ", "") + "Icon", parent, icon,
            Orange, 23, 23, x, y - 3, false, false);
        Text(name.Replace(" ", "") + "Label", parent, font,
            labelOverride ?? name, 19f,
            Pale, FontStyles.Bold, TextAlignmentOptions.Left, 218, 27, x + 144, y + 5);
        Text(name.Replace(" ", "") + "Description", parent, font, explanation, 16f,
            Muted, FontStyles.Normal, TextAlignmentOptions.Left, 218, 25, x + 144, y - 17);
    }

    private static Sprite LoadSprite(string rootPath, string assetName, bool sliced)
    {
        string p = rootPath + "/Textures/" + assetName + ".png";
        var textureImporter = AssetImporter.GetAtPath(p) as TextureImporter;
        if (textureImporter == null) return null;

        Vector4 desiredBorder = sliced ? new Vector4(27, 27, 27, 27) : Vector4.zero;
        if (textureImporter.textureType != TextureImporterType.Sprite ||
            textureImporter.spriteImportMode != SpriteImportMode.Single ||
            textureImporter.spriteBorder != desiredBorder || textureImporter.mipmapEnabled)
        {
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Single;
            textureImporter.spriteBorder = desiredBorder;
            textureImporter.mipmapEnabled = false;
            textureImporter.alphaIsTransparency = true;
            textureImporter.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(p);
    }

    private static TMP_FontAsset FindFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;
        string[] ids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
        if (ids.Length == 0)
            ids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (string id in ids)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(id));
            if (font != null) return font;
        }
        return null;
    }

    private static void SetLayer(GameObject go)
    {
        int ui = LayerMask.NameToLayer("UI");
        if (ui >= 0) go.layer = ui;
    }

    private static Color Html(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color color)) return color;
        return Color.white;
    }
}
