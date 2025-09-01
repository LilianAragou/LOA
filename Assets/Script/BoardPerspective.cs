using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[DisallowMultipleComponent]
public class BoardPerspective : MonoBehaviourPunCallbacks
{
    public enum Mode { RotateCamera, RotateBoardRoot }
    public Mode mode = Mode.RotateCamera;

    [Header("Targets")]
    public Camera targetCamera;
    public Transform boardRoot;

    [Header("Keep sprites upright (optional)")]
    public bool keepPiecesUpright = true;
    [Tooltip("Where to search pieces (leave empty to search the whole scene).")]
    public Transform uprightSearchRoot;

    bool applied;
    int lastTeam = -2;

    // cache: visual transform -> initial local Z
    readonly Dictionary<Transform, float> _initialLocalZ = new Dictionary<Transform, float>();

    void Awake()
    {
        if (mode == Mode.RotateBoardRoot && boardRoot == null)
            boardRoot = transform;
    }

    void Start()                       { BuildUprightList(); TryApply("Start"); }
    public override void OnJoinedRoom(){ BuildUprightList(); TryApply("OnJoinedRoom"); }

    void OnEnable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnMatchStarted += TryApply;
            TurnManager.Instance.OnTurnChanged  += TryApply;
        }
        Invoke(nameof(BuildUprightList), 0.5f); // catch late spawns
        Invoke(nameof(BuildUprightList), 1.5f);
    }
    void OnDisable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnMatchStarted -= TryApply;
            TurnManager.Instance.OnTurnChanged  -= TryApply;
        }
    }

    void TryApply() => TryApply("event");
    void TryApply(string where)
    {
        int myTeam = DetectMyTeam();
        if (myTeam < 0) return;
        if (applied && myTeam == lastTeam) return;

        if (mode == Mode.RotateCamera) ApplyCamera(myTeam);
        else                           ApplyBoard(myTeam);

        ApplyUpright(myTeam);
        lastTeam = myTeam;
        applied  = true;
        // Debug.Log($"[BoardPerspective] applied {mode} for team={myTeam} ({where})");
    }

    int DetectMyTeam()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.MyTeam != -1)
            return TurnManager.Instance.MyTeam;
        if (PhotonNetwork.InRoom) return PhotonNetwork.IsMasterClient ? 0 : 1;
        return 0;
    }

    void ApplyCamera(int myTeam)
    {
        var cam = targetCamera ? targetCamera : Camera.main;
        if (!cam) return;
        cam.transform.rotation = Quaternion.Euler(0, 0, myTeam == 1 ? 180f : 0f);
    }

    void ApplyBoard(int myTeam)
    {
        if (!boardRoot) return;
        boardRoot.localRotation = Quaternion.Euler(0, 0, myTeam == 1 ? 180f : 0f);
    }

    void BuildUprightList()
    {
        if (!keepPiecesUpright) return;

        _initialLocalZ.Clear();

        SpriteRenderer[] srs;
        if (uprightSearchRoot)
            srs = uprightSearchRoot.GetComponentsInChildren<SpriteRenderer>(true);
        else
            srs = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var sr in srs)
        {
            if (!sr) continue;
            // only treat sprites that belong to a Piece
            if (!sr.GetComponentInParent<Piece>()) continue;

            var t = sr.transform;
            if (!_initialLocalZ.ContainsKey(t))
                _initialLocalZ[t] = t.localEulerAngles.z;
        }
    }

    void ApplyUpright(int myTeam)
    {
        if (!keepPiecesUpright) return;

        float camZ = (myTeam == 1) ? 180f : 0f;
        foreach (var kv in _initialLocalZ)
        {
            var t = kv.Key;
            if (!t) continue;
            float baseZ = kv.Value;
            t.localRotation = Quaternion.Euler(0f, 0f, baseZ - camZ);
        }
    }
}
