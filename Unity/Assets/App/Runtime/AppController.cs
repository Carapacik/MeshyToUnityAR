using System;
using System.Collections;
using System.Globalization;
using StreetArt.AR;
using UnityEngine;
using UnityEngine.UIElements;

namespace StreetArt
{
    public sealed partial class AppController : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private SurfacePlacement placement;
        [SerializeField] private Camera preview;
        [SerializeField] private Material modelMaterial;
        private VisualElement root, body;
        private Label notice, trackingLabel, gpsLabel;
        private Button placeButton, saveButton, nearbyButton;
        private ServerApi api;
        private GpsLocation gps; private MapView mapView;
        private Transform content, model;
        private string selectedId, message="";
        private Placement nearby;
        private bool canvas, busy, saved, loadingModel;
        private float scale=1, rotation, nextNearby;
        private int selection;
        private Coroutine generation;
        public void Configure(UIDocument ui,SurfacePlacement ar,Camera previewCamera,Material material)
        { document=ui; placement=ar; preview=previewCamera; modelMaterial=material; }
        private void Start()
        {
            Application.targetFrameRate=30;
            try { api=new ServerApi(PlayerPrefs.GetString("server_url","http://localhost:5052")); }
            catch { api=new ServerApi("http://localhost:5052"); }
            Home();
            OpenDocumentationPreview();
        }
        private VisualElement Element(string css,VisualElement parent=null)
        { var item=new VisualElement(); foreach(var name in css.Split(' ')) if(name.Length>0)item.AddToClassList(name); (parent??body).Add(item); return item; }
        private Label Text(string text,string css,VisualElement parent=null)
        { var item=new Label(text); foreach(var name in css.Split(' ')) if(name.Length>0)item.AddToClassList(name); (parent??body).Add(item); return item; }
        private Button Button(string text,Action action,string css="",VisualElement parent=null)
        { var item=new Button(action){text=text}; if(css.Length>0)foreach(var name in css.Split(' ')) if(name.Length>0)item.AddToClassList(name); (parent??body).Add(item); return item; }
        private void Layout(bool transparent=false)
        {
            root=document.rootVisualElement; root.Clear();
            root.style.backgroundColor=transparent && placement.HasCameraImage?Color.clear:Color.white;
            root.AddToClassList("app"); root.EnableInClassList("camera-view",transparent);
            var safe=Element("safe",root);
            var header=Element("header",safe);
            Text("STREET / ART","brand",header);
            if(transparent) { header.Clear(); Button("Back",Home,"quiet",header); Text("Artist","badge artist-title",header); }
            else Text("AR","badge",header);
            body=Element(transparent?"canvas-body":"body",safe);
            notice=Text(message,"notice",safe);
            RefreshSafeArea();
        }
        private void RefreshSafeArea()
        {
            if(root==null)return;
            float uiScale=Mathf.Min(Screen.width/430f,Screen.height/820f);
            document.panelSettings.scale=Mathf.Max(.5f,uiScale);
            var safe=root.Q(className:"safe");
            Rect rect=Screen.safeArea;
            safe.style.paddingLeft=20+rect.xMin/uiScale;
            safe.style.paddingRight=20+(Screen.width-rect.xMax)/uiScale;
            safe.style.paddingTop=16+(Screen.height-rect.yMax)/uiScale;
            safe.style.paddingBottom=12+rect.yMin/uiScale;
        }
        private void Update()
        {
            RefreshSafeArea();
            if(notice!=null){notice.text=message;notice.style.display=string.IsNullOrEmpty(message)?DisplayStyle.None:DisplayStyle.Flex;}
            if(!canvas)return;
            preview.enabled=!placement.Camera.gameObject.activeInHierarchy;
            root.style.backgroundColor=placement.HasCameraImage?Color.clear:Color.white;
            if(trackingLabel!=null)trackingLabel.text=placement.Status;
            if(gpsLabel!=null)gpsLabel.text=gps ? gps.Status : "Starting GPS...";
            placeButton?.SetEnabled(placement.CanPlace&&!loadingModel);
            saveButton?.SetEnabled(placement.IsPlaced && gps && gps.Ready && !busy && !saved && !loadingModel);
            if(saveButton!=null) saveButton.text=saved?"Saved":gps && gps.Ready && !gps.IsPrecise?"Save approx.":"Save"; var confirmation=body?.Q(className:"confirmation"); if(confirmation!=null)confirmation.style.display=placement.IsPlaced&&!saved?DisplayStyle.Flex:DisplayStyle.None; if(placeButton!=null)placeButton.style.display=placement.IsPlaced?DisplayStyle.None:DisplayStyle.Flex;
            if(gps && gps.Ready && Time.unscaledTime>nextNearby)
            { nextNearby=Time.unscaledTime+15; StartCoroutine(FindNearby()); }
            if(nearbyButton!=null)
            {
                bool visible=nearby!=null && gps && gps.Ready;
                nearbyButton.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
                if(visible) nearbyButton.text="Artwork nearby · "+Math.Round(gps.Distance(nearby.latitude,nearby.longitude))+" m / Restore";
            }
        }
        private void Home()
        {
            selection++; canvas=false; loadingModel=false; busy=false; message="";
            if(mapView){Destroy(mapView);mapView=null;}
            if(generation!=null){StopCoroutine(generation);generation=null;}
            placement.End();
            if(content)Destroy(content.gameObject);content=null;model=null;
            preview.enabled=true;Layout();
            body.AddToClassList("menu-body");
            Text("StreetArtAI","menu-title");
            var actions=Element("menu-actions");
            Button("AR Camera",OpenCamera,"primary menu-action",actions);
            Button("Map",ShowMap,"secondary menu-action",actions);
            Button("Server settings",ShowSettings,"settings-link");
            Text("Place a little imagination in the real world.","hint");
        }
        private void OpenCamera(){ SelectModel(""); }
        private void ShowSettings()
        {
            Layout();Text("Server settings","title");
            var address=new TextField{value=api.BaseUrl};address.AddToClassList("address");body.Add(address);
            Button("Save",()=>{try{api.SetAddress(address.value);PlayerPrefs.SetString("server_url",api.BaseUrl);PlayerPrefs.Save();message="Server address saved.";}catch(Exception e){message=e.Message;}},"primary");
            Button("Test connection",()=>StartCoroutine(CheckServer(address.value)),"secondary");
            Text("Enter the HTTPS address of your StreetArtAI server.","hint");
            Button("Back",Home);
        }
        private IEnumerator CheckServer(string address)
        {
            ServerApi candidate;
            try { candidate=new ServerApi(address); }
            catch(Exception e){message=e.Message;yield break;}
            message="Checking connection...";
            yield return candidate.Request<ServerConfig>("GET","/api/config",null,
                config=>message=config.generationConfigured
                    ?"Connected. Library, GPS locations and Meshy generation are available."
                    :"Connected. Library and GPS locations are available. Meshy is not configured.",
                error=>message=error);
        }
        partial void OpenDocumentationPreview();
        private void ShowMap()
        {
            Home();
            Layout();Text("Nearby artwork","title");
            if(!gps)gps=gameObject.AddComponent<GpsLocation>();
            mapView=gameObject.AddComponent<MapView>();
            mapView.Show(body,gps,api,p=>{
                if(!gps.Ready||gps.Distance(p.latitude,p.longitude)>100){message="Move within 100 meters of this point to restore it.";return;}
                Destroy(mapView);mapView=null;SelectModel(p.modelId,p);
            });
            Button("Back",Home);
        }
        private void ChooseSource()
        {
            Layout(true);
            var sheet=Element("create-sheet");
            Text("Create an object","title",sheet);
            Button("Choose from library",ShowLibrary,"primary",sheet);
            Button("Generate with Meshy",ShowGeneration,"secondary",sheet);
            Button("Back",BuildCanvas,"",sheet);
        }
        private void ShowLibrary()
        {
            Layout(canvas);
            var sheet=Element("library-sheet");
            Text("Model library","title",sheet);
            var scroll=new ScrollView(ScrollViewMode.Vertical);sheet.Add(scroll);scroll.style.flexGrow=1; scroll.horizontalScrollerVisibility=ScrollerVisibility.Hidden; scroll.verticalScrollerVisibility=ScrollerVisibility.Hidden;
            scroll.contentContainer.AddToClassList("model-grid");
            VisualElement row=null;
            for(int index=0;index<ModelLibrary.Items.Length;index++)
            {
                if(index%2==0) row=Element("model-row",scroll.contentContainer);
                var item=ModelLibrary.Items[index];
                var card=Element("model-card",row);
                var thumbnail=new Image{image=Resources.Load<Texture2D>("ModelPreviews/"+item.id),scaleMode=ScaleMode.ScaleToFit};
                thumbnail.AddToClassList("model-preview");card.Add(thumbnail);
                Text(item.name,"model-name",card);Text(item.description,"model-description",card);
                string id=item.id;Button("Place in AR",()=>SelectModel(id),"primary",card);
            }
            Button("Back",()=>{if(canvas)BuildCanvas();else Home();},"",sheet);
        }
        private void ShowGeneration()
        {
            Layout(true);
            var sheet=Element("create-sheet");
            Text("Create an object","title",sheet);
            var prompt=new TextField{multiline=true,maxLength=800};prompt.AddToClassList("prompt");sheet.Add(prompt);
            var texture=new Toggle("Add textures"){value=true};sheet.Add(texture);
            Text("Describe one object. Generation requires a Meshy key configured on the server.","hint",sheet);
            Button("Generate",()=>{
                if(busy)return;
                if(string.IsNullOrWhiteSpace(prompt.value)){message="Describe the object first.";return;}
                busy=true;generation=StartCoroutine(Generate(prompt.value,texture.value));
            },"primary",sheet);
            Button("Back",()=>{if(generation!=null){StopCoroutine(generation);generation=null;}busy=false;message="";BuildCanvas();},"",sheet);
        }
        private IEnumerator Generate(string prompt,bool textures)
        {
            message="Submitting generation...";
            GenerationCreated created=null;
            yield return api.Request<GenerationCreated>("POST","/api/generations",new GenerateInput{prompt=prompt},x=>created=x,x=>message=x);
            if(created==null || string.IsNullOrEmpty(created.id)){busy=false;yield break;}
            GenerationTask result=null;
            yield return Poll(created.id,x=>result=x);
            if(result==null){busy=false;yield break;}
            if(textures)
            {
                message="Preparing textures..."; created=null;
                yield return api.Request<GenerationCreated>("POST","/api/generations/"+result.id+"/refine",new GenerateInput(),x=>created=x,x=>message=x);
                if(created==null){busy=false;yield break;}
                result=null; yield return Poll(created.id,x=>result=x);
                if(result==null){busy=false;yield break;}
            }
            busy=false; generation=null;
            SelectModel(result.id);
        }
        private IEnumerator Poll(string id,Action<GenerationTask> completed)
        {
            float deadline=Time.realtimeSinceStartup+900;
            while(Time.realtimeSinceStartup<deadline)
            {
                GenerationTask task=null;
                yield return api.Request<GenerationTask>("GET","/api/generations/"+id,null,x=>task=x,x=>message=x);
                if(task==null)yield break;
                if(task.status=="SUCCEEDED"){completed(task);yield break;}
                if(task.status=="FAILED"||task.status=="CANCELED"){message=task.error??"Generation did not complete.";yield break;}
                message="Generating · "+task.progress+"%";
                yield return new WaitForSecondsRealtime(3);
            }
            message="Generation is taking too long. Check the task on your Meshy account.";
        }
        private async void SelectModel(string id,Placement existing=null)
        {
            int version=++selection;
            placement.End();
            if(content)Destroy(content.gameObject);
            canvas=true; busy=false; saved=existing!=null; selectedId=id;
            scale=existing?.scale??.35f; rotation=existing?.rotation??0;
            message=existing==null?"":"GPS locates the area. Align the model on a surface.";
            content=new GameObject("Artwork").transform; content.gameObject.SetActive(false);
            if(!gps)gps=gameObject.AddComponent<GpsLocation>();
            BuildCanvas();
            loadingModel=true;
            GameObject instance=null;
            try
            {
                if(string.IsNullOrEmpty(id)) instance=new GameObject("Empty canvas"); else if(ModelLibrary.Contains(id)) instance=ModelLibrary.Create(id,modelMaterial);
                else
                {
                    instance=new GameObject("Generated artwork");
                    instance.SetActive(false);
                    var loader=instance.AddComponent<GeneratedModel>();
                    if(!await loader.Load(api.BaseUrl+"/api/models/"+Uri.EscapeDataString(id)))
                        throw new InvalidOperationException("Could not load the generated model.");
                }
                if(!this || version!=selection){if(instance)Destroy(instance);return;}
                instance.transform.SetParent(content,false); instance.SetActive(true); model=instance.transform;
                ApplyTransform();
                placement.Begin(content);
            }
            catch(Exception)
            {
                if(instance)Destroy(instance);
                if(this && version==selection)message="Could not load this model. Check the server connection.";
            }
            finally {if(this && version==selection)loadingModel=false;}
        }
        private void BuildCanvas()
        {
            Layout(true);
            var status=Element("status");
            trackingLabel=Text("Starting AR...","status-text",status);
            gpsLabel=Text("Starting GPS...","hint",status);
            nearbyButton=Button("",()=>{if(nearby!=null)SelectModel(nearby.modelId,nearby);},"nearby",status);
            nearbyButton.style.display=DisplayStyle.None;
            var cross=Text("+","reticle",root);cross.pickingMode=PickingMode.Ignore;
            var controls=Element("camera-controls");
            if(!string.IsNullOrEmpty(selectedId))
            {
                var foldout=new Foldout{text="Size and rotation",value=false};foldout.AddToClassList("transform-controls");controls.Add(foldout);
                var size=new Slider("Size",.1f,3){value=scale};size.RegisterValueChangedCallback(e=>{scale=e.newValue;ApplyTransform();});foldout.Add(size);
                var angle=new Slider("Rotation",0,359){value=rotation};angle.RegisterValueChangedCallback(e=>{rotation=e.newValue;ApplyTransform();});foldout.Add(angle);
                placeButton=Button("Place on surface",()=>placement.Place(),"primary",controls);
                var confirm=Element("confirmation",controls);
                Text("Save at this location?","confirm-title",confirm);
                var row=Element("confirm-row",confirm);
                Button("Cancel",OpenCamera,"round",row);
                saveButton=Button("Save",()=>{if(!busy)StartCoroutine(SaveLocation());},"round primary",row);
                Button("Move",()=>{saved=false;placement.Reposition();},"quiet",controls);
            }
            else {placeButton=null;saveButton=null;}
            Button("+",ChooseSource,"add-object",controls);
        }
        private void ApplyTransform()
        { if(model){model.localScale=Vector3.one*scale;model.localRotation=Quaternion.Euler(0,rotation,0);} }
        private IEnumerator SaveLocation()
        {
            if(!gps.Ready||!placement.IsPlaced||saved)yield break;
            busy=true; int version=selection;
            var input=new Placement{modelId=selectedId,latitude=gps.Latitude,longitude=gps.Longitude,scale=scale,rotation=rotation,accuracyMeters=gps.AccuracyMeters};
            message="Saving GPS location...";
            yield return api.Request<Placement>("POST","/api/placements",input,
                x=>{if(version==selection){saved=true;message="Location saved · "+Mathf.CeilToInt(input.accuracyMeters)+" m accuracy. Return here to restore it.";}},
                x=>{if(version==selection)message=x;});
            if(version==selection)busy=false;
        }
        private IEnumerator FindNearby()
        {
            int version=selection;
            string lat=gps.Latitude.ToString("R",CultureInfo.InvariantCulture),lon=gps.Longitude.ToString("R",CultureInfo.InvariantCulture);
            yield return api.Request<PlacementsResponse>("GET","/api/placements?latitude="+lat+"&longitude="+lon+"&radiusMeters=100",null,
                x=>{if(canvas&&version==selection)nearby=x.items!=null&&x.items.Length>0?x.items[0]:null;},
                x=>{if(canvas&&version==selection&&string.IsNullOrEmpty(message))message="Nearby artwork is unavailable. Library placement still works.";});
        }
        private void OnDestroy() { selection++; if(content)Destroy(content.gameObject); }
    }
}
