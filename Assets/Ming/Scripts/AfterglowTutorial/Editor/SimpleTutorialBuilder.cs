using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using Afterglow.SimpleTutorial;

namespace Afterglow.SimpleTutorial.Editor
{
    public static class SimpleTutorialBuilder
    {
        // Prefer the project's shared art folder; support the original package layout too.
        static readonly string[] ArtRoots = {
            "Assets/Ming/Art/",
            "Assets/Ming/SimpleTutorial/Art/"
        };
        static readonly string[] RoundedPaths = {
            "Assets/Ming/Generated/Rounded Button.png",
            "Assets/Ming/Art/Rounded Button.png",
            "Assets/Ming/SimpleTutorial/Art/Rounded Button.png"
        };
        const string Output = "Assets/Ming/Generated/Simple Tutorial/";
        const string PrefabPath = Output + "Afterglow Simple Tutorial.prefab";
        const string RootName = "Afterglow Simple Tutorial";
        static readonly Color Background = new Color32(25,24,32,255), Orange = new Color32(255,122,0,255), Hover = new Color32(255,240,182,255), Pressed = new Color32(255,177,0,255), Ink = new Color32(32,27,25,255), Title = new Color32(246,238,228,255), Body = new Color32(224,217,208,255), Hint = new Color32(185,180,175,255);
        static readonly string[] ArtNames = {"LookAround", "Move", "Turn", "Select", "GrabRelease"};
        static readonly string[] Titles = {"Look Around", "Move", "Turn", "Select", "Grab & Release"};
        static readonly string[] Main = {"Turn your head to look around.", "Use the left thumbstick to move.", "Use the right thumbstick to turn.", "Point at a button or object with your controller.", "Hold the grip button to grab an object."};
        static readonly string[] Secondary = {"Look left, right, up, and down.", "Push the stick in the direction you want to go.", "Push left or right to change direction.", "Press the trigger to select it.", "Release the grip button to let go."};
        [Serializable] class Drawing { public Part[] parts; public LabelData[] labels; }
        [Serializable] class Part { public string name,color; public float x0,y0,x1,y1; public Vector2[] vertices; public int[] triangles; }
        [Serializable] class LabelData { public string name,text,color; public float x,y,width,height,size; }

