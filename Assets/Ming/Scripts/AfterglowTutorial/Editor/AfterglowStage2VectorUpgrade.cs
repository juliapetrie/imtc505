using System;
using System.Linq;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Afterglow.Tutorial;

namespace Afterglow.Tutorial.Editor
{
    public static class AfterglowStage2VectorUpgrade
    {
        const string DataRoot = "Assets/Ming/Art/VectorData/";
        const string Generated = "Assets/Ming/Generated/";
        static readonly Color Gold = new Color32(197,160,121,255), Body = new Color32(224,217,208,255), Title = new Color32(246,238,228,255), Ink = new Color32(32,27,25,255);
        static readonly string[] Names = {"Look Around Page","Move Page","Turn Page","Select Page","Grab & Release Page"};
        static readonly string[] Art = {"LookAround","Move","Turn","Select","GrabRelease"};
        static readonly string[] Titles = {"Look Around","Move","Turn","Select","Grab & Release"};
        static readonly string[] Main = {"Turn your head and look at the glowing markers.","Use the left thumbstick to move.","Use the right thumbstick to turn.","Point at the target and press the trigger.","Hold the grip button to grab the object."};
        static readonly string[] Hint = {"", "Walk to the glowing circle.","Turn towards the glowing markers.","Select the glowing target.","Release it onto the glowing platform."};
        [Serializable] class Drawing { public Part[] parts; public LabelData[] labels; }
        [Serializable] class Part { public string name,color; public float x0,y0,x1,y1; public Vector2[] vertices; public int[] triangles; }
        [Serializable] class LabelData { public string name,text,color; public float x,y,width,height,size; }

