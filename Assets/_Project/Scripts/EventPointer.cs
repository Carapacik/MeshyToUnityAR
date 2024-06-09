using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Project.Scripts
{
    public class EventPointer : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 50f;
        [SerializeField] private float amplitude = 2.0f;
        [SerializeField] private float frequency = 0.5f;


        private void Update()
        {
            RotatePointer();
        }

        private void OnMouseDown()
        {
            SceneManager.LoadScene("LikeScene", LoadSceneMode.Single);
        }

        private void RotatePointer()
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            transform.position = new Vector3(transform.position.x,
                Mathf.Sin(Time.fixedTime * Mathf.PI * frequency) * amplitude + 35, transform.position.z);
        }
    }
}