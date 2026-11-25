using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Afterglow.Tutorial;

namespace Afterglow.Tutorial.Editor
{
    public static class AfterglowTutorialFlowBuilder
    {
        private const string Menu = "Tools/Afterglow Tutorial/3. Install Introduction + Stage 2";
        private static readonly Color32 Background = new Color32(25, 24, 32, 255);
        private static readonly Color32 Gold = new Color32(197, 160, 121, 255);
        private static readonly Color32 TitleColor = new Color32(246, 238, 228, 255);
        private static readonly Color32 BodyColor = new Color32(214, 207, 199, 255);
        private static readonly Color32 Ink = new Color32(32, 27, 25, 255);
        private const string ArtRoot = "Assets/Ming/Art";
        private const string GeneratedRoot = "Assets/Ming/Generated";
        private const string RoundedButtonPath = GeneratedRoot + "/Rounded Button.png";
        private const string RepairMenu = "Tools/Afterglow Tutorial/4. Repair Introduction + Stage 2 Sprites";
        private static readonly string[] ArtNames = { "LookAround", "Move", "Turn", "Select", "GrabRelease" };
        private static readonly string[] PageNames =
        {
            "Introduction Page 01", "Introduction Page 02", "Look Around Page",
            "Move Page", "Turn Page", "Select Page", "Grab & Release Page"
        };

        [MenuItem(Menu)]
        public static void Install()
        {
            var selected = Selection.activeGameObject;
            if (selected == null || EditorUtility.IsPersistent(selected) || !selected.scene.IsValid() || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog("Afterglow", "Open the Tutorial scene and select Tutorial Info Screen in the Hierarchy.", "OK");
                return;
            }
            var canvas = selected.GetComponent<Canvas>();
            if (selected.name != "Tutorial Info Screen" || canvas == null || canvas.renderMode != RenderMode.WorldSpace)
            {
                EditorUtility.DisplayDialog("Afterglow", "Select the existing Tutorial Info Screen World Space Canvas.", "OK");
                return;
            }

            EnsureSpriteImportSettings();
            ConfigureRaycaster(canvas);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install Afterglow Introduction + Stage 2");

            var screen = (RectTransform)selected.transform;
            var old = screen.Find("Introduction + Stage 2");
            if (old != null)
            {
                RepairScenePages(old);
                RepairSavedPrefabs();
                ExportPagePrefabs(old);
                EditorSceneManager.MarkSceneDirty(selected.scene);
                Undo.CollapseUndoOperations(group);
                Selection.activeGameObject = old.gameObject;
                Debug.Log("Afterglow: Existing pages repaired and missing prefabs exported without rebuilding the scene.", old);
                return;
            }

            var root = new GameObject("Introduction + Stage 2", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AfterglowTutorialFlowManager));
            root.layer = 5;
            Undo.RegisterCreatedObjectUndo(root, "Create Tutorial Flow");
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(screen, false);
            Stretch(rootRect);
            rootRect.SetAsLastSibling();
            var rootImage = root.GetComponent<Image>();
            rootImage.color = Background;
            rootImage.raycastTarget = false;
            var flow = root.GetComponent<AfterglowTutorialFlowManager>();

            TMP_FontAsset font = ResolveFont(screen);

            // Pages
            var intro1 = CreateIntroPage1(rootRect, font);
            var intro2 = CreateIntroPage2(rootRect, font);
                        RectTransform unused;
            var look = CreateIllustrationPage(rootRect, font, "Look Around", LoadSprite("LookAround"), out unused, false);
            var move = CreateIllustrationPage(rootRect, font, "Move", LoadSprite("Move"), out unused, false);
            var turn = CreateIllustrationPage(rootRect, font, "Turn", LoadSprite("Turn"), out unused, false);
            var select = CreateIllustrationPage(rootRect, font, "Select", LoadSprite("Select"), out var selectOverlay, true);
            var grab = CreateIllustrationPage(rootRect, font, "Grab & Release", LoadSprite("GrabRelease"), out unused, false);