        [MenuItem("Tools/Afterglow Simple Tutorial/1. Build and Install on Selected Screen")]
        public static void Install()
        {
            var selected = Selection.activeGameObject;
            if (EditorApplication.isPlayingOrWillChangePlaymode || selected == null || EditorUtility.IsPersistent(selected) || !selected.scene.IsValid() || PrefabStageUtility.GetCurrentPrefabStage() != null)
            { EditorUtility.DisplayDialog("Afterglow", "Open Tutorial scene outside Play Mode and Prefab Mode. Select Tutorial Info Screen.", "OK"); return; }
            var canvas = selected.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace || selected.name != "Tutorial Info Screen")
            { EditorUtility.DisplayDialog("Afterglow", "Select Tutorial Info Screen, the World Space Canvas.", "OK"); return; }
            var screen = (RectTransform)canvas.transform;
            var installed = screen.Find(RootName);
            if (installed != null) { Selection.activeGameObject = installed.gameObject; Debug.Log("Afterglow: already installed. No changes made.", installed); return; }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var font = screen.GetComponentsInChildren<TMP_Text>(true).Select(t=>t.font).FirstOrDefault(f=>f!=null) ?? TMP_Settings.defaultFontAsset;
                prefab = Build(font);
            }
            if (prefab == null) return;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Install Simple Tutorial");
            // Only known legacy tutorial objects are disabled. Never apply changes to the lobby prefab.
            foreach (string name in new[]{"Welcome Page","Introduction + Stage 2","Heading","Body Text","Amber Trim"})
                Hide(screen.Find(name));
            if (screen.parent != null) Hide(screen.parent.Find("Tutorial Practice"));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, screen);
            Undo.RegisterCreatedObjectUndo(instance, "Install Simple Tutorial");
            var r = (RectTransform)instance.transform;
            FitToScreen(r, screen);
            r.SetAsLastSibling();
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            var backing = selected.GetComponent<Image>();
            if (backing != null) { Undo.RecordObject(backing,"Screen backing"); backing.raycastTarget=false; backing.color=Background; PrefabUtility.RecordPrefabInstancePropertyModifications(backing); }
            ConfigureRaycaster(canvas);
            EditorSceneManager.MarkSceneDirty(selected.scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject=instance;
            Debug.Log("Afterglow: Simple tutorial installed. Assign Game Scene on Simple Tutorial Flow and save the scene. Legacy tutorial objects are inactive.",instance);
        }
        [MenuItem("Tools/Afterglow Simple Tutorial/2. Create Standalone Tutorial Prefab")]
        public static void CreateStandalone()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing!=null) { Selection.activeObject=existing; EditorGUIUtility.PingObject(existing); return; }
            var prefab=Build(TMP_Settings.defaultFontAsset);
            if(prefab!=null) { Selection.activeObject=prefab; EditorGUIUtility.PingObject(prefab); }
        }
        static GameObject Build(TMP_FontAsset font)
        {
            try { return BuildCore(font); }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Afterglow: build failed", ex.Message + "\nSee Console for the full error.", "OK");
                return null;
            }
        }
        static GameObject BuildCore(TMP_FontAsset font)
        {
            if (font==null) { EditorUtility.DisplayDialog("Afterglow", "Import TMP Essential Resources first.", "OK"); return null; }
            AssetDatabase.Refresh();
            var drawings=ArtNames.Select(LoadDrawing).ToArray();
            var rounded=LoadRounded();
            if(rounded==null) throw new InvalidOperationException("Cannot load Rounded Button sprite. Tried:\n" + string.Join("\n", RoundedPaths));
            Directory.CreateDirectory(Output); AssetDatabase.Refresh();
            var root=new GameObject(RootName,typeof(RectTransform),typeof(SimpleTutorialFlow)); root.layer=5;
            var rect=(RectTransform)root.transform; rect.sizeDelta=new Vector2(900,340);
            var flow=root.GetComponent<SimpleTutorialFlow>(); var pages=new List<GameObject>();
            try
            {
                var welcome=CreateWelcome(rect,font,rounded,flow);
                pages.Add(welcome.gameObject);
                var intro=Page(rect,"Introduction Page 01"); pages.Add(intro.gameObject);
                Label(intro,"Kicker","INTRODUCTION",font,15,Orange,.89f,600,24);
                Label(intro,"Title","Discover Your Campus",font,32,Title,.75f,720,43,true);
                Label(intro,"Line 1","Explore the campus.",font,22,Body,.56f,760,29);
                Label(intro,"Line 2","Meet new people.",font,22,Body,.46f,760,29);
                Label(intro,"Line 3","Discover shared memories.",font,22,Body,.36f,760,29);
                Nav(intro,font,rounded,flow,"NEXT");
                var preview=Page(rect,"Introduction Page 02"); pages.Add(preview.gameObject);
                Label(preview,"Kicker","INTRODUCTION",font,15,Orange,.93f,600,23);
                Label(preview,"Title","Five Basic Controls",font,30,Title,.80f,760,40,true);
                Label(preview,"Subtitle","Get familiar with your controllers.",font,20,Body,.67f,760,28);
                var strip=Child(preview,"Preview Strip",.5f,.43f,740,112);
                for(int i=0;i<5;i++)
                {
                    var tile=Child(strip,ArtNames[i],.5f,.5f,132,100);tile.anchoredPosition=new Vector2((i-2)*140,0);
                    RenderPreview(Child(tile,"Vector Artwork",.5f,.65f,126,72),drawings[i]);
                    Label(tile,"Preview Label",i==4?"Grab":Titles[i],font,15,Body,.11f,132,24);
                }
                Nav(preview,font,rounded,flow,"START CONTROLS");
                for(int i=0;i<5;i++)
                {
                    var page=Page(rect,Titles[i]+" Page"); pages.Add(page.gameObject);
                    Label(page,"Kicker","BASIC CONTROLS  ·  "+(i+1)+"/5",font,15,Orange,.94f,480,23);
                    Label(page,"Title",Titles[i],font,31,Title,.82f,700,40,true);
                    var art=Child(page,"Illustration",.5f,.535f,520,520*220f/760);
                    Render(art,drawings[i],font);
                    Label(page,"Instruction",Main[i],font,20,Body,.26f,830,29);
                    Label(page,"Tip",Secondary[i],font,17,Hint,.18f,830,24);
                    Nav(page,font,rounded,flow,"NEXT");
                }
                var final=Page(rect,"Enter Game Page"); pages.Add(final.gameObject);
                Label(final,"Kicker","AFTERGLOW",font,16,Orange,.90f,600,26);
                Label(final,"Title","Begin Your Journey",font,37,Title,.74f,780,53,true);
                Divider(final,.62f);
                Label(final,"Message","The campus is waiting for you.",font,22,Body,.52f,760,34);
                var enter=ButtonAt(final,"Enter Game Button","ENTER GAME",font,rounded,.5f,.30f,410,57,flow.EnterGame,24);
                ButtonAt(final,"Back Button","BACK",font,rounded,.13f,.30f,120,44,flow.Back,17);
                var status=Label(final,"Load Status","",font,16,Hint,.105f,850,30);
                var so=new SerializedObject(flow); var arr=so.FindProperty("pages"); arr.arraySize=pages.Count;
                for(int i=0;i<pages.Count;i++) arr.GetArrayElementAtIndex(i).objectReferenceValue=pages[i];
                so.FindProperty("finalPageIndex").intValue=pages.Count-1;
                so.FindProperty("statusText").objectReferenceValue=status;
                so.FindProperty("enterButton").objectReferenceValue=enter;
                so.ApplyModifiedPropertiesWithoutUndo();
                for(int i=0;i<pages.Count;i++)pages[i].SetActive(i==0);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath,out bool ok);
                if(!ok){Debug.LogError("Afterglow: failed to save prefab.");return null;}
                AssetDatabase.SaveAssets(); return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static RectTransform CreateWelcome(RectTransform parent,TMP_FontAsset font,Sprite rounded,SimpleTutorialFlow flow)
        {
            var r=Page(parent,"Welcome Page");
            Label(r,"University","UBC OKANAGAN",font,16,Orange,.90f,520,25);
            Label(r,"Title","AFTERGLOW",font,37,Title,.75f,620,53,true);
            Divider(r,.635f);
            Label(r,"Welcome Message","Welcome to your journey.",font,22,new Color32(209,203,195,255),.535f,620,34);
            ButtonAt(r,"Start Button","START",font,rounded,.5f,.265f,410,57,flow.Next,24);
            Label(r,"Trigger Hint","Point and press the trigger.",font,17,new Color32(167,161,156,255),.095f,600,30);
            return r;
        }
        static void Nav(RectTransform p,TMP_FontAsset font,Sprite rounded,SimpleTutorialFlow flow,string next)
        {
            ButtonAt(p,"Back Button","BACK",font,rounded,.13f,.075f,120,42,flow.Back,17);
            ButtonAt(p,"Next Button",next,font,rounded,.5f,.075f,250,42,flow.Next,18);
            ButtonAt(p,"Skip Tutorial Button","SKIP TUTORIAL",font,rounded,.86f,.075f,175,42,flow.SkipTutorial,16);
        }
        static Button ButtonAt(RectTransform parent,string name,string text,TMP_FontAsset font,Sprite sprite,float x,float y,float w,float h,UnityAction action,int fontSize)
        {
            var r=Child(parent,name,x,y,w,h); var im=r.gameObject.AddComponent<Image>(); var b=r.gameObject.AddComponent<Button>(); b.targetGraphic=im; SetupButton(b,sprite);
            Label(r,"Label",text,font,fontSize,Ink,.5f,w-8,h-4,true);
            UnityEventTools.AddPersistentListener(b.onClick,action); return b;
        }
        static void SetupButton(Button b,Sprite sprite)
        {
            if(b.targetGraphic is Image im){ im.sprite=sprite; im.type=Image.Type.Sliced; im.color=Color.white; im.raycastTarget=true; }
            b.transition=Selectable.Transition.ColorTint; var c=b.colors; c.normalColor=Orange;c.highlightedColor=Hover;c.selectedColor=Hover;c.pressedColor=Pressed;c.disabledColor=new Color32(102,79,60,255);c.colorMultiplier=1;c.fadeDuration=.06f;b.colors=c;
            var nav=b.navigation; nav.mode=Navigation.Mode.None;b.navigation=nav;
            if(b.targetGraphic!=null)b.targetGraphic.canvasRenderer.SetColor(Orange);
        }
        static RectTransform Page(RectTransform parent,string name)
        {
            var r=Child(parent,name,.5f,.5f,900,340); Stretch(r); var im=r.gameObject.AddComponent<Image>();im.color=Background; im.raycastTarget=false; return r;
        }
        static void Divider(RectTransform p,float y)
        { var im=Child(p,"Gold Divider",.5f,y,64,2).gameObject.AddComponent<Image>();im.color=Orange;im.raycastTarget=false; }
        static RectTransform Child(RectTransform parent,string name,float x,float y,float w,float h)
        { var go=new GameObject(name,typeof(RectTransform));go.layer=parent.gameObject.layer; var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(x,y);r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(w,h);r.anchoredPosition=Vector2.zero;return r; }
        static TextMeshProUGUI Label(RectTransform p,string name,string text,TMP_FontAsset font,float size,Color color,float y,float w,float h,bool bold=false,float x=.5f)
        { var t=Child(p,name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.color=color;t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.enableAutoSizing=false;return t; }
        static void Stretch(RectTransform r)
        {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,.5f);r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one;r.localRotation=Quaternion.identity;r.anchoredPosition3D=Vector3.zero;}
        static void FitToScreen(RectTransform r,RectTransform screen)
        {r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(900,340);r.anchoredPosition3D=Vector3.zero;r.localRotation=Quaternion.identity;float scale=Mathf.Min(screen.rect.width/900f,screen.rect.height/340f);r.localScale=Vector3.one*Mathf.Max(.001f,scale);}
        static Drawing LoadDrawing(string name)
        {
            var paths = ArtRoots.Select(root => root + "VectorData/" + name + ".json").ToArray();
            var failures = new List<string>();
            foreach (string path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null) { failures.Add(path + " (not found)"); continue; }
                try
                {
                    var drawing = JsonUtility.FromJson<Drawing>(asset.text);
                    if (drawing == null || drawing.parts == null || drawing.parts.Length == 0 ||
                        drawing.parts.Any(p => p == null || p.vertices == null || p.vertices.Length < 3 ||
                            p.triangles == null || p.triangles.Length < 3 || p.triangles.Length % 3 != 0 ||
                            p.triangles.Any(i => i < 0 || i >= p.vertices.Length)))
                        throw new InvalidDataException("Invalid vector mesh data.");
                    Debug.Log("Afterglow: loaded " + path);
                    return drawing;
                }
                catch (Exception ex) { failures.Add(path + " (" + ex.Message + ")"); }
            }
            throw new InvalidDataException("Cannot load " + name + ".json. Tried:\n" + string.Join("\n", failures));
        }
        static Sprite LoadRounded()
        {
            foreach (string path in RoundedPaths)
            {
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                if (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single ||
                    imp.spriteBorder != new Vector4(12,12,12,12) || !imp.alphaIsTransparency || imp.mipmapEnabled)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.spriteBorder = new Vector4(12,12,12,12);
                    imp.alphaIsTransparency = true;
                    imp.mipmapEnabled = false;
                    imp.SaveAndReimport();
                }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) { Debug.Log("Afterglow: loaded " + path); return sprite; }
            }
            return null;
        }
        static Color Accent(Color color)
        {Color32 c=color; if(c.r>=145 && c.r-c.g>=17 && c.g-c.b>=13 && c.r-c.b>=38){float f=Mathf.Clamp(c.r/197f,.50f,1.15f);return new Color(Mathf.Clamp01(Orange.r*f),Mathf.Clamp01(Orange.g*f),0,color.a);}return color;}
        static Color Parse(string color){return ColorUtility.TryParseHtmlString(color,out var c)?Accent(c):Color.white;}
        static void Render(RectTransform frame,Drawing d,TMP_FontAsset font)
        {foreach(var p in d.parts) AddPart(frame,p);if(d.labels!=null)foreach(var l in d.labels)Label(frame,l.name,l.text,font,l.size*frame.sizeDelta.y,Parse(l.color),l.y,l.width*frame.sizeDelta.x,l.height*frame.sizeDelta.y,false,l.x);}
        static void RenderPreview(RectTransform frame,Drawing d)
        {var parts=d.parts.Where(p=>p.name!="Callout").ToArray();float x0=parts.Min(p=>p.x0),x1=parts.Max(p=>p.x1),y0=parts.Min(p=>p.y0),y1=parts.Max(p=>p.y1);float scale=Mathf.Min(126/((x1-x0)*760),68/((y1-y0)*220));var inner=Child(frame,"Geometry",.5f,.5f,760*scale,220*scale);inner.anchoredPosition=new Vector2((.5f-(x0+x1)/2)*760*scale,(.5f-(y0+y1)/2)*220*scale);foreach(var p in parts)AddPart(inner,p);}
        static void AddPart(RectTransform frame,Part p)
        {var r=Child(frame,p.name,0,0,0,0);r.anchorMin=new Vector2(p.x0,p.y0);r.anchorMax=new Vector2(p.x1,p.y1);r.offsetMin=r.offsetMax=Vector2.zero;var g=r.gameObject.AddComponent<SimpleTutorialVectorGraphic>();g.color=Parse(p.color);g.raycastTarget=false;g.SetGeometry(p.vertices,p.triangles);}
        static void Hide(Transform t)
        {if(t==null)return;Undo.RecordObject(t.gameObject,"Disable old tutorial");t.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);}
        static void ConfigureRaycaster(Canvas canvas)
        {const string n="UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster";var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(n,false)).FirstOrDefault(t=>t!=null);if(type!=null && typeof(Component).IsAssignableFrom(type)){var existing=canvas.GetComponent(type) as Behaviour;if(existing==null)Undo.AddComponent(canvas.gameObject,type);else if(!existing.enabled){Undo.RecordObject(existing,"Enable XR raycaster");existing.enabled=true;PrefabUtility.RecordPrefabInstancePropertyModifications(existing);}}else Debug.LogWarning("Afterglow: add Tracked Device Graphic Raycaster manually if using XRI.",canvas);var systems=UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);if(systems.Length!=1)Debug.LogWarning("Afterglow: verify exactly one active EventSystem with XR UI Input Module.",canvas);if(canvas.worldCamera==null)Debug.LogWarning("Afterglow: assign XR camera as Event Camera if needed.",canvas);}
    }
}
