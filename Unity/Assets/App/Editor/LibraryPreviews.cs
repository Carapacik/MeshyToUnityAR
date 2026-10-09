using System.IO;
using UnityEditor;
using UnityEngine;

namespace StreetArt.Editor
{
    public static class LibraryPreviews
    {
        // Bake the actual library geometry once, keeping the mobile grid inexpensive.
        public static void Generate()
        {
            const string folder = "Assets/App/Resources/ModelPreviews";
            Directory.CreateDirectory(folder);
            var material = new Material(Shader.Find("Standard"));
            foreach (var item in ModelLibrary.Items)
            {
                var preview = new PreviewRenderUtility();
                Texture2D texture = null;
                try
                {
                    var model = ModelLibrary.Create(item.id, material);
                    preview.AddSingleGO(model);
                    preview.camera.orthographic = true;
                    preview.camera.orthographicSize = .68f;
                    preview.camera.nearClipPlane = .01f;
                    preview.camera.farClipPlane = 20;
                    preview.camera.clearFlags = CameraClearFlags.SolidColor;
                    preview.camera.backgroundColor = Color.white;
                    preview.camera.transform.position = new Vector3(1.8f, 1.35f, -3f);
                    preview.camera.transform.LookAt(new Vector3(0, .5f, 0));
                    preview.lights[0].intensity = 1.3f;
                    preview.lights[0].transform.rotation = Quaternion.Euler(35, -30, 0);
                    preview.lights[1].intensity = .6f;
                    preview.ambientColor = new Color(.65f, .65f, .65f);
                    preview.BeginStaticPreview(new Rect(0, 0, 384, 384));
                    preview.Render(false);
                    texture = preview.EndStaticPreview();
                    File.WriteAllBytes(folder + "/" + item.id + ".png", texture.EncodeToPNG());
                }
                finally
                {
                    if (texture) Object.DestroyImmediate(texture);
                    preview.Cleanup();
                }
            }
            Object.DestroyImmediate(material);
            AssetDatabase.Refresh();
            foreach (var item in ModelLibrary.Items)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(folder + "/" + item.id + ".png");
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
    }
}