            // Practice Objects
            Transform roomRoot = screen.parent != null ? screen.parent : screen;
            var practiceRoot = new GameObject("Tutorial Practice", typeof(Transform));
            Undo.RegisterCreatedObjectUndo(practiceRoot, "Create Tutorial Practice");
            practiceRoot.transform.SetParent(roomRoot, false);
            practiceRoot.transform.localPosition = Vector3.zero;
            practiceRoot.transform.localRotation = Quaternion.identity;
            practiceRoot.transform.localScale = Vector3.one;

            var lookLeft = CreateMarker(practiceRoot.transform, "Look Left Target", new Vector3(-2.35f, 1.55f, -1.6f), 0.14f);
            var lookRight = CreateMarker(practiceRoot.transform, "Look Right Target", new Vector3(2.35f, 1.55f, -1.6f), 0.14f);
            var moveRing = CreateRing(practiceRoot.transform, "Move Target", new Vector3(0f, 0.05f, -0.8f), 0.55f);
            var turnLeft = CreateMarker(practiceRoot.transform, "Turn Left Target", new Vector3(-2.1f, 1.2f, -0.9f), 0.18f);
            var turnRight = CreateMarker(practiceRoot.transform, "Turn Right Target", new Vector3(2.1f, 1.2f, -0.9f), 0.18f);
            var grabSource = CreatePedestal(practiceRoot.transform, "Grab Source Pedestal", new Vector3(-1.05f, 0f, -0.45f));
            var grabCube = CreateCube(practiceRoot.transform, "Grab Cube", new Vector3(-1.05f, 0.98f, -0.45f));
            var grabTarget = CreatePedestal(practiceRoot.transform, "Grab Target Pedestal", new Vector3(1.15f, 0f, -0.45f));
            var grabTargetZone = CreateTargetZone(practiceRoot.transform, new Vector3(1.15f, 1.0f, -0.45f));

            // Add a simple title label under practice root for easy discovery in Hierarchy.
            practiceRoot.SetActive(true);
            lookLeft.SetActive(false);
            lookRight.SetActive(false);
            moveRing.SetActive(false);
            turnLeft.SetActive(false);
            turnRight.SetActive(false);
            grabSource.SetActive(false);
            grabCube.SetActive(false);
            grabTarget.SetActive(false);
            grabTargetZone.SetActive(false);

            // Tasks container
            var tasksRoot = new GameObject("Tutorial Tasks", typeof(Transform));
            Undo.RegisterCreatedObjectUndo(tasksRoot, "Create Tutorial Tasks");
            tasksRoot.transform.SetParent(rootRect, false);
            tasksRoot.SetActive(true);
            var lookTask = Undo.AddComponent<TutorialLookTask>(tasksRoot);
            var moveTask = Undo.AddComponent<TutorialMoveTask>(tasksRoot);
            var turnTask = Undo.AddComponent<TutorialTurnTask>(tasksRoot);
            var selectTask = Undo.AddComponent<TutorialSelectTask>(tasksRoot);
            var grabTask = Undo.AddComponent<TutorialGrabTask>(tasksRoot);

            // Select Target helper on overlay.
            var selectTarget = Undo.AddComponent<TutorialSelectTarget>(selectOverlay.gameObject);
            var selectButton = selectOverlay.GetComponent<Button>();
            if (selectButton != null)
            {
                UnityEventTools.AddPersistentListener(selectButton.onClick, selectTarget.SelectTarget);
            }

            // Bind page buttons to flow through page components.
            BindContinueButtons(intro1, intro2);

            // Configure steps.
            ConfigureSteps(flow, intro1.gameObject, null, intro2.gameObject, null,
                look.gameObject, lookTask,
                move.gameObject, moveTask,
                turn.gameObject, turnTask,
                select.gameObject, selectTask,
                grab.gameObject, grabTask);

