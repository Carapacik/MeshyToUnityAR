using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Project.Scripts
{
    public class MenuNavigation : MonoBehaviour
    {
        public void GoMenu()
        {
            SceneManager.LoadScene("MenuScene", LoadSceneMode.Single);
        }

        public void GoProfile()
        {
        }

        public void GoMap()
        {
            SceneManager.LoadScene("MapScene", LoadSceneMode.Single);
        }

        public void GoCamera()
        {
            SceneManager.LoadScene("CameraScene", LoadSceneMode.Single);
        }
    }
}