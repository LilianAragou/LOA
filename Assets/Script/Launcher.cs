using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class Launcher : MonoBehaviourPunCallbacks
{
    void Awake()
    {
        // Permet à tous les clients de charger automatiquement la même scène
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    void Start()
    {
        PhotonNetwork.GameVersion = "v1.0";
        PhotonNetwork.ConnectUsingSettings();
        Debug.Log("[Launcher] Connexion en cours...");
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[Launcher] Connecté au Master, j'essaie de rejoindre le lobby...");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("[Launcher] Lobby rejoint, l'UI de création/join est activée.");
        // Active ton Canvas de menu ici
    }
}