            // Configure task serialized fields.
            var xrOrigin = FindXROriginCandidate();
            var camera = xrOrigin != null ? xrOrigin.GetComponentInChildren<Camera>(true) : Camera.main;
            SetObject(lookTask, "cameraTransform", camera != null ? camera.transform : null);
            SetObject(lookTask, "leftTarget", lookLeft.transform);
            SetObject(lookTask, "rightTarget", lookRight.transform);
            SetObject(lookTask, "leftVisual", lookLeft.GetComponent<TutorialVisualPulse>());
            SetObject(lookTask, "rightVisual", lookRight.GetComponent<TutorialVisualPulse>());

            SetObject(moveTask, "playerRoot", xrOrigin != null ? xrOrigin.transform : null);
            SetObject(moveTask, "moveTarget", moveRing.transform);
            SetObject(moveTask, "targetVisual", moveRing.GetComponent<TutorialVisualPulse>());
            SetObject(moveTask, "moveAction", GuessInputActionReference("move"));

            SetObject(turnTask, "playerRoot", xrOrigin != null ? xrOrigin.transform : null);
            SetObject(turnTask, "leftTarget", turnLeft.transform);
            SetObject(turnTask, "rightTarget", turnRight.transform);
            SetObject(turnTask, "leftVisual", turnLeft.GetComponent<TutorialVisualPulse>());
            SetObject(turnTask, "rightVisual", turnRight.GetComponent<TutorialVisualPulse>());
            SetObject(turnTask, "turnAction", GuessInputActionReference("turn"));

            SetObject(selectTask, "target", selectTarget);

            SetObject(grabTask, "grabbable", grabCube.transform);
            SetObject(grabTask, "sourcePedestal", grabSource.transform);
            SetObject(grabTask, "targetZone", grabTargetZone.GetComponent<Collider>());
            SetObject(grabTask, "targetVisual", grabTargetZone.GetComponent<TutorialVisualPulse>());

            // Try to add XR Grab Interactable to the cube if XRI is installed.
            TryAddXrGrabInteractable(grabCube);
            AddPhysics(grabCube);

            // Connect Welcome page if present.
            var welcome = screen.Find("Welcome Page");
            if (welcome != null)
            {
                var welcomePage = welcome.GetComponent<AfterglowWelcomePage>();
                if (welcomePage != null)
                {
                    var so = new SerializedObject(welcomePage);
                    so.FindProperty("nextPage").objectReferenceValue = intro1.gameObject;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    UnityEventTools.AddPersistentListener(welcomePage.OnStartRequested, flow.StartFlow);
                }
            }

            foreach (var page in new[] { intro1.gameObject, intro2.gameObject, look.gameObject, move.gameObject, turn.gameObject, select.gameObject, grab.gameObject })
                page.SetActive(false);

            RepairSavedPrefabs();
            ExportPagePrefabs(rootRect);
            EditorSceneManager.MarkSceneDirty(selected.scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = root;
            Debug.Log("Afterglow: Introduction + Stage 2 installed. Save the scene. If XR input references are blank, assign them in Tutorial Tasks.", root);
        }

        // Save only the UI pages, not scene-specific tasks or XR Origin references.
        // Existing prefab assets are deliberately preserved to protect manual edits.
        private static void ExportPagePrefabs(Transform flowRoot)
        {
            if (!Directory.Exists(GeneratedRoot))
            {
                Directory.CreateDirectory(GeneratedRoot);
                AssetDatabase.Refresh();
            }

            foreach (string pageName in PageNames)
            {
                Transform page = flowRoot.Find(pageName);
                if (page == null)
                {
                    Debug.LogWarning($"Afterglow: Cannot export missing page '{pageName}'.", flowRoot);
                    continue;
                }

                string path = $"{GeneratedRoot}/{pageName}.prefab";
                if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path))
                    continue;

