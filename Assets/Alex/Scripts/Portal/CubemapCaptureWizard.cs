#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Editor tool: captures a 360 view of the open scene from a chosen point and
/// saves it as a cubemap texture you can drag into PortalVisual.
///
/// Put this file in a folder called "Editor" (e.g. Assets/Scripts/Editor).
/// Use it from the menu: Tools > Capture Scene Cubemap...
/// Run it in the Editor (not in Play mode), with the memory scene open.
/// </summary>
public class CubemapCaptureWizard : ScriptableWizard
{
    [Tooltip("An empty object placed where the player would stand. The 360 view is taken from here.")]
    public Transform captureFrom;

    [Tooltip("Size of each cube face. 1024 is plenty for Quest.")]
    public int faceSize = 1024;

    public string assetName = "Memory1_Cubemap";

    [Tooltip("Folder inside Assets where the result is saved.")]
    public string saveFolder = "Assets";

    [MenuItem("Tools/Capture Scene Cubemap...")]
    static void Open()
    {
        var wizard = DisplayWizard<CubemapCaptureWizard>("Capture Scene Cubemap", "Capture");
        if (Selection.activeTransform != null) wizard.captureFrom = Selection.activeTransform;
        wizard.OnWizardUpdate();
    }

    void OnWizardUpdate()
    {
        helpString = "Select an object in the scene where the player would stand, then press Capture.";
        isValid = captureFrom != null && !string.IsNullOrWhiteSpace(assetName) && faceSize >= 64;
    }

    void OnWizardCreate()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Capture Scene Cubemap", "Exit Play mode first.", "OK");
            return;
        }

        var camObject = new GameObject("CubemapCaptureCam") { hideFlags = HideFlags.HideAndDontSave };
        RenderTexture cubeRT = null, equirectRT = null;
        Texture2D readback = null;

        try
        {
            var cam = camObject.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(captureFrom.position, Quaternion.identity);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.cullingMask = ~0;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;

            cubeRT = new RenderTexture(faceSize, faceSize, 24) { dimension = TextureDimension.Cube };
            equirectRT = new RenderTexture(faceSize * 4, faceSize * 2, 0);

            cam.RenderToCubemap(cubeRT);                 // all six faces
            cubeRT.ConvertToEquirect(equirectRT);        // single 2:1 panorama

            var previous = RenderTexture.active;
            RenderTexture.active = equirectRT;
            readback = new Texture2D(equirectRT.width, equirectRT.height, TextureFormat.RGB24, false);
            readback.ReadPixels(new Rect(0, 0, equirectRT.width, equirectRT.height), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(saveFolder);
            string path = $"{saveFolder}/{assetName}.png";
            File.WriteAllBytes(path, readback.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            // Import it as a cubemap, ready to use.
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureShape = TextureImporterShape.TextureCube;
            importer.generateCubemap = TextureImporterGenerateCubemap.Cylindrical; // Latitude-Longitude
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();

            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"Cubemap saved to {path}. Drag it into the Othersidecubemap slot on PortalVisual.");
        }
        finally
        {
            DestroyImmediate(camObject);
            if (cubeRT != null) { cubeRT.Release(); DestroyImmediate(cubeRT); }
            if (equirectRT != null) { equirectRT.Release(); DestroyImmediate(equirectRT); }
            if (readback != null) DestroyImmediate(readback);
        }
    }
}
#endif
