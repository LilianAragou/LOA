using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

// Gestion de la room et rematch
public class RoomManager : MonoBehaviourPunCallbacks
{
    // Stocke le nom de la dernière room pour le rematch
    public static string LastRoomName;

    // Appelé par le MasterClient pour faire quitter tout le monde
    [PunRPC]
    void ForceLeaveRoom(string message)
    {
        GameResultData.VictoryMessage = message;

        // Sauvegarde le nom avant de quitter
        if (PhotonNetwork.CurrentRoom != null)
            LastRoomName = PhotonNetwork.CurrentRoom.Name;

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