                // Clone so the saved prefab is active, while the installed scene page
                // keeps its current visibility and all task / manager references.
                GameObject copy = UnityEngine.Object.Instantiate(page.gameObject);
                try
                {
                    copy.name = pageName;
                    if (PrefabUtility.IsPartOfPrefabInstance(copy))
                        PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    copy.SetActive(true);
                    var prefab = PrefabUtility.SaveAsPrefabAsset(copy, path, out bool success);
                    if (success && prefab != null)
                        Debug.Log("Afterglow: Page prefab saved at " + path, prefab);
                    else
                        Debug.LogError("Afterglow: Could not save page prefab at " + path, flowRoot);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static TMP_FontAsset ResolveFont(RectTransform screen)
        {
            var heading = screen.Find("Heading");
            var tmp = heading != null ? heading.GetComponent<TMP_Text>() : null;
            if (tmp != null && tmp.font != null) return tmp.font;
            return TMP_Settings.defaultFontAsset;
        }

        private static RectTransform CreateIntroPage1(RectTransform parent, TMP_FontAsset font)
        {
            var root = CreatePageRoot(parent, "Introduction Page 01");
            Label(root, "Kicker", "INTRODUCTION", font, 15, Gold, FontStyles.Normal, 0.86f, 20, 600);
            Label(root, "Title", "Discover Your Campus", font, 30, TitleColor, FontStyles.Bold, 0.73f, 38, 700);
            Label(root, "Line1", "Explore the campus.", font, 22, BodyColor, FontStyles.Normal, 0.55f, 28, 600);
            Label(root, "Line2", "Meet new people.", font, 22, BodyColor, FontStyles.Normal, 0.46f, 28, 600);
            Label(root, "Line3", "Complete each club activity.", font, 22, BodyColor, FontStyles.Normal, 0.37f, 28, 600);
            var btn = CreateMainButton(root, font, "NEXT", 0.15f, 240, 50);
            CreatePageComponent(root, btn.gameObject, true);
            return root;
        }

        private static RectTransform CreateIntroPage2(RectTransform parent, TMP_FontAsset font)
        {
            var root = CreatePageRoot(parent, "Introduction Page 02");
            Label(root, "Kicker", "INTRODUCTION", font, 15, Gold, FontStyles.Normal, 0.88f, 20, 600);
            Label(root, "Title", "You will learn 5 basic controls.", font, 28, TitleColor, FontStyles.Bold, 0.76f, 38, 760);
            Label(root, "Subtitle", "Look, move, turn, select, and grab.", font, 20, BodyColor, FontStyles.Normal, 0.66f, 28, 700);
            var strip = Child(root, "Preview Strip", 0.44f, 740, 120);
            float[] xs = { -280f, -140f, 0f, 140f, 280f };
            string[] names = { "LookAround", "Move", "Turn", "Select", "GrabRelease" };
            for (int i = 0; i < names.Length; i++)
            {
                var tile = Child(strip, names[i], 0.5f, 126, 96);
                tile.anchoredPosition = new Vector2(xs[i], 0f);
                var img = tile.gameObject.AddComponent<Image>();
                img.sprite = LoadSprite(names[i]);
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            var btn = CreateMainButton(root, font, "START CONTROLS", 0.13f, 300, 50);
            CreatePageComponent(root, btn.gameObject, true);
            return root;
        }

        private static RectTransform CreateIllustrationPage(RectTransform parent, TMP_FontAsset font, string pageName, Sprite sprite, out RectTransform overlayButton, bool addSelectOverlay)
        {
            var root = CreatePageRoot(parent, pageName + " Page");
            var art = Child(root, "Illustration", 0.52f, 850, 300);
            var img = art.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            CreatePageComponent(root, null, false);
            overlayButton = null;
            if (addSelectOverlay)
            {
                overlayButton = Child(art, "Select Target Overlay", 0.44f, 150, 120);
                overlayButton.anchorMin = overlayButton.anchorMax = new Vector2(0.73f, 0.43f);
                overlayButton.anchoredPosition = Vector2.zero;
                var bimg = overlayButton.gameObject.AddComponent<Image>();
                bimg.color = new Color(1f, 1f, 1f, 0.001f);
                var button = overlayButton.gameObject.AddComponent<Button>();
                button.targetGraphic = bimg;
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(1f, 1f, 1f, 0.20f);
                colors.disabledColor = new Color(1f, 1f, 1f, 0.05f);
                button.colors = colors;
            }
            return root;
        }

        private static RectTransform CreatePageRoot(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = 5;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            var bg = go.GetComponent<Image>();
            bg.color = Background;
            bg.raycastTarget = false;
            return rect;
        }

        private static void CreatePageComponent(RectTransform page, GameObject button, bool showContinueButton)
        {
            var comp = page.gameObject.AddComponent<AfterglowTutorialPage>();
            var so = new SerializedObject(comp);
            so.FindProperty("showContinueButton").boolValue = showContinueButton;
            so.FindProperty("continueButton").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (button != null)
            {
                var btn = button.GetComponent<Button>();
                if (btn != null) UnityEventTools.AddPersistentListener(btn.onClick, comp.GoNext);
            }
        }

        private static RectTransform CreateMainButton(RectTransform parent, TMP_FontAsset font, string label, float y, float width, float height)
        {
            var buttonRect = Child(parent, label + " Button", y, width, height);
            var image = buttonRect.gameObject.AddComponent<Image>();
            image.color = Gold;
            var roundedSprite = LoadRoundedButton();
            if (roundedSprite != null)
            {
                image.sprite = roundedSprite;
                image.type = Image.Type.Sliced;
            }
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            button.colors = colors;
            Label(buttonRect, "Label", label, font, 21, Ink, FontStyles.Bold, 0.5f, 30, width - 24);
            return buttonRect;
        }

        private static void BindContinueButtons(RectTransform intro1, RectTransform intro2)
        {
            // Buttons are already wired by CreatePageComponent.
        }

        private static void ConfigureSteps(AfterglowTutorialFlowManager flow,
            GameObject intro1, TutorialTask intro1Task,
            GameObject intro2, TutorialTask intro2Task,
            GameObject look, TutorialTask lookTask,
            GameObject move, TutorialTask moveTask,
            GameObject turn, TutorialTask turnTask,
            GameObject select, TutorialTask selectTask,
            GameObject grab, TutorialTask grabTask)
        {
            var so = new SerializedObject(flow);
            var steps = so.FindProperty("steps");
            steps.arraySize = 7;
            SetStep(steps, 0, "Introduction 01", intro1, intro1Task);
            SetStep(steps, 1, "Introduction 02", intro2, intro2Task);
            SetStep(steps, 2, "Look Around", look, lookTask);
            SetStep(steps, 3, "Move", move, moveTask);
            SetStep(steps, 4, "Turn", turn, turnTask);
            SetStep(steps, 5, "Select", select, selectTask);
            SetStep(steps, 6, "Grab & Release", grab, grabTask);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStep(SerializedProperty steps, int index, string name, GameObject page, TutorialTask task)
        {
            var element = steps.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("name").stringValue = name;
            element.FindPropertyRelative("page").objectReferenceValue = page;
            element.FindPropertyRelative("task").objectReferenceValue = task;
        }

        private static void SetObject(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
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
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void ConfigureRaycaster(Canvas canvas)
        {
            const string typeName = "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster";
            Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName, false)).FirstOrDefault(t => t != null);
            if (type != null && typeof(Component).IsAssignableFrom(type) && canvas.GetComponent(type) == null)
            {
                Undo.AddComponent(canvas.gameObject, type);
            }
        }

        // Repair only the intended Image components. Do not rebuild the pages,
        // replace task references, or alter RectTransforms, colors or button events.
        [MenuItem(RepairMenu)]
        public static void RepairExistingSprites()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog("Afterglow", "Exit Play Mode and Prefab Mode before repairing sprites.", "OK");
                return;
            }
            EnsureSpriteImportSettings();
            int saved = RepairSavedPrefabs();
            int scene = 0;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Repair Afterglow tutorial sprites");
            // Include inactive pages and flows in all loaded scenes.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var loadedScene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!loadedScene.isLoaded) continue;
                foreach (var root in loadedScene.GetRootGameObjects())
                {
                    foreach (var page in root.GetComponentsInChildren<AfterglowTutorialPage>(true))
                    {
                        if (Array.IndexOf(PageNames, page.gameObject.name) < 0) continue;
                        if (RepairPageSprites(page.transform, true)) scene++;
                    }
                }
            }
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            Debug.Log($"Afterglow: repaired {saved} saved prefab(s) and {scene} scene page(s). Save the scene. Check Console for missing assets or children.");
        }

