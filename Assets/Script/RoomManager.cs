using UnityEngine;
using Photon.Pun;

public class RoomManager : MonoBehaviourPunCallbacks
{
    // Appelé par le MasterClient pour faire quitter tout le monde
    [PunRPC]
    void ForceLeaveRoom(string message)
    {
        GameResultData.VictoryMessage = message;
        PhotonNetwork.LeaveRoom();
    }

    // Le Master appelle cette fonction
    public void MakeEveryoneLeave(string message)
    {
        photonView.RPC("ForceLeaveRoom", RpcTarget.All, message);
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Joueur sorti de la room !");
        UnityEngine.SceneManagement.SceneManager.LoadScene("End");
    }
}
