using UnityEngine;
using Photon.Pun;
public class SceneManager : MonoBehaviour
{
    public string redWinsText  = "Victoire des rouges";
    public string blueWinsText = "Victoire des bleus";
    public string message;
    public void ToStart()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
    }
    public void ToEnd()
    {
        if (TurnManager.Instance.MyTeam == 0)
        {
            message = blueWinsText;
        }
        else
        {
            message = redWinsText;
        }
        FindObjectOfType<RoomManager>().MakeEveryoneLeave(message);
    }
}
