using System;
using System.Collections;
using UnityEngine;

namespace StreetArt
{
    public sealed class GpsLocation : MonoBehaviour
    {
        private double lastTimestamp;
        private float lastFixTime = -1000;
        private bool permissionDenied;
        public float AccuracyMeters => Input.location.lastData.horizontalAccuracy;
        public bool HasLocation => Input.location.status == LocationServiceStatus.Running &&
            AccuracyMeters > 0 && AccuracyMeters <= 5000 && Time.realtimeSinceStartup-lastFixTime < 120;
        public bool Ready => HasLocation && AccuracyMeters <= 100;
        public bool IsPrecise => Ready && AccuracyMeters <= 25;
        public double Latitude => Input.location.lastData.latitude;
        public double Longitude => Input.location.lastData.longitude;
        public string Status
        {
            get
            {
                if(permissionDenied) return "Location permission is off. Enable it in Android settings.";
                if(!Input.location.isEnabledByUser) return "Turn on Location in Android settings.";
                if(HasLocation)
                {
                    string accuracy=Mathf.CeilToInt(AccuracyMeters)+" m";
                    if(IsPrecise) return "Location ready · "+accuracy+" accuracy";
                    if(Ready) return "Approximate location · "+accuracy+" accuracy";
                    return "Location accuracy: "+accuracy+". Move outdoors to save a point.";
                }
                if(Input.location.status==LocationServiceStatus.Failed) return "Location unavailable. Try near a window or outdoors.";
                if(lastTimestamp>0) return "Refreshing location. Move near a window or outdoors.";
                return "Finding location. GPS may take longer indoors.";
            }
        }
        private void Update()
        {
            if(Input.location.status != LocationServiceStatus.Running) return;
            double stamp=Input.location.lastData.timestamp;
            if(stamp!=lastTimestamp) {lastTimestamp=stamp;lastFixTime=Time.realtimeSinceStartup;}
        }
        private IEnumerator Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
            {
                UnityEngine.Android.Permission.RequestUserPermissions(new[]{
                    UnityEngine.Android.Permission.CoarseLocation,UnityEngine.Android.Permission.FineLocation});
                float deadline=Time.realtimeSinceStartup+45;
                while(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation)
                    && Time.realtimeSinceStartup<deadline) yield return null;
                permissionDenied=!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.CoarseLocation);
                if(permissionDenied) yield break;
            }
#endif
            // Receive fresh fixes even when the phone is stationary.
            Input.location.Start(5,0);
            yield break;
        }
        private void OnDestroy(){Input.location.Stop();}
        public double Distance(double latitude,double longitude)
        {
            const double rad=Math.PI/180;
            double a=Math.Pow(Math.Sin((latitude-Latitude)*rad/2),2)+Math.Cos(Latitude*rad)*Math.Cos(latitude*rad)*Math.Pow(Math.Sin((longitude-Longitude)*rad/2),2);
            return 6371000*2*Math.Asin(Math.Sqrt(Math.Min(1,a)));
        }
    }
}
