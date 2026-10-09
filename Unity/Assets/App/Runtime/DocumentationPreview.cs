#if DEBUG && UNITY_ANDROID
using UnityEngine;
namespace StreetArt
{
    public sealed partial class AppController
    {
        // Explicit launch extras for README captures; never alter GPS or server data.
        partial void OpenDocumentationPreview()
        {
            if(!Debug.isDebugBuild) return;
            using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
            using var intent=activity.Call<AndroidJavaObject>("getIntent");
            string screen=intent.Call<string>("getStringExtra","documentationScreen");
            if(screen=="library") ShowLibrary();
            if(screen=="map")
            {
                Layout();Text("Nearby artwork","title");
                mapView=gameObject.AddComponent<MapView>();
                mapView.ShowDocumentationPreview(body);
                Button("Back",Home);
            }
        }
    }
}
#endif
