using Photon.Pun;
using UnityEngine;

public class NetworkGameManager : MonoBehaviourPunCallbacks
{
    void Start()
    {
        // Cette scène doit être déjà chargée par PhotonNetwork.LoadLevel()
        // On instancie ici les pions via le réseau :
        SpawnPlayerPawn();
    }

    void SpawnPlayerPawn()
    {
        // Calcule la position en fonction de ActorNumber
        Vector3 spawnPos = PhotonNetwork.LocalPlayer.ActorNumber == 1
            ? new Vector3(-2, 0, 0)
            : new Vector3(2, 0, 0);

        // Instanciation réseau du prefab (doit être dans Resources/PhotonPrefabs)
        PhotonNetwork.Instantiate(
            "PhotonPrefabs/Pion",
            spawnPos,
            Quaternion.identity
        );
    }
}
