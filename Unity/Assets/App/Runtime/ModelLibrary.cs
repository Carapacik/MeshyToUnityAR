using UnityEngine;
namespace StreetArt
{
    public static class ModelLibrary
    {
        public static readonly LibraryItem[] Items =
        {
            new() {id="rocket",name="Little rocket",description="A tiny explorer ready for takeoff."},
            new() {id="tree",name="Pocket tree",description="A little green in an unexpected place."},
            new() {id="sculpture",name="Balance",description="A colorful stack of playful shapes."}
        };
        public static bool Contains(string id) => System.Array.Exists(Items,x=>x.id==id);
        public static GameObject Create(string id, Material baseMaterial)
        {
            var root=new GameObject(id);
            var colors=new System.Collections.Generic.Dictionary<Color,Material>();
            void Part(PrimitiveType type,Vector3 position,Vector3 scale,Color color,Quaternion rotation=default)
            {
                var part=GameObject.CreatePrimitive(type);
                if(Application.isPlaying) Object.Destroy(part.GetComponent<Collider>()); else Object.DestroyImmediate(part.GetComponent<Collider>());
                part.transform.SetParent(root.transform,false); part.transform.localPosition=position; part.transform.localScale=scale;
                part.transform.localRotation=rotation == default ? Quaternion.identity : rotation;
                if(!colors.TryGetValue(color,out var material)) { material=new Material(baseMaterial); if(material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color); if(material.HasProperty("_Color")) material.SetColor("_Color",color); colors.Add(color,material); }
                part.GetComponent<Renderer>().sharedMaterial=material;
            }
            Color green=new(.48f,.78f,.2f),dark=new(.12f,.23f,.12f),cream=new(.95f,.92f,.8f),orange=new(.95f,.47f,.2f);
            if(id=="tree")
            {
                Part(PrimitiveType.Cylinder,new(0,.24f,0),new(.1f,.24f,.1f),new(.4f,.24f,.12f));
                Part(PrimitiveType.Sphere,new(0,.65f,0),new(.65f,.7f,.65f),green);
                Part(PrimitiveType.Sphere,new(-.2f,.5f,.04f),new(.42f,.42f,.42f),dark);
                Part(PrimitiveType.Sphere,new(.22f,.55f,0),new(.4f,.45f,.4f),green);
            }
            else if(id=="rocket")
            {
                Part(PrimitiveType.Capsule,new(0,.5f,0),new(.32f,.43f,.32f),cream);
                Part(PrimitiveType.Sphere,new(0,.6f,-.155f),new(.15f,.15f,.035f),dark);
                Part(PrimitiveType.Cylinder,new(0,.1f,0),new(.18f,.07f,.18f),orange);
                for(int i=0;i<3;i++) { float a=i*120*Mathf.Deg2Rad; Part(PrimitiveType.Cube,new(Mathf.Sin(a)*.19f,.2f,Mathf.Cos(a)*.19f),new(.08f,.3f,.24f),green,Quaternion.Euler(0,i*120,0)); }
            }
            else
            {
                Part(PrimitiveType.Cylinder,new(0,.06f,0),new(.55f,.06f,.55f),cream);
                Part(PrimitiveType.Cube,new(0,.29f,0),new(.36f,.36f,.36f),green,Quaternion.Euler(0,25,0));
                Part(PrimitiveType.Sphere,new(.04f,.63f,0),new(.36f,.36f,.36f),orange);
                Part(PrimitiveType.Cube,new(-.06f,.89f,0),new(.22f,.22f,.22f),dark,Quaternion.Euler(0,20,16));
            }
            root.AddComponent<ModelMaterials>().Owned=System.Linq.Enumerable.ToArray(colors.Values);
            return root;
        }
    }
    public sealed class ModelMaterials : MonoBehaviour
    {
        public Material[] Owned;
        private void OnDestroy() { if(Owned!=null) foreach(var item in Owned) {if(Application.isPlaying) Destroy(item);else DestroyImmediate(item);} }
    }
}
