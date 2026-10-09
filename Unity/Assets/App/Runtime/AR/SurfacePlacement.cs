
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace StreetArt.AR
{
    public sealed class SurfacePlacement : MonoBehaviour
    {
        [SerializeField] private GameObject rig;
        [SerializeField] private ARSession session;
        [SerializeField] private ARRaycastManager raycasts;
        [SerializeField] private ARPlaneManager planes;
        [SerializeField] private ARAnchorManager anchors;
        [SerializeField] private Camera arCamera;
        private readonly List<ARRaycastHit> hits = new();
        private bool requested;
        private bool initializing;
        private bool initialized;
        private bool placingAnchor;
        private int generation;
        private ARAnchor anchor;
        private Transform content;
        private Pose pose;
        private string placementError;

        public Camera Camera => arCamera;
        public bool HasCameraImage => arCamera.gameObject.activeInHierarchy && arCamera.GetComponent<ARCameraBackground>().backgroundRenderingEnabled;
        public bool IsPlaced { get; private set; }
        public bool CanPlace { get; private set; }
        public bool Tracking => requested && ARSession.state == ARSessionState.SessionTracking && (!IsPlaced || anchor != null && anchor.trackingState == TrackingState.Tracking);
        public string Status { get; private set; } = "";

        public void Configure(GameObject xrRig, ARSession arSession, ARRaycastManager rays, ARPlaneManager planeManager, ARAnchorManager anchorManager, Camera camera)
        {
            rig = xrRig; session = arSession; raycasts = rays; planes = planeManager; anchors = anchorManager; arCamera = camera;
        }

        public void Begin(Transform board)
        {
            content = board;
            requested = true;
            IsPlaced = false;
            CanPlace = false;
            placementError = null;
            ARPresentation.ConfigureCamera(arCamera);
            content.gameObject.SetActive(false);
            Status = "Starting AR...";
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!initializing) StartCoroutine(Initialize());
#else
            Status = "AR placement requires an Android device.";
#endif
        }

        private IEnumerator Initialize()
        {
            initializing = true;
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                float deadline = Time.realtimeSinceStartup + 30;
                while (requested && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (!requested || !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                {
                    Status = "Camera access is required. Allow it in Android settings.";
                    initializing = false;
                    yield break;
                }
            }
            var manager = XRGeneralSettings.Instance?.Manager;
            if (manager == null)
            {
                Status = "AR configuration is unavailable.";
                initializing = false;
                yield break;
            }
            if (!initialized)
            {
                yield return manager.InitializeLoader();
                initialized = manager.activeLoader != null;
            }
            if (!requested) { ShutdownLoader(); initializing = false; yield break; }
            if (!initialized)
            {
                Status = "Could not start AR.";
                initializing = false;
                yield break;
            }
            manager.StartSubsystems();
            rig.SetActive(true);
            session.gameObject.SetActive(true);
            yield return ARSession.CheckAvailability();
            if (requested && ARSession.state == ARSessionState.NeedsInstall)
                yield return ARSession.Install();
            initializing = false;
            if (!requested) { End(); yield break; }
            Status = ARSession.state == ARSessionState.Unsupported || ARSession.state == ARSessionState.NeedsInstall
                ? "ARCore is unavailable on this device."
                : "Move slowly to find a surface.";
        }

        private void Update()
        {
            if (!requested || initializing || !initialized) return;
            if (IsPlaced)
            {
                // Keep the last anchored pose visible while tracking recovers. Input remains blocked.
                content.gameObject.SetActive(anchor != null);
                Status = Tracking ? "Model anchored. Save this GPS location." : "Tracking paused. Move slowly.";
                return;
            }
            CanPlace = !placingAnchor && Tracking && raycasts.Raycast(new Vector2(Screen.width * .5f, Screen.height * .5f), hits, TrackableType.PlaneWithinPolygon);
            if (CanPlace)
            {
                pose = hits[0].pose;
                ARPresentation.PlaceBoard(content, null, pose, arCamera.transform.position);
                Status = placementError ?? "Surface found. Place your model.";
            }
            else
            {
                if (!placingAnchor) content.gameObject.SetActive(false);
                if (Tracking) Status = placingAnchor ? "Anchoring model..." : "Aim at a flat surface.";
                else Status = "Tracking paused. Move slowly.";
            }
        }

        public async void Place()
        {
            if (!CanPlace || placingAnchor) return;
            placingAnchor = true;
            CanPlace = false;
            placementError = null;
            Status = "Anchoring model...";
            int requestGeneration = generation;
            try
            {
                var result = await anchors.TryAddAnchorAsync(pose);
                if (!requested || requestGeneration != generation)
                {
                    if (result.status.IsSuccess()) anchors.TryRemoveAnchor(result.value);
                    return;
                }
                if (!result.status.IsSuccess()) { placementError = Status = "Could not anchor here. Try another surface."; return; }
                anchor = result.value;
                ARPresentation.PlaceBoard(content, anchor.transform, new Pose(anchor.transform.position, anchor.transform.rotation), arCamera.transform.position);
                IsPlaced = true;
                Status = "Model anchored. Save this GPS location.";
                Debug.Log($"AR model placed at {content.position}, scale {content.lossyScale}, anchor tracking {anchor.trackingState}.");
                planes.enabled = false;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                if (requested && requestGeneration == generation) placementError = Status = "Placement failed. Please try again.";
            }
            finally { placingAnchor = false; }
        }

        public void Reposition()
        {
            generation++;
            placementError = null;
            if (content != null) { content.SetParent(null, true); content.gameObject.SetActive(false); }
            if (anchor != null) anchors.TryRemoveAnchor(anchor);
            anchor = null;
            IsPlaced = false;
            CanPlace = false;
            planes.enabled = true;
        }

        public void End()
        {
            requested = false;
            Reposition();
            rig.SetActive(false);
            session.gameObject.SetActive(false);
            if (!initializing) ShutdownLoader();
        }

        private void ShutdownLoader()
        {
            if (!initialized) return;
            XRGeneralSettings.Instance.Manager.StopSubsystems();
            XRGeneralSettings.Instance.Manager.DeinitializeLoader();
            initialized = false;
        }

        private void OnDestroy() { if (initialized) ShutdownLoader(); }
    }
}
