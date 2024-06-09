using System;
using System.Collections;
using System.Collections.Generic;
using ARLocation;
using ARLocation.MapboxRoutes;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace _Project.Scripts
{
    public class MenuController : MonoBehaviour
    {
        public enum LineType
        {
            Route,
            NextTarget
        }

        [FormerlySerializedAs("MapboxToken")] public string mapboxToken =
            "EXAMPLE_MAPBOX_PUBLIC_TOKEN_NOT_VALID";

        [FormerlySerializedAs("ARSession")] public GameObject arSession;

        [FormerlySerializedAs("ARSessionOrigin")]
        public GameObject arSessionOrigin;

        [FormerlySerializedAs("RouteContainer")]
        public GameObject routeContainer;

        public Camera Camera;
        public Camera MapboxMapCamera;
        public MapboxRoute MapboxRoute;
        public AbstractRouteRenderer RoutePathRenderer;
        public AbstractRouteRenderer NextTargetPathRenderer;
        public Texture RenderTexture;
        public AbstractMap Map;
        [Range(100, 800)] public int MapSize = 400;
        public int MinimapLayer;
        public Material MinimapLineMaterial;
        public float BaseLineWidth = 2;
        public float MinimapStepSize = 0.5f;

        private readonly State s = new State();


        private GUIStyle _buttonStyle;

        private GUIStyle _errorLabelStyle;

        private Texture2D _separatorTexture;

        private GUIStyle _textFieldStyle;

        private GUIStyle _textStyle;
        private RouteResponse currentResponse;

        private Vector3 lastCameraPos;

        private GameObject minimapRouteGo;

        private AbstractRouteRenderer currentPathRenderer =>
            s.LineType == LineType.Route ? RoutePathRenderer : NextTargetPathRenderer;

        public LineType PathRendererType
        {
            get => s.LineType;
            set
            {
                if (value != s.LineType)
                {
                    currentPathRenderer.enabled = false;
                    s.LineType = value;
                    currentPathRenderer.enabled = true;

                    if (s.View == View.Route) MapboxRoute.RoutePathRenderer = currentPathRenderer;
                }
            }
        }

        private Texture2D SeparatorTexture
        {
            get
            {
                if (_separatorTexture == null)
                {
                    _separatorTexture = new Texture2D(1, 1);
                    _separatorTexture.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.15f));
                    _separatorTexture.Apply();
                }

                return _separatorTexture;
            }
        }

        private void Start()
        {
            NextTargetPathRenderer.enabled = false;
            RoutePathRenderer.enabled = false;
            ARLocationProvider.Instance.OnEnabled.AddListener(onLocationEnabled);
            Map.OnUpdated += OnMapRedrawn;
        }

        private void Update()
        {
            if (s.View == View.Route)
            {
                var cameraPos = Camera.main.transform.position;

                var arLocationRootAngle = ARLocationManager.Instance.gameObject.transform.localEulerAngles.y;
                var cameraAngle = Camera.main.transform.localEulerAngles.y;
                var mapAngle = cameraAngle - arLocationRootAngle;

                MapboxMapCamera.transform.eulerAngles = new Vector3(90, mapAngle, 0);

                if ((cameraPos - lastCameraPos).magnitude < MinimapStepSize) return;

                lastCameraPos = cameraPos;

                var location = ARLocationManager.Instance.GetLocationForWorldPosition(Camera.main.transform.position);

                Map.SetCenterLatitudeLongitude(new Vector2d(location.Latitude, location.Longitude));
                Map.UpdateMap();
            }
            else
            {
                MapboxMapCamera.transform.eulerAngles = new Vector3(90, 0, 0);
            }
        }

        private void OnEnable()
        {
            Debug.Log("Enable!!!!!!!!");
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            // ARLocationProvider.Instance.OnEnabled.RemoveListener(onLocationEnabled);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnGUI()
        {
            if (s.View == View.Route)
            {
                drawMap();
                return;
            }

            float h = Screen.height - MapSize;
            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(20, 20, 20, 20) }, GUILayout.MaxHeight(h),
                GUILayout.Height(h));

            var w = Screen.width;

            GUILayout.BeginVertical(GUILayout.MaxHeight(100));
            GUILayout.Label("Location Search", textStyle());
            GUILayout.BeginHorizontal(GUILayout.MaxHeight(100), GUILayout.MinHeight(100));
            s.QueryText = GUILayout.TextField(s.QueryText, textFieldStyle(), GUILayout.MinWidth(0.8f * w),
                GUILayout.MaxWidth(0.8f * w));

            if (GUILayout.Button("OK", buttonStyle(), GUILayout.MinWidth(0.15f * w), GUILayout.MaxWidth(0.15f * w)))
            {
                s.ErrorMessage = null;
                StartCoroutine(search());
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.BeginVertical();

            if (s.ErrorMessage != null) GUILayout.Label(s.ErrorMessage, errorLabelSytle());

            foreach (var r in s.Results)
                if (GUILayout.Button(r.place_name,
                        new GUIStyle(buttonStyle())
                            { alignment = TextAnchor.MiddleLeft, fontSize = 24, fixedHeight = 0.05f * Screen.height }))
                    StartRoute(r.geometry.coordinates[0]);


            GUILayout.EndVertical();
            // GUILayout.Label(RenderTexture);
            GUILayout.EndVertical();
            // GUILayout.Label(RenderTexture, GUILayout.Height(mapSize));
            drawMap();
        }

        private GUIStyle textStyle()
        {
            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label);
                _textStyle.fontSize = 48;
                _textStyle.fontStyle = FontStyle.Bold;
            }

            return _textStyle;
        }

        private GUIStyle textFieldStyle()
        {
            if (_textFieldStyle == null)
            {
                _textFieldStyle = new GUIStyle(GUI.skin.textField);
                _textFieldStyle.fontSize = 48;
            }

            return _textFieldStyle;
        }

        private GUIStyle errorLabelSytle()
        {
            if (_errorLabelStyle == null)
            {
                _errorLabelStyle = new GUIStyle(GUI.skin.label);
                _errorLabelStyle.fontSize = 24;
                _errorLabelStyle.fontStyle = FontStyle.Bold;
                _errorLabelStyle.normal.textColor = Color.red;
            }

            return _errorLabelStyle;
        }

        private GUIStyle buttonStyle()
        {
            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button);
                _buttonStyle.fontSize = 48;
            }

            return _buttonStyle;
        }

        private void OnMapRedrawn()
        {
            // Debug.Log("OnMapRedrawn");
            if (currentResponse != null) buildMinimapRoute(currentResponse);
        }

        private void onLocationEnabled(Location location)
        {
            Map.SetCenterLatitudeLongitude(new Vector2d(location.Latitude, location.Longitude));
            // Map.SetZoom(18);
            Map.UpdateMap();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"Scene Loaded: {scene.name}");
        }

        private void drawMap()
        {
            var tw = RenderTexture.width;
            var th = RenderTexture.height;

            var scale = MapSize / th;
            var newWidth = scale * tw;
            var x = Screen.width / 2 - newWidth / 2;
            float border;
            if (x < 0)
                border = -x;
            else
                border = 0;


            GUI.DrawTexture(new Rect(x, Screen.height - MapSize, newWidth, MapSize), RenderTexture,
                ScaleMode.ScaleAndCrop);
            GUI.DrawTexture(new Rect(0, Screen.height - MapSize - 20, Screen.width, 20), SeparatorTexture,
                ScaleMode.StretchToFill, false);

            var newZoom = GUI.HorizontalSlider(new Rect(0, Screen.height - 60, Screen.width, 60), Map.Zoom, 10, 22);

            if (newZoom != Map.Zoom)
            {
                Map.SetZoom(newZoom);
                Map.UpdateMap();
                // buildMinimapRoute(currentResponse);
            }
        }

        public void StartRoute(Location dest)
        {
            s.destination = dest;

            if (ARLocationProvider.Instance.IsEnabled)
                loadRoute(ARLocationProvider.Instance.CurrentLocation.ToLocation());
            else
                ARLocationProvider.Instance.OnEnabled.AddListener(loadRoute);
        }

        public void EndRoute()
        {
            ARLocationProvider.Instance.OnEnabled.RemoveListener(loadRoute);
            arSession.SetActive(false);
            arSessionOrigin.SetActive(false);
            routeContainer.SetActive(false);
            Camera.gameObject.SetActive(true);
            s.View = View.SearchMenu;
        }

        private void loadRoute(Location _)
        {
            if (s.destination != null)
            {
                var api = new MapboxApi(mapboxToken);
                var loader = new RouteLoader(api);
                StartCoroutine(
                    loader.LoadRoute(
                        new RouteWaypoint { Type = RouteWaypointType.UserLocation },
                        new RouteWaypoint { Type = RouteWaypointType.Location, Location = s.destination },
                        (err, res) =>
                        {
                            if (err != null)
                            {
                                s.ErrorMessage = err;
                                s.Results = new List<GeocodingFeature>();
                                return;
                            }

                            arSession.SetActive(true);
                            arSessionOrigin.SetActive(true);
                            routeContainer.SetActive(true);
                            Camera.gameObject.SetActive(false);
                            s.View = View.Route;

                            currentPathRenderer.enabled = true;
                            MapboxRoute.RoutePathRenderer = currentPathRenderer;
                            MapboxRoute.BuildRoute(res);
                            currentResponse = res;
                            buildMinimapRoute(res);
                        }));
            }
        }

        private void buildMinimapRoute(RouteResponse res)
        {
            var geo = res.routes[0].geometry;
            var vertices = new List<Vector3>();
            var indices = new List<int>();

            var worldPositions = new List<Vector2>();

            foreach (var p in geo.coordinates)
            {
                /* var pos = Mapbox.Unity.Utilities.Conversions.GeoToWorldPosition(
                        p.Latitude,
                        p.Longitude,
                        Map.CenterMercator,
                        Map.WorldRelativeScale
                        ); */

                // Mapbox.Unity.Utilities.Conversions.GeoToWorldPosition
                var pos = Map.GeoToWorldPosition(new Vector2d(p.Latitude, p.Longitude));
                worldPositions.Add(new Vector2(pos.x, pos.z));
                // worldPositions.Add(new Vector2((float)pos.x, (float)pos.y));
            }

            if (minimapRouteGo != null) minimapRouteGo.Destroy();

            minimapRouteGo = new GameObject("minimap route game object");
            minimapRouteGo.layer = MinimapLayer;

            var mesh = minimapRouteGo.AddComponent<MeshFilter>().mesh;

            var lineWidth = BaseLineWidth * Mathf.Pow(2.0f, Map.Zoom - 18);
            LineBuilder.BuildLineMesh(worldPositions, mesh, lineWidth);

            var meshRenderer = minimapRouteGo.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = MinimapLineMaterial;
        }

        private IEnumerator search()
        {
            var api = new MapboxApi(mapboxToken);

            yield return api.QueryLocal(s.QueryText, true);

            if (api.ErrorMessage != null)
            {
                s.ErrorMessage = api.ErrorMessage;
                s.Results = new List<GeocodingFeature>();
            }
            else
            {
                s.Results = api.QueryLocalResult.features;
            }
        }

        private enum View
        {
            SearchMenu,
            Route
        }

        [Serializable]
        private class State
        {
            public string QueryText = "";
            public List<GeocodingFeature> Results = new List<GeocodingFeature>();
            public View View = View.SearchMenu;
            public Location destination;
            public LineType LineType = LineType.NextTarget;
            public string ErrorMessage;
        }
    }
}