        [MenuItem("Tools/Afterglow Tutorial/5. Upgrade Introduction + Stage 2 Vector Layout")]
        public static void Upgrade()
        {
            var selected = Selection.activeGameObject;
            if (selected == null || EditorUtility.IsPersistent(selected) || PrefabStageUtility.GetCurrentPrefabStage()!=null || !selected.scene.IsValid())
            { EditorUtility.DisplayDialog("Afterglow","Open Tutorial scene and select Tutorial Info Screen or a page in the Hierarchy.","OK"); return; }
            var flow = selected.GetComponentInParent<AfterglowTutorialFlowManager>(true) ?? selected.GetComponentInChildren<AfterglowTutorialFlowManager>(true);
            if (flow == null) { EditorUtility.DisplayDialog("Afterglow","Install Introduction + Stage 2 first (menu 3).","OK"); return; }
            var drawings = Art.Select(LoadDrawing).ToArray();
            if (drawings.Any(x=>x==null || x.parts==null || x.parts.Length==0)) { Debug.LogError("Afterglow: missing or invalid VectorData. No pages changed."); return; }
            var root = flow.transform;
            var pages = Names.Select(x=>root.Find(x) as RectTransform).ToArray();
            var intro = root.Find("Introduction Page 02") as RectTransform;
            var intro1 = root.Find("Introduction Page 01") as RectTransform;
            if (pages.Any(x=>x==null) || intro==null || intro1==null) { Debug.LogError("Afterglow: expected page names not found. No pages changed.",flow); return; }
            var overlay = pages[3].GetComponentInChildren<TutorialSelectTarget>(true);
            if (overlay==null) { Debug.LogError("Afterglow: Select Target Overlay missing. Reconnect the existing TutorialSelectTarget before upgrading.",pages[3]); return; }
            var font = root.GetComponentsInChildren<TMP_Text>(true).Select(x=>x.font).FirstOrDefault(x=>x!=null) ?? TMP_Settings.defaultFontAsset;
            if (font==null) { Debug.LogError("Afterglow: no TMP font found. Import TMP Essentials first."); return; }
            if (!EditorUtility.DisplayDialog("Afterglow","Replace the seven page layouts / preview illustrations and update their saved prefab assets? Existing scene tasks and references are preserved. Existing custom visual overrides may be replaced. Commit or back up before continuing.","Upgrade","Cancel")) return;
            Directory.CreateDirectory(Generated); AssetDatabase.Refresh();
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Afterglow Vector Upgrade");
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject,"Afterglow Vector Upgrade");
            var rounded=LoadRounded();
            for (int i=0;i<pages.Length;i++) { UpgradeStage(pages[i],i,drawings[i],font,rounded, i==3?overlay:null); SavePage(pages[i]); }
            UpgradeIntro(intro,drawings,font,rounded);
            FixButtons(intro1,rounded); FixButtons(intro,rounded);
            SavePage(intro1); SavePage(intro);
            var grab = flow.GetComponentInChildren<TutorialGrabTask>(true);
            var practice = root.parent != null && root.parent.parent != null ? root.parent.parent.Find("Tutorial Practice") : null;
            var dest = practice!=null ? practice.Find("Grab Target Pedestal") : null;
            if (grab!=null && dest!=null) { var so=new SerializedObject(grab); var p=so.FindProperty("destinationPedestal"); if(p!=null && p.objectReferenceValue==null){p.objectReferenceValue=dest;so.ApplyModifiedProperties();} }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(selected.scene); Undo.CollapseUndoOperations(group);
            Debug.Log("Afterglow: Introduction previews and all five controls upgraded. Save scene and test Skip + Select.",flow);
        }
        static Drawing LoadDrawing(string name)
        {
            var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(DataRoot+name+".json");
            return asset==null?null:JsonUtility.FromJson<Drawing>(asset.text);
        }
        static Sprite LoadRounded()
        {
            string path=Generated+"Rounded Button.png";
            var imp=AssetImporter.GetAtPath(path) as TextureImporter;
            if(imp!=null && (imp.textureType!=TextureImporterType.Sprite || imp.spriteImportMode!=SpriteImportMode.Single || imp.spriteBorder!=new Vector4(12,12,12,12)))
            {imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.spriteBorder=new Vector4(12,12,12,12);imp.SaveAndReimport();}
            var result=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(result==null) Debug.LogWarning("Afterglow: Rounded Button.png missing; buttons will use a plain background.");
            return result;
        }
        static void FixButtons(RectTransform page,Sprite sprite)
        {
            if(sprite==null)return;
            foreach(var b in page.GetComponentsInChildren<Button>(true)) if(b.targetGraphic is Image im)
            {Undo.RecordObject(im,"Round button"); im.sprite=sprite;im.type=Image.Type.Sliced;PrefabUtility.RecordPrefabInstancePropertyModifications(im);}
        }
        static void UpgradeStage(RectTransform page,int i,Drawing d,TMP_FontAsset font,Sprite rounded,TutorialSelectTarget overlay)
        {
            // Keep the existing parent of Select Target (and its serialized task reference).
            RectTransform frame=overlay!=null ? overlay.transform.parent as RectTransform : null;
            if(frame==page)frame=null;
            if(frame==null)frame=page.Find("Illustration") as RectTransform ?? page.Find("Illustration Root") as RectTransform;
            if(frame==null)frame=Child(page,"Illustration",new Vector2(.5f,.52f),new Vector2(620,620*220f/760));
            // Only known generated visual objects are replaced. Custom siblings are preserved.
            foreach(string name in new[]{"Kicker","Title","Top Text","Bottom Text","Legend","Left Legend","Right Legend","Skip Button"}) RemoveNamed(page,name);
            SetRect(frame,new Vector2(.5f,.52f),new Vector2(620,620*220f/760)); frame.gameObject.SetActive(true);
            var old=frame.GetComponent<Image>(); if(old!=null){Undo.RecordObject(old,"Disable legacy PNG"); old.sprite=null;old.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(old);}
            RemoveNamed(frame,"Vector Artwork");
            var art=Child(frame,"Vector Artwork",new Vector2(.5f,.5f),frame.sizeDelta);
            Render(art,d,font,true);
            art.anchorMin=Vector2.zero;art.anchorMax=Vector2.one;art.offsetMin=art.offsetMax=Vector2.zero;
            Text(page,"Kicker","BASIC CONTROLS · "+(i+1)+"/5",font,15,Gold,new Vector2(.5f,.94f),new Vector2(490,22));
            Text(page,"Title",Titles[i],font,30,Title,new Vector2(.5f,.84f),new Vector2(560,37),true);
            Text(page,"Top Text",Main[i],font,19,Body,new Vector2(.5f,.17f),new Vector2(820,28));
            if(i==0){Text(page,"Left Legend","●  Left target",font,16,Gold,new Vector2(.40f,.075f),new Vector2(155,24));Text(page,"Right Legend","●  Right target",font,16,Gold,new Vector2(.60f,.075f),new Vector2(155,24));}
            else Text(page,"Bottom Text",Hint[i],font,16,i==4?Body:Gold,new Vector2(.5f,.075f),new Vector2(800,24));
            var skip=Child(page,"Skip Button",new Vector2(1,1),new Vector2(88,34));skip.pivot=new Vector2(1,1);skip.anchoredPosition=new Vector2(-16,-12);
            var image=skip.gameObject.AddComponent<Image>();image.color=Gold;image.sprite=rounded;if(rounded!=null)image.type=Image.Type.Sliced;
            var btn=skip.gameObject.AddComponent<Button>();btn.targetGraphic=image;
            var colors=btn.colors;colors.highlightedColor=new Color(.95f,.9f,.8f);colors.pressedColor=new Color(.72f,.72f,.72f);btn.colors=colors;
            Text(skip,"Label","SKIP",font,15,Ink,new Vector2(.5f,.5f),new Vector2(84,30),true);
            var comp=page.GetComponent<AfterglowTutorialPage>();if(comp!=null)UnityEventTools.AddPersistentListener(btn.onClick,comp.GoNext);
            if(overlay!=null)
            {
                var r=(RectTransform)overlay.transform;
                if(r.parent!=frame)Undo.SetTransformParent(r,frame,"Align target");
                SetRect(r,new Vector2(520f/760f,1f-64f/220f),new Vector2(620f*112f/760f,620f*112f/760f));
                r.SetAsLastSibling();
                var im=r.GetComponent<Image>();if(im!=null){Undo.RecordObject(im,"Align Select target");im.enabled=true;im.raycastTarget=true;im.color=new Color(1,1,1,.001f);PrefabUtility.RecordPrefabInstancePropertyModifications(im);}
            }
        }
        static void UpgradeIntro(RectTransform intro,Drawing[] drawings,TMP_FontAsset font,Sprite rounded)
        {
            var strip=intro.Find("Preview Strip") as RectTransform;
            if(strip==null)strip=Child(intro,"Preview Strip",new Vector2(.5f,.43f),new Vector2(740,112));
            SetRect(strip,new Vector2(.5f,.43f),new Vector2(740,112));
            for(int i=0;i<Art.Length;i++)
            {
                var tile=strip.Find(Art[i]) as RectTransform;
                if(tile==null)tile=Child(strip,Art[i],new Vector2(.5f,.5f),new Vector2(132,100));
                SetRect(tile,new Vector2(.5f,.5f),new Vector2(132,100));tile.anchoredPosition=new Vector2((i-2)*140,0);
                var im=tile.GetComponent<Image>();if(im!=null){Undo.RecordObject(im,"Disable old preview PNG");im.sprite=null;im.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(im);}
                RemoveNamed(tile,"Vector Artwork");RemoveNamed(tile,"Preview Label");
                var frame=Child(tile,"Vector Artwork",new Vector2(.5f,.64f),new Vector2(126,72));
                RenderPreview(frame,drawings[i],font);
                Text(tile,"Preview Label",i==4?"Grab":Titles[i],font,14,Body,new Vector2(.5f,.12f),new Vector2(132,23));
            }
        }
        static void RenderPreview(RectTransform frame,Drawing d,TMP_FontAsset font)
        {
            var ps=d.parts.Where(p=>p.name!="Callout").ToArray();
            float x0=ps.Min(p=>p.x0),x1=ps.Max(p=>p.x1),y0=ps.Min(p=>p.y0),y1=ps.Max(p=>p.y1);
            float w=(x1-x0)*760,h=(y1-y0)*220; float scale=Mathf.Min(126/w,68/h);
            var inner=Child(frame,"Geometry",new Vector2(.5f,.5f),new Vector2(760*scale,220*scale));
            inner.anchoredPosition=new Vector2((.5f-(x0+x1)/2)*760*scale,(.5f-(y0+y1)/2)*220*scale);
            foreach(var p in ps) AddPart(inner,p);
        }
        static void Render(RectTransform frame,Drawing d,TMP_FontAsset font,bool labels)
        {
            foreach(var p in d.parts)AddPart(frame,p);
            if(labels && d.labels!=null)foreach(var l in d.labels)
                Text(frame,l.name,l.text,font,l.size*frame.sizeDelta.y,Parse(l.color),new Vector2(l.x,l.y),new Vector2(l.width*frame.sizeDelta.x,l.height*frame.sizeDelta.y));
        }
        static void AddPart(RectTransform frame,Part p)
        {
            var r=Child(frame,p.name,Vector2.zero,Vector2.zero);r.anchorMin=new Vector2(p.x0,p.y0);r.anchorMax=new Vector2(p.x1,p.y1);r.offsetMin=r.offsetMax=Vector2.zero;
            var g=r.gameObject.AddComponent<AfterglowVectorGraphic>();g.color=Parse(p.color);g.raycastTarget=false;g.SetGeometry(p.vertices,p.triangles);
        }
        static Color Parse(string s){return ColorUtility.TryParseHtmlString(s,out var c)?c:Color.white;}
        static RectTransform Child(RectTransform parent,string name,Vector2 anchor,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=parent.gameObject.layer;Undo.RegisterCreatedObjectUndo(go,"Create "+name);
            var r=(RectTransform)go.transform;r.SetParent(parent,false);SetRect(r,anchor,size);return r;
        }
        static void SetRect(RectTransform r,Vector2 a,Vector2 size)
        {Undo.RecordObject(r,"Set layout");r.anchorMin=r.anchorMax=a;r.pivot=new Vector2(.5f,.5f);r.sizeDelta=size;r.anchoredPosition=Vector2.zero;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
        static TextMeshProUGUI Text(RectTransform parent,string name,string value,TMP_FontAsset font,float size,Color c,Vector2 a,Vector2 dimensions,bool bold=false)
        {
            var r=Child(parent,name,a,dimensions);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=c;t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;
        }
        static void RemoveNamed(RectTransform parent,string name)
        {
            var old=parent.Find(name);if(old==null)return;
            if(PrefabUtility.IsPartOfPrefabInstance(old.gameObject) && !PrefabUtility.IsAddedGameObjectOverride(old.gameObject))
            {Undo.RecordObject(old.gameObject,"Hide previous layout");old.gameObject.SetActive(false);old.gameObject.name="Legacy "+name;PrefabUtility.RecordPrefabInstancePropertyModifications(old.gameObject);}
            else Undo.DestroyObjectImmediate(old.gameObject);
        }
        static void SavePage(RectTransform page)
        {
            var copy=UnityEngine.Object.Instantiate(page.gameObject);copy.name=page.name;
            try{
                if(PrefabUtility.IsPartOfPrefabInstance(copy))PrefabUtility.UnpackPrefabInstance(copy,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                copy.SetActive(true);
                var path=Generated+page.name+".prefab";
                PrefabUtility.SaveAsPrefabAsset(copy,path,out bool success);
                if(!success)Debug.LogError("Afterglow: failed to save "+path,page);
            }finally{UnityEngine.Object.DestroyImmediate(copy);}
        }
    }
}
