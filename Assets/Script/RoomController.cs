using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;  // ← Ajouté pour TextMeshPro

public class RoomController : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_InputField roomNameInput;  // TMP instead of InputField
    [SerializeField] private Button        createBtn;
    [SerializeField] private Button        joinBtn;

void Awake()
{
    Debug.Log("[RoomController] Awake()");      
}
    void Start()
{
     Debug.Log("[RoomController] Start()");
    Debug.Log($"[RoomController] fields: roomNameInput={(roomNameInput==null?"NULL":"OK")}, createBtn={(createBtn==null?"NULL":"OK")}");
    createBtn.onClick.AddListener(OnCreateRoom);
    joinBtn.onClick.AddListener(OnJoinRoom);
}


    public void OnCreateRoom()
{
    Debug.Log("[RoomController] OnCreateRoom click");  // ← tu dois voir ce log
    string name = roomNameInput.text.Trim();
    if (string.IsNullOrEmpty(name)) return;
    PhotonNetwork.CreateRoom(name, new RoomOptions { MaxPlayers = 2 });
}

    public void OnJoinRoom()
    {
        string roomName = roomNameInput.text.Trim();
        if (string.IsNullOrEmpty(roomName))
            return;

        PhotonNetwork.JoinRoom(roomName);
        Debug.Log($"[RoomController] Tentative de rejoindre « {roomName} »…");
    }

    public override void OnCreatedRoom()
    {
        Debug.Log("[RoomController] Room créée 🎉");
        PhotonNetwork.LoadLevel("Game");
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("[RoomController] Room rejointe ✅");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[RoomController] Échec création : {message}");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[RoomController] Échec jointure : {message}");
    }
}
