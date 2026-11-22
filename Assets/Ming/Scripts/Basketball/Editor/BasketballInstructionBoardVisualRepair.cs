using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Repairs the ORIGINAL Ming basketball board prefab and any instances in open scenes.
/// Does not rebuild the board, replace its runtime script, or touch XR settings.
/// All existing GameObjects, file IDs, Button listeners, and layout transforms survive.
/// </summary>
public static class BasketballInstructionBoardVisualRepair
{
    private const string TextureFolder = "Assets/Ming/Textures";
    private const string PrefabSearchFolder = "Assets/Ming";
    private const string BoardRootName = "StreetBasketballInstructions";
    private const string PanelName = "InstructionsPanel";

    private sealed class Sprites
    {
        public Sprite background;
        public Sprite outline;
        public Sprite grip;
        public Sprite walk;
        public Sprite turn;
        public Sprite teleport;
    }

    [MenuItem("Tools/Basketball/Repair Instruction Board Sprites & Corners")]
    public static void RepairFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Basketball Board Repair] Exit Play Mode first.");
            return;
        }

        Sprites sprites = LoadAllSprites();
        if (sprites == null)
        {
            Debug.LogError("[Basketball Board Repair] Missing or invalid textures. " +
                "Copy the ZIP's Assets/Ming/Textures folder into the project first.");
            return;
        }

        int prefabCount = 0;
        string[] matches = AssetDatabase.FindAssets("StreetBasketballInstructions t:Prefab",
            new[] { PrefabSearchFolder });
        foreach (string guid in matches)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(Path.GetFileNameWithoutExtension(path), BoardRootName,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null) continue;
                Transform panel = prefabRoot.transform.Find(PanelName);
                if (panel == null)
                {
                    Debug.LogWarning("[Basketball Board Repair] Missing InstructionsPanel: " + path);
                    continue;
                }
                if (!FixPanel(panel, sprites, false)) continue;

                // Saves into the same prefab path, retaining the original GUID and child file IDs.
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                prefabCount++;
                Debug.Log("[Basketball Board Repair] Fixed prefab: " + path);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Basketball Board Repair] Failed on prefab " + path + ": " + ex);
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        int sceneCount = 0;
        // Also fix loaded scene instances explicitly in case old Image.sprite / Image.type
        // values were overridden on the scene instance and hide prefab asset changes.
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || !canvas.gameObject.scene.IsValid()) continue;
            if (!canvas.gameObject.name.StartsWith(BoardRootName, StringComparison.Ordinal)) continue;
            Transform panel = canvas.transform.Find(PanelName);
            if (panel == null) continue;
            try
            {
                if (!FixPanel(panel, sprites, true)) continue;
                EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
                sceneCount++;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Basketball Board Repair] Scene instance failed: " + ex);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Basketball Board Repair] Done. Repaired " + prefabCount +
                  " prefab(s) and " + sceneCount + " loaded scene instance(s). " +
                  "Save the StreetBasketball scene, then check the Game view.");
        if (prefabCount == 0 && sceneCount == 0)
            Debug.LogWarning("[Basketball Board Repair] No compatible board found. " +
                "Check that Assets/Ming/Prefabs/Street Basketball/StreetBasketballInstructions.prefab " +
                "exists and that your basketball scene is open.");
    }

    private static bool FixPanel(Transform panel, Sprites s, bool recordUndo)
    {
        bool good = true;
        good &= FixImage(panel, "OuterPlate", s.background, Image.Type.Sliced, recordUndo);
        good &= FixImage(panel, "OrangeOutline", s.outline, Image.Type.Sliced, recordUndo);
        good &= FixImage(panel, "InnerSurface", s.background, Image.Type.Sliced, recordUndo);
        good &= FixImage(panel, "CloseButton", s.background, Image.Type.Sliced, recordUndo);
        good &= FixImage(panel, "GripIcon", s.grip, Image.Type.Simple, recordUndo);
        good &= FixImage(panel, "RightStickIcon", s.turn, Image.Type.Simple, recordUndo);

        // Renaming in place is safe: original objects and Button.onClick are kept.
        good &= FixControl(panel, "LeftStick", "PhysicalWalking", s.walk,
            "Physical Walking", "Walk to Move", recordUndo);
        good &= FixControl(panel, "AButton", "RightStickUp", s.teleport,
            "Right Stick Up", "Teleport", recordUndo);
        SetText(panel, "GripLabel", "Grip", recordUndo);
        SetText(panel, "GripDescription", "Grab & Throw", recordUndo);
        SetText(panel, "RightStickLabel", "Right Stick L/R", recordUndo);
        SetText(panel, "RightStickDescription", "Turn", recordUndo);
        SetText(panel, "CloseButton/CloseButtonText", "TRIGGER TO CLOSE", recordUndo);
        SetText(panel, "HintText", "Get the ball through the hoop to score.", recordUndo);
        return good;
    }

    private static bool FixControl(Transform panel, string oldName, string newName,
        Sprite sprite, string label, string detail, bool undo)
    {
        Transform icon = FindAlternative(panel, newName + "Icon", oldName + "Icon");
        Transform labelObject = FindAlternative(panel, newName + "Label", oldName + "Label");
        Transform detailObject = FindAlternative(panel, newName + "Description", oldName + "Description");
        if (icon == null || labelObject == null || detailObject == null)
        {
            Debug.LogWarning("[Basketball Board Repair] Missing UI elements for " + newName);
            return false;
        }

        Rename(icon, newName + "Icon", undo);
        Rename(labelObject, newName + "Label", undo);
        Rename(detailObject, newName + "Description", undo);
        bool imageOk = FixImage(panel, newName + "Icon", sprite, Image.Type.Simple, undo);
        SetText(panel, newName + "Label", label, undo);
        SetText(panel, newName + "Description", detail, undo);
        return imageOk;
    }

    private static Transform FindAlternative(Transform panel, string first, string fallback)
    {
        return panel.Find(first) ?? panel.Find(fallback);
    }

    private static bool FixImage(Transform panel, string path, Sprite sprite,
        Image.Type mode, bool undo)
    {
        Transform t = panel.Find(path);
        Image image = t != null ? t.GetComponent<Image>() : null;
        if (image == null)
        {
            Debug.LogWarning("[Basketball Board Repair] Missing Image: " + path);
            return false;
        }
        if (undo) Undo.RecordObject(image, "Repair Basketball Board Sprites");
        image.sprite = sprite;
        image.overrideSprite = null;
        image.type = mode;
        if (mode == Image.Type.Sliced)
            image.fillCenter = true; // transparent center in the outline sprite stays transparent
        else
            image.preserveAspect = true;
        image.SetAllDirty();
        EditorUtility.SetDirty(image);
        if (undo && PrefabUtility.IsPartOfPrefabInstance(image))
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        return true;
    }

    private static void SetText(Transform panel, string path, string value, bool undo)
    {
        Transform t = panel.Find(path);
        TextMeshProUGUI text = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        if (text == null) return;
        if (undo) Undo.RecordObject(text, "Repair Basketball Board Text");
        text.text = value;
        EditorUtility.SetDirty(text);
        if (undo && PrefabUtility.IsPartOfPrefabInstance(text))
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
    }

    private static void Rename(Transform t, string newName, bool undo)
    {
        if (t.name == newName) return;
        if (undo) Undo.RecordObject(t.gameObject, "Repair Basketball Board Names");
        t.name = newName;
        EditorUtility.SetDirty(t.gameObject);
        if (undo && PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
            PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
    }

    private static Sprites LoadAllSprites()
    {
        Sprites sprites = new Sprites
        {
            background = LoadSprite("basketball_card_bg", true),
            outline = LoadSprite("basketball_card_outline", true),
            grip = LoadSprite("basketball_icon_grip", false),
            walk = LoadSprite("basketball_icon_walk", false),
            turn = LoadSprite("basketball_icon_turn", false),
            teleport = LoadSprite("basketball_icon_teleport", false)
        };
        if (sprites.background == null || sprites.outline == null ||
            sprites.grip == null || sprites.walk == null ||
            sprites.turn == null || sprites.teleport == null)
            return null;
        return sprites;
    }

    private static Sprite LoadSprite(string file, bool nineSliced)
    {
        string path = TextureFolder + "/" + file + ".png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[Basketball Board Repair] Texture missing: " + path);
            return null;
        }
        Vector4 border = nineSliced ? new Vector4(27f, 27f, 27f, 27f) : Vector4.zero;
        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single ||
            importer.spriteBorder != border || importer.mipmapEnabled ||
            !importer.alphaIsTransparency ||
            importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null && nineSliced && sprite.border == Vector4.zero)
            Debug.LogWarning("[Basketball Board Repair] Missing 9-slice border: " + path);
        return sprite;
    }
}
