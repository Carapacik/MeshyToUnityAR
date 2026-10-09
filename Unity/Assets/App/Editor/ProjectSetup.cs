using System;
using System.IO;
using System.Linq;
using StreetArt.AR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace StreetArt.Editor
{
    public static class ProjectSetup
    {
        private const string Settings="Assets/App/Settings";
        private const string ScenePath="Assets/App/Scenes/App.unity";
        [MenuItem("Street Art/Prepare project")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Settings); Directory.CreateDirectory("Assets/App/Scenes"); AssetDatabase.Refresh();
            PlayerSettings.companyName="CarapacikSpace"; PlayerSettings.productName="StreetArtAI"; PlayerSettings.bundleVersion="2.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.carapacik.streetartai");
            PlayerSettings.Android.bundleVersionCode=2;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevel37;
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android,ManagedStrippingLevel.Minimal);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.OpenGLES3});
            PlayerSettings.insecureHttpOption=InsecureHttpOption.DevelopmentOnly;
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait=false; PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            PlayerSettings.allowedAutorotateToLandscapeLeft=false; PlayerSettings.allowedAutorotateToLandscapeRight=false;
            PlayerSettings.defaultScreenWidth=430; PlayerSettings.defaultScreenHeight=932;
            PlayerSettings.SplashScreen.show=false;
            var player=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            player.FindProperty("activeInputHandler").intValue=1; player.ApplyModifiedPropertiesWithoutUndo();
            ConfigureRendering(); ConfigureXR(); CreateScene(); LibraryPreviews.Generate();
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            Debug.Log("STREET_ART_PREPARED");
        }
        private static T Asset<T>(string name) where T : ScriptableObject
        {
            string path = $"{Settings}/{name}.asset";
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value != null) return value;
            value = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        private static void ConfigureRendering()
        {
            var renderer = Asset<UniversalRendererData>("Mobile Renderer");
            if (!renderer.rendererFeatures.OfType<ARBackgroundRendererFeature>().Any())
            {
                var background = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                background.name = "AR Camera Background";
                AssetDatabase.AddObjectToAsset(background, renderer);
                renderer.rendererFeatures.Add(background);
                EditorUtility.SetDirty(renderer);
            }
            var pipeline = Asset<UniversalRenderPipelineAsset>("Mobile URP");
            var serialized = new SerializedObject(pipeline);
            var renderers = serialized.FindProperty("m_RendererDataList");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.Disabled;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 2;
            pipeline.renderScale = 1;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.shadowDistance = 0;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i);
                QualitySettings.renderPipeline = pipeline;
                QualitySettings.vSyncCount = 0;
            }
            EditorUtility.SetDirty(pipeline);
        }

        private static void ConfigureXR()
        {
            var perTarget = Asset<XRGeneralSettingsPerBuildTarget>("XR Targets");
            var general = Asset<XRGeneralSettings>("Android XR");
            var manager = Asset<XRManagerSettings>("Android XR Manager");
            general.Manager = manager;
            general.InitManagerOnStart = false;
            manager.automaticLoading = false;
            manager.automaticRunning = false;
            perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, general);
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);
            if (!XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android))
                throw new System.InvalidOperationException("Could not assign ARCore loader.");
            var arcore = Asset<ARCoreSettings>("ARCore");
            arcore.requirement = ARCoreSettings.Requirement.Optional;
            arcore.depth = ARCoreSettings.Requirement.Optional;
            ARCoreSettings.currentSettings = arcore;
            EditorUtility.SetDirty(general); EditorUtility.SetDirty(manager); EditorUtility.SetDirty(perTarget);
        }


        private static void CreateScene()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var sun=new GameObject("Sun").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=1.4f;
            sun.transform.rotation=Quaternion.Euler(45,-35,0); sun.shadows=LightShadows.None;
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.7f,.7f,.7f);
            var preview=new GameObject("Menu Camera").AddComponent<Camera>(); preview.clearFlags=CameraClearFlags.SolidColor; preview.backgroundColor=Color.white;
            preview.GetUniversalAdditionalCameraData().allowXRRendering=false;
            var rig=new GameObject("XR Origin"); rig.SetActive(false);
            var origin=rig.AddComponent<XROrigin>();
            var offset=new GameObject("Camera Offset"); offset.transform.SetParent(rig.transform,false);
            origin.CameraFloorOffsetObject=offset; origin.CameraYOffset=0; origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Device;
            var camera=new GameObject("AR Camera").AddComponent<Camera>(); camera.transform.SetParent(offset.transform,false); camera.tag="MainCamera";
            camera.gameObject.AddComponent<ARCameraManager>(); camera.gameObject.AddComponent<ARCameraBackground>();
            ARPresentation.ConfigureCamera(camera); origin.Camera=camera;
            var planes=rig.AddComponent<ARPlaneManager>(); planes.requestedDetectionMode=PlaneDetectionMode.Horizontal;
            var rays=rig.AddComponent<ARRaycastManager>(); var anchors=rig.AddComponent<ARAnchorManager>();
            var sessionObject=new GameObject("AR Session"); sessionObject.SetActive(false);
            var session=sessionObject.AddComponent<ARSession>(); sessionObject.AddComponent<ARInputManager>();
            var placement=new GameObject("Placement").AddComponent<SurfacePlacement>(); placement.Configure(rig,session,rays,planes,anchors,camera);
            var panel=Asset<PanelSettings>("App Panel"); panel.scaleMode=PanelScaleMode.ConstantPixelSize; panel.scale=1;
            panel.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/App/UI/AppTheme.tss"); EditorUtility.SetDirty(panel);
            var material=AssetDatabase.LoadAssetAtPath<Material>(Settings+"/Models.mat");
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));AssetDatabase.CreateAsset(material,Settings+"/Models.mat");}
            var document=new GameObject("App UI").AddComponent<UIDocument>(); document.panelSettings=panel;
            var app=new GameObject("App").AddComponent<AppController>(); app.Configure(document,placement,preview,material);
            EditorSceneManager.SaveScene(scene,ScenePath);
        }
        [MenuItem("Street Art/Build Android development APK")]
        public static void BuildAndroid()
        {
            Prepare(); Directory.CreateDirectory("Builds/Android");
            PlayerSettings.Android.useCustomKeystore=false; EditorUserBuildSettings.buildAppBundle=false;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/Android/StreetArtAI-2.0-dev.apk",target=BuildTarget.Android,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Android build failed.");
            Debug.Log("STREET_ART_BUILD_SUCCEEDED");
        }
    }
}
