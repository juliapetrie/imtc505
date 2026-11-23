#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Put this file in a folder named "Editor" (e.g. Assets/Editor/GlowOnTouchTools.cs).
/// Select any number of objects in the Hierarchy, then use Tools > Glow On Touch.
/// </summary>
public static class GlowOnTouchTools
{
    [MenuItem("Tools/Glow On Touch/Add To Selected")]
    static void AddToSelected()
    {
        Material glow = FindGlowMaterial();
        if (glow == null)
        {
            EditorUtility.DisplayDialog("Glow On Touch",
                "No material using the Custom/TouchGlow shader was found. Create one first.", "OK");
            return;
        }

        int added = 0, colliders = 0;
        foreach (var go in Selection.gameObjects)
        {
            var glowScript = go.GetComponent<GlowOnTouch>();
            if (glowScript == null)
            {
                glowScript = Undo.AddComponent<GlowOnTouch>(go);
                added++;
            }
            glowScript.glowMaterial = glow;

            // The collider has to be on the same object as the script
            if (go.GetComponent<Collider>() == null)
            {
                if (go.GetComponent<Renderer>() != null)
                {
                    var box = Undo.AddComponent<BoxCollider>(go);
                    box.isTrigger = true;
                    colliders++;
                }
                else
                {
                    Debug.LogWarning($"[GlowOnTouch] '{go.name}' has no Collider and no Renderer of its own. " +
                                     "Add a Collider to it manually.", go);
                }
            }
            EditorUtility.SetDirty(go);
        }
        Debug.Log($"[GlowOnTouch] Added to {added} object(s), created {colliders} trigger collider(s).");
    }

    [MenuItem("Tools/Glow On Touch/Add To Selected", true)]
    static bool AddToSelectedValidate() => Selection.gameObjects.Length > 0;

    [MenuItem("Tools/Glow On Touch/Remove From Selected")]
    static void RemoveFromSelected()
    {
        foreach (var go in Selection.gameObjects)
        {
            var s = go.GetComponent<GlowOnTouch>();
            if (s != null) Undo.DestroyObjectImmediate(s);
        }
    }

    static Material FindGlowMaterial()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Material"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null && m.shader != null && m.shader.name == "Custom/TouchGlow") return m;
        }
        return null;
    }
}
#endif