        private static void RepairScenePages(Transform flowRoot)
        {
            foreach (string name in PageNames)
            {
                var page = flowRoot.Find(name);
                if (page != null) RepairPageSprites(page, true);
            }
        }

        private static int RepairSavedPrefabs()
        {
            int count = 0;
            foreach (string name in PageNames)
            {
                string path = $"{GeneratedRoot}/{name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (!RepairPageSprites(contents.transform, false)) continue;
                    PrefabUtility.SaveAsPrefabAsset(contents, path, out bool success);
                    if (success) count++;
                    else Debug.LogError("Afterglow: could not save repaired prefab: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            AssetDatabase.SaveAssets();
            return count;
        }

        private static bool RepairPageSprites(Transform page, bool recordUndo)
        {
            bool changed = false;
            if (page.name == "Introduction Page 01" || page.name == "Introduction Page 02")
            {
                string buttonName = page.name == "Introduction Page 01" ? "NEXT Button" : "START CONTROLS Button";
                changed |= RepairImage(page, buttonName, LoadRoundedButton(), Image.Type.Sliced, recordUndo);
                if (page.name == "Introduction Page 02")
                {
                    foreach (string art in ArtNames)
                        changed |= RepairImage(page, "Preview Strip/" + art, LoadSprite(art), Image.Type.Simple, recordUndo);
                }
            }
            else
            {
                int index = Array.IndexOf(PageNames, page.name) - 2;
                if (index >= 0 && index < ArtNames.Length)
                    changed |= RepairImage(page, "Illustration", LoadSprite(ArtNames[index]), Image.Type.Simple, recordUndo);
            }
            return changed;
        }

        private static bool RepairImage(Transform page, string childPath, Sprite sprite, Image.Type type, bool recordUndo)
        {
            if (sprite == null) return false; // Never erase a reference when a source asset is missing.
            var child = page.Find(childPath);
            var img = child != null ? child.GetComponent<Image>() : null;
            if (img == null)
            {
                Debug.LogWarning($"Afterglow: Image not found: {page.name}/{childPath}. If renamed, assign the sprite manually.", page);
                return false;
            }
            if (img.sprite == sprite && img.type == type) return false;
            if (recordUndo) Undo.RecordObject(img, "Repair tutorial sprite");
            img.sprite = sprite;
            img.type = type;
            EditorUtility.SetDirty(img);
            if (recordUndo)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(img);
                if (page.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(page.gameObject.scene);
            }
            return true;
        }

        private static void EnsureSpriteImportSettings()
        {
            foreach (string name in ArtNames)
                EnsureSpriteImporter($"{ArtRoot}/{name}.png", false);
            EnsureSpriteImporter(RoundedButtonPath, true);
        }

        private static void EnsureSpriteImporter(string path, bool rounded)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("Afterglow: texture missing at " + path + (rounded ? ". Generate or move Rounded Button.png into Assets/Ming/Generated." : ""));
                return;
            }
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
            if (rounded)
            {
                // Matches the 64 x 64 rounded sprite produced by the Welcome builder.
                if (importer.spriteBorder != new Vector4(12, 12, 12, 12)) { importer.spriteBorder = new Vector4(12, 12, 12, 12); dirty = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
                if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; dirty = true; }
                if (importer.spritePixelsPerUnit != 100f) { importer.spritePixelsPerUnit = 100f; dirty = true; }
            }
            if (dirty) importer.SaveAndReimport();
        }

