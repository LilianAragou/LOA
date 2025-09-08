using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;

[DisallowMultipleComponent]
public class BoardPerspective : MonoBehaviourPunCallbacks
{

    [Header("Targets")]
    public Camera targetCamera;
    public Transform boardRoot;

    [Header("Keep pieces upright (optional)")]
    public bool keepPiecesUpright = true;
    [Tooltip("Where to search pieces (leave empty to search the whole scene).")]
    public Transform uprightSearchRoot;

    bool applied;
    int lastTeam = -2;

    // cache: visual transform -> initial local Z
    readonly Dictionary<Transform, float> _initialLocalZ = new Dictionary<Transform, float>();


    void Update()
    {
        bool myTeam = DetectMyTeam();
        ApplyCamera(myTeam);
        ApplyPions(myTeam);
    }

    bool DetectMyTeam()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.MyTeam != -1)
            return TurnManager.Instance.MyTeam == 1;
        if (PhotonNetwork.InRoom) return PhotonNetwork.IsMasterClient ? false : true;
        return false;
    }

    void ApplyCamera(bool myTeam)
    {
        var cam = targetCamera;
        if (!cam) return;
        cam.transform.rotation = Quaternion.Euler(0, 0, myTeam ? 180f : 0f);
    }
    void ApplyPions(bool myTeam)
    {
        SpriteRenderer[] srs = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var sr in srs)
        {
            if (!sr) continue;
            var piece = sr.GetComponentInParent<Piece>();
            if (!piece) continue;

            var t = piece.transform;

            // Si le joueur est bleu (team 1), on tourne toutes les pièces de 180°
            if (myTeam)
                t.localRotation = Quaternion.Euler(0, 0, 180f);
            else
                t.localRotation = Quaternion.identity;
        }
    }
}
