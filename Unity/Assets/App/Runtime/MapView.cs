using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace StreetArt
{
    public sealed class MapView : MonoBehaviour
    {
        private VisualElement viewport, tiles, pins;
        private Label status;
        private GpsLocation gps;
        private ServerApi api;
        private Action<Placement> select;
        private readonly List<Texture2D> textures=new();
        private double latitude,longitude;
        private int zoom=17, revision;
        private Vector2 down;
        private bool dragging, hasCenter;
        private Coroutine refresh;
        public void Show(VisualElement parent,GpsLocation location,ServerApi server,Action<Placement> selected)
        {
            gps=location; api=server; select=selected;
            viewport=new VisualElement(); viewport.AddToClassList("map"); parent.Add(viewport);
            tiles=new VisualElement();tiles.StretchToParentSize();tiles.pickingMode=PickingMode.Ignore;viewport.Add(tiles);
            pins=new VisualElement(); pins.StretchToParentSize(); pins.pickingMode=PickingMode.Ignore; viewport.Add(pins);
            var toolbar=new VisualElement();toolbar.AddToClassList("map-toolbar");parent.Add(toolbar);
            toolbar.Add(new Button(()=>{zoom=Math.Min(19,zoom+1);Reload();}){text="+"});
            toolbar.Add(new Button(()=>{zoom=Math.Max(15,zoom-1);Reload();}){text="−"});
            toolbar.Add(new Button(()=>{if(gps && gps.HasLocation){latitude=gps.Latitude;longitude=gps.Longitude;hasCenter=true;Reload();}}){text="My location"});
            status=new Label("Waiting for GPS...");status.AddToClassList("hint");parent.Add(status);
            var attribution=new Button(()=>Application.OpenURL("https://www.openstreetmap.org/copyright")){text="© OpenStreetMap contributors"};
            attribution.AddToClassList("attribution");parent.Add(attribution);
            viewport.RegisterCallback<PointerDownEvent>(e=>{if(!hasCenter || e.target!=viewport)return;down=e.position;dragging=true;viewport.CapturePointer(e.pointerId);});
            viewport.RegisterCallback<PointerUpEvent>(e=>{
                if(!dragging)return;dragging=false;viewport.ReleasePointer(e.pointerId);
                Vector2 delta=(Vector2)e.position-down;
                var center=Tile(latitude,longitude,zoom);
                FromTile(center.x-delta.x/256,center.y-delta.y/256,zoom,out latitude,out longitude);Reload();
            });
            StartCoroutine(WaitForGps());
        }
        private IEnumerator WaitForGps()
        {
            while(gps && !gps.HasLocation){if(status!=null)status.text=gps.Status;yield return new WaitForSecondsRealtime(1);}
            if(!gps)yield break;
            latitude=gps.Latitude;longitude=gps.Longitude;hasCenter=true;Reload();
        }
        private void Reload()
        {
            if(!hasCenter || viewport==null || viewport.panel==null)return;
            revision++;
            if(refresh!=null)StopCoroutine(refresh);
            refresh=StartCoroutine(Render(revision));
        }
        private IEnumerator Render(int version)
        {
            yield return null;
            tiles.Clear();pins.Clear();foreach(var texture in textures)Destroy(texture);textures.Clear();
            status.text="Loading map...";
            float width=viewport.resolvedStyle.width,height=viewport.resolvedStyle.height;
            if(float.IsNaN(width)||width<1||height<1)yield break;
            var center=Tile(latitude,longitude,zoom);
            double left=center.x-width/512d,top=center.y-height/512d;
            int count=1<<zoom;bool failed=false;
            string cache=Path.Combine(Application.persistentDataPath,"tiles");Directory.CreateDirectory(cache);
            for(int y=(int)Math.Floor(top);y<=Math.Floor(top+height/256);y++)
            for(int x=(int)Math.Floor(left);x<=Math.Floor(left+width/256);x++)
            {
                if(y<0||y>=count)continue;
                int wrapped=(x%count+count)%count;
                string file=Path.Combine(cache,zoom+"-"+wrapped+"-"+y+".png");
                byte[] bytes=null;
                if(File.Exists(file)&&(DateTime.UtcNow-File.GetLastWriteTimeUtc(file)).TotalDays<7)bytes=File.ReadAllBytes(file);
                else
                {
                    using var request=UnityWebRequest.Get("https://tile.openstreetmap.org/"+zoom+"/"+wrapped+"/"+y+".png");
                    request.SetRequestHeader("User-Agent","StreetArtAI/2.0 (+https://github.com/Carapacik/MeshyToUnityAR)");
                    request.timeout=15; yield return request.SendWebRequest();
                    if(version!=revision)yield break;
                    if(request.result==UnityWebRequest.Result.Success){bytes=request.downloadHandler.data;File.WriteAllBytes(file,bytes);}
                    else if(File.Exists(file))bytes=File.ReadAllBytes(file);
                    else failed=true;
                }
                if(bytes==null)continue;
                var texture=new Texture2D(2,2); if(!texture.LoadImage(bytes)){Destroy(texture);continue;}textures.Add(texture);
                var tile=new VisualElement();tile.pickingMode=PickingMode.Ignore;
                tile.style.position=Position.Absolute;tile.style.left=(float)((x-left)*256);tile.style.top=(float)((y-top)*256);tile.style.width=256;tile.style.height=256;
                tile.style.backgroundImage=new StyleBackground(texture);tiles.Add(tile);
            }
            void Pin(double lat,double lon,string text,Action action)
            {
                var point=Tile(lat,lon,zoom); double dx=point.x-center.x;
                if(dx>count/2d)dx-=count;if(dx< -count/2d)dx+=count;
                var pin=new Button(action){text=text};pin.AddToClassList("map-pin");
                pin.style.left=(float)(width/2+dx*256)-22;pin.style.top=(float)((point.y-top)*256)-22;pins.Add(pin);
            }
            if(gps && gps.HasLocation)Pin(gps.Latitude,gps.Longitude,"You",()=>{});
#if DEBUG || UNITY_EDITOR
            if(previewPoints!=null)
            {
                foreach(var item in previewPoints) Pin(item.latitude,item.longitude,"AR",()=>{});
                status.text=failed?"Some map tiles could not be loaded.":"Moscow · Two example locations";
                yield break;
            }
#endif
            string query="/api/placements?latitude="+latitude.ToString("R",CultureInfo.InvariantCulture)+"&longitude="+longitude.ToString("R",CultureInfo.InvariantCulture)+"&radiusMeters=1000";
            yield return api.Request<PlacementsResponse>("GET",query,null,
                response=>{if(version!=revision)return;foreach(var item in response.items??Array.Empty<Placement>()){var p=item;Pin(p.latitude,p.longitude,"AR",()=>select(p));}
                    status.text=failed?"Some map tiles could not be loaded.":"Tap an AR marker to restore its model nearby.";},
                error=>{if(version==revision)status.text=error;});
        }
        private static (double x,double y) Tile(double lat,double lon,int zoom)
        {
            lat=Math.Clamp(lat,-85.05112878,85.05112878); double n=1<<zoom,r=lat*Math.PI/180;
            return ((lon+180)/360*n,(1-Math.Log(Math.Tan(r)+1/Math.Cos(r))/Math.PI)/2*n);
        }
        private static void FromTile(double x,double y,int zoom,out double lat,out double lon)
        { double n=1<<zoom;lon=((x/n*360)%360+360)%360-180;lat=Math.Atan(Math.Sinh(Math.PI*(1-2*Math.Clamp(y,0,n)/n)))*180/Math.PI; }
#if DEBUG || UNITY_EDITOR
        private Placement[] previewPoints;
        public void ShowDocumentationPreview(VisualElement parent)
        {
            Show(parent,null,null,_=>{});
            latitude=55.7520;longitude=37.6155;zoom=16;hasCenter=true;
            previewPoints=new[]{
                new Placement{modelId="rocket",latitude=55.7543,longitude=37.6122},
                new Placement{modelId="tree",latitude=55.7496,longitude=37.6188}
            };
            Reload();
        }
#endif
        private void OnDestroy(){revision++;foreach(var texture in textures)Destroy(texture);}
    }
}
