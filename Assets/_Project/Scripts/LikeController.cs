using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace _Project.Scripts
{
    public class LikeController : MonoBehaviour
    {
        [FormerlySerializedAs("LikesCount")] public int likesCount;
        [FormerlySerializedAs("TextObject")] public Text textObject;


        private void Update()
        {
            textObject.text = likesCount.ToString();
        }

        public void AddLike()
        {
            likesCount = likesCount + 1;
        }

        public void RemoveLike()
        {
            if (likesCount == 0) return;
            likesCount = likesCount - 1;
        }
    }
}