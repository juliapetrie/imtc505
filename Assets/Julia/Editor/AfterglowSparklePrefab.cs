using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Afterglow > Create Sparkle Prefab: saves the star texture and particle material as real assets
/// (so headset builds include the shader instead of rendering pink) and makes
/// Assets/Julia/Prefabs/Afterglow Sparkle.prefab: an empty holder with the sparkle on it.
/// Drop the prefab in any scene and drag objects under it to make them sparkle when touched.
/// </summary>
internal static class AfterglowSparklePrefab
{
    private const string TexturePath = "Assets/Julia/Materials/Afterglow Star.png";
    private const string MaterialPath = "Assets/Julia/Materials/Afterglow Sparkle.mat";
    private const string PrefabPath = "Assets/Julia/Prefabs/Afterglow Sparkle.prefab";

    [MenuItem("Tools/Afterglow/Create Sparkle Prefab")]
    private static void Create()
    {
        // Star texture as a PNG, imported crisp (same settings the runtime texture uses).
        var star = AfterglowSparkle.CreateStarTexture();
        File.WriteAllBytes(TexturePath, star.EncodeToPNG());
        Object.DestroyImmediate(star);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.mipMapBias = -0.6f;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

        // Particle material asset referencing that texture.
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var fresh = AfterglowSparkle.CreateMaterial(texture);
        if (material == null)
        {
            AssetDatabase.CreateAsset(fresh, MaterialPath);
            material = fresh;
        }
        else
        {
            material.shader = fresh.shader;
            material.CopyPropertiesFromMaterial(fresh);
            material.shaderKeywords = fresh.shaderKeywords;
            material.renderQueue = fresh.renderQueue;
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(material);
        }

        // The prefab: an empty holder carrying the sparkle, wired to the material asset.
        var holder = new GameObject("Afterglow Sparkle");
        var sparkle = holder.AddComponent<AfterglowSparkle>();
        var serialized = new SerializedObject(sparkle);
        serialized.FindProperty("materialOverride").objectReferenceValue = material;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var prefab = PrefabUtility.SaveAsPrefabAsset(holder, PrefabPath);
        Object.DestroyImmediate(holder);
        AssetDatabase.SaveAssets();

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"Afterglow: created {PrefabPath} (material {MaterialPath}).");
    }

    [MenuItem("Tools/Afterglow/Create Sparkle Prefab", true)]
    private static bool CanCreate() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
