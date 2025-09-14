using UnityEngine;

public class SceneManager : MonoBehaviour
{
    public void ToStart()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
    }
    public void ToEnd()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("End");
    }
}
