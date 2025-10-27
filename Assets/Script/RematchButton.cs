using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

public class RematchButton : MonoBehaviourPunCallbacks
{
    [SerializeField] private Button rematchBtn;
    [SerializeField] private byte maxPlayers = 2;

    private void Start()
    {
        if (rematchBtn != null)
            rematchBtn.onClick.AddListener(OnRematchClicked);
    }

    private void OnDestroy()
    {
        if (rematchBtn != null)
            rematchBtn.onClick.RemoveListener(OnRematchClicked);
    }

    private void OnRematchClicked()
    {
        if (string.IsNullOrEmpty(RoomManager.LastRoomName))
        {
            Debug.LogWarning("Pas de room précédente pour rematch !");
            return;
        }

        // Quitte la room actuelle si besoin
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = maxPlayers,
            IsOpen = true,
            IsVisible = true,
            PlayerTtl = 0,
            EmptyRoomTtl = 0
        };

        Debug.Log($"Tentative de JoinOrCreateRoom('{RoomManager.LastRoomName}')");
        PhotonNetwork.JoinOrCreateRoom(RoomManager.LastRoomName, options, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("[Rematch] Room rejointe ou créée, chargement de la scène Game...");
        PhotonNetwork.LoadLevel("Game");
    }
}