        private static Sprite LoadSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/{name}.png");
        }

        private static Sprite LoadRoundedButton()
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedButtonPath);
        }

        private static GameObject CreateMarker(Transform parent, string name, Vector3 localPos, float scale)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(root, "Create Marker");
            root.name = name;
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localScale = Vector3.one * scale;
            var renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            renderer.sharedMaterial.color = new Color(0.78f,0.63f,0.47f,1f);
            var pulse = root.AddComponent<TutorialVisualPulse>();
            var so = new SerializedObject(pulse);
            var arr = so.FindProperty("targetRenderers");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject CreateRing(Transform parent, string name, Vector3 localPos, float diameter)
        {
            var root = new GameObject(name, typeof(Transform));
            Undo.RegisterCreatedObjectUndo(root, "Create Ring");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            root.transform.localScale = Vector3.one;
            for (int i = 0; i < 20; i++)
            {
                float angle = i * Mathf.PI * 2f / 20f;
                var segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                segment.transform.SetParent(root.transform, false);
                segment.transform.localScale = new Vector3(0.03f, 0.01f, 0.03f);
                segment.transform.localPosition = new Vector3(Mathf.Cos(angle) * diameter * 0.5f, Mathf.Sin(angle) * diameter * 0.5f, 0f);
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Rad2Deg * angle);
                var renderer = segment.GetComponent<Renderer>();
                renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            }
            var pulse = root.AddComponent<TutorialVisualPulse>();
            var renderers = root.GetComponentsInChildren<Renderer>();
            var so = new SerializedObject(pulse);
            var arr = so.FindProperty("targetRenderers");
            arr.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject CreatePedestal(Transform parent, string name, Vector3 localPos)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(root, "Create Pedestal");
            root.name = name;
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos + new Vector3(0f, 0.4f, 0f);
            root.transform.localScale = new Vector3(0.8f, 0.8f, 0.6f);
            var renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            renderer.sharedMaterial.color = new Color(0.34f,0.29f,0.24f,1f);
            return root;
        }

        private static GameObject CreateCube(Transform parent, string name, Vector3 localPos)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(cube, "Create Grab Cube");
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
            var renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            renderer.sharedMaterial.color = new Color(0.76f,0.62f,0.42f,1f);
            return cube;
        }

        private static GameObject CreateTargetZone(Transform parent, Vector3 localPos)
        {
            var root = new GameObject("Grab Target Zone", typeof(SphereCollider));
            Undo.RegisterCreatedObjectUndo(root, "Create Target Zone");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            var collider = root.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.22f;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Ring Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(0.28f, 0.01f, 0.28f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            var renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            var pulse = root.AddComponent<TutorialVisualPulse>();
            var so = new SerializedObject(pulse);
            var arr = so.FindProperty("targetRenderers");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void AddPhysics(GameObject cube)
        {
            if (cube.GetComponent<Rigidbody>() == null) cube.AddComponent<Rigidbody>();
        }

        private static void TryAddXrGrabInteractable(GameObject cube)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable", false)).FirstOrDefault(t => t != null)
                ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable", false)).FirstOrDefault(t => t != null);
            if (type != null && cube.GetComponent(type) == null) Undo.AddComponent(cube, type);
        }

        private static GameObject FindXROriginCandidate()
        {
            string[] names = { "XR Origin", "XROrigin", "XR Origin (XR Rig)", "Player", "VR Player" };
            foreach (var n in names)
            {
                var go = GameObject.Find(n);
                if (go != null) return go;
            }
            var camera = Camera.main;
            return camera != null ? camera.transform.root.gameObject : null;
        }

        private static InputActionReference GuessInputActionReference(string keyword)
        {
            string[] guids = AssetDatabase.FindAssets("t:InputActionReference");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
                if (obj == null || obj.action == null || string.IsNullOrEmpty(obj.action.name)) continue;
                string n = obj.action.name.ToLowerInvariant();
                if (n.Contains(keyword)) return obj;
                if (keyword == "turn" && (n.Contains("snap") || n.Contains("rotate"))) return obj;
            }
            return null;
        }
    }
}
