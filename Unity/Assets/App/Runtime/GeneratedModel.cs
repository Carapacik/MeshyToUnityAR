using GLTFast;
using UnityEngine;
namespace StreetArt
{
    public sealed class GeneratedModel : MonoBehaviour
    {
        private GltfImport import;
        public async System.Threading.Tasks.Task<bool> Load(string url)
        {
            import=new GltfImport();
            if(!await import.Load(url) || !this) return false;
            if(!await import.InstantiateMainSceneAsync(transform) || !this) return false;
            var renderers=GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) return false;
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            if(size<.001f) return false;
            var pivot=new GameObject("Normalized model").transform;
            pivot.SetParent(transform,false);
            var children=new System.Collections.Generic.List<Transform>(); foreach(Transform child in transform) if(child!=pivot) children.Add(child); foreach(var child in children) child.SetParent(pivot,true);
            pivot.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)/size;
            pivot.localScale=Vector3.one/size;
            return true;
        }
        private void OnDestroy() { import?.Dispose(); }
    }
}
