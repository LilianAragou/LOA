using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System;
using TMPro;

[RequireComponent(typeof(PhotonView))]
public class TurnManager : MonoBehaviourPunCallbacks
{
    public TextMeshProUGUI POtext;
    public static TurnManager Instance { get; private set; }

    // 0 = RED, 1 = BLUE
    public int CurrentPlayer { get; private set; } = 0;
    public int TurnIndex { get; private set; } = 1;
    public TextMeshProUGUI turnText;

    // Events
    public event Action OnTurnChanged;
    public event Action OnMatchStarted;
    public event Action OnMatchStopped;
    public event Action OnTurnStart;
    public event Action OnTurnEnd;
    public event Action<Piece, Piece> OnPieceCaptured;
    public event Action<Piece> OnPieceDestroyed;

    // Room props keys
    const string KEY_STARTED = "started";
    const string KEY_TURN_TEAM = "turnTeam";
    const string KEY_TURN_IDX = "turnIndex";
    const string KEY_RED_ACTOR = "redActor";
    const string KEY_BLU_ACTOR = "blueActor";

    private int _lastKnownTeam = 0;
    private bool _lastStarted = false;

    void Awake()
    {
        if (turnText != null) turnText.text = "Tour: Rouge";

        var mask = FindFirstObjectByType<BaronSamediMaskPiece>();
        if (POtext != null) POtext.text = $"PO: 0";

        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    #region Public API

    public bool RoomReady
    {
        get
        {
#if UNITY_EDITOR
            return PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null;
#else
            return PhotonNetwork.InRoom &&
                   PhotonNetwork.CurrentRoom != null &&
                   PhotonNetwork.CurrentRoom.PlayerCount >= 2;
#endif
        }
    }

#if UNITY_EDITOR
    // En solo dans l'éditeur on considère la partie comme démarrée pour faciliter le playtest
    public bool Started => true;
#else
    public bool Started => GetBoolProp(KEY_STARTED, false);
#endif

#if UNITY_EDITOR
    // En éditeur solo on force le tour au joueur rouge (utile pour debug / playtest)
    public bool IsMyTurn => true;
#else
    public bool IsMyTurn
    {
        get
        {
            if (!Started) return false;
            var local = PhotonNetwork.LocalPlayer;
            int red = GetIntProp(KEY_RED_ACTOR, -1);
            int blu = GetIntProp(KEY_BLU_ACTOR, -1);
            int mineTeam = (local != null && local.ActorNumber == red) ? 0 :
                           (local != null && local.ActorNumber == blu) ? 1 : -1;
            return mineTeam == CurrentPlayer;
        }
    }
#endif
#if UNITY_EDITOR
    // En éditeur solo on force le tour au joueur rouge (utile pour debug / playtest)
    public int MyTeam = 0;
#else
    public int MyTeam
    {
        get
        {
            var local = PhotonNetwork.LocalPlayer;
            int red = GetIntProp(KEY_RED_ACTOR, -1);
            int blu = GetIntProp(KEY_BLU_ACTOR, -1);
            if (local == null) return -1;
            if (local.ActorNumber == red) return 0;
            if (local.ActorNumber == blu) return 1;
            return -1;
        }
    }
#endif
    

    /// <summary>
    /// Fin de tour demandée par un client.
    /// - Si appelant = Master → on bypass IsMyTurn (le Master arbitre).
    /// - Sinon → on exige IsMyTurn et on envoie RPC au Master.
    /// </summary>
    public void RequestEndTurn()
    {
#if UNITY_EDITOR
        // En éditeur solo, on ne change pas le tour (toujours rouge)
        Debug.Log("[TurnManager] RequestEndTurn ignored in editor solo (always red).");
        return;
#endif

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[TurnManager] Master force end-turn.");
            RPC_EndTurn_Master(); // Appel direct
            return;
        }

        if (!IsMyTurn)
        {
            Debug.Log("[TurnManager] RequestEndTurn rejeté (pas mon tour).");
            return;
        }

        photonView.RPC(nameof(RPC_EndTurn_Master), RpcTarget.MasterClient);
    }

    public void EndTurn() => RequestEndTurn();

    public void NotifyCapture(Piece attacker, Piece victim)
        => OnPieceCaptured?.Invoke(attacker, victim);

    public void NotifyDestruction(Piece victim)
        => OnPieceDestroyed?.Invoke(victim);

    public void NotifyDestroyed(Piece victim)
        => OnPieceDestroyed?.Invoke(victim);

    #endregion

    #region Photon Callbacks

    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.IsMasterClient)
            MasterEnsureSetup();

        SyncFromRoomProps();
        _lastKnownTeam = CurrentPlayer;
        _lastStarted = Started;

#if UNITY_EDITOR
        // Si on est en éditeur et Master, s'assurer que les props existent (début du match en solo)
        if (PhotonNetwork.IsMasterClient)
            MasterEnsureSetup();
#endif
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        MasterEnsureSetup();

        if (RoomReady && !Started)
            MasterSetStarted(true);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (!RoomReady && Started)
            MasterSetStarted(false);

        if (PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.CurrentRoom.IsOpen = true;
            PhotonNetwork.CurrentRoom.IsVisible = true;
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        int prevTeam = CurrentPlayer;
        bool prevStart = _lastStarted;

        SyncFromRoomProps();
        bool nowStart = Started;

        if (propertiesThatChanged.ContainsKey(KEY_STARTED))
        {
            if (nowStart && !prevStart) OnMatchStarted?.Invoke();
            if (!nowStart && prevStart) OnMatchStopped?.Invoke();
            _lastStarted = nowStart;

            if (nowStart && !prevStart)
            {
                OnTurnChanged?.Invoke();
                OnTurnStart?.Invoke();
            }
        }

        if (propertiesThatChanged.ContainsKey(KEY_TURN_TEAM) ||
            propertiesThatChanged.ContainsKey(KEY_TURN_IDX))
        {
            if (prevTeam != CurrentPlayer)
            {
                OnTurnEnd?.Invoke();
                OnTurnChanged?.Invoke();
                OnTurnStart?.Invoke();
                _lastKnownTeam = CurrentPlayer;
            }
            else
            {
                OnTurnChanged?.Invoke();
            }
        }
    }

    #endregion

    #region Master authority

    private void MasterEnsureSetup()
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        var props = room.CustomProperties ?? new Hashtable();

        bool needAssign = !props.ContainsKey(KEY_RED_ACTOR) || !props.ContainsKey(KEY_BLU_ACTOR);
        if (needAssign)
        {
            int red = -1, blu = -1;
            var players = PhotonNetwork.PlayerList;
            if (players.Length > 0) red = players[0].ActorNumber;
            if (players.Length > 1) blu = players[1].ActorNumber;
            else blu = red; // Solo : le même acteur représente les deux équipes

            var newProps = new Hashtable {
                { KEY_RED_ACTOR, red },
                { KEY_BLU_ACTOR, blu }
            };
            room.SetCustomProperties(newProps);
        }

        if (!props.ContainsKey(KEY_TURN_TEAM) || !props.ContainsKey(KEY_TURN_IDX))
        {
            room.SetCustomProperties(new Hashtable {
                { KEY_TURN_TEAM, 0 },
                { KEY_TURN_IDX, 1 }
            });
        }

        if (!props.ContainsKey(KEY_STARTED))
            room.SetCustomProperties(new Hashtable { { KEY_STARTED, false } });
    }

    private void MasterSetStarted(bool started)
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        room.SetCustomProperties(new Hashtable { { KEY_STARTED, started } });

        if (started)
        {
            room.IsOpen = false;
            room.IsVisible = false;
            room.SetCustomProperties(new Hashtable { { KEY_TURN_TEAM, 0 }, { KEY_TURN_IDX, 1 } });
        }
        else
        {
            room.IsOpen = true;
            room.IsVisible = true;
        }
    }

    [PunRPC]
    private void RPC_EndTurn_Master()
    {
#if UNITY_EDITOR
        // En solo dans l'éditeur, on ne fait pas tourner le tour (toujours rouge)
        return;
#endif

        if (!PhotonNetwork.IsMasterClient || !Started) return;

        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        int team = GetIntProp(KEY_TURN_TEAM, 0);
        int index = GetIntProp(KEY_TURN_IDX, 1);

        int nextTeam = (team == 0) ? 1 : 0;
        int nextIndex = Mathf.Max(1, index + 1);

        room.SetCustomProperties(new Hashtable { { KEY_TURN_TEAM, nextTeam }, { KEY_TURN_IDX, nextIndex } });
    }

    #endregion

    #region Local sync helpers

    private void SyncFromRoomProps()
    {
        CurrentPlayer = GetIntProp(KEY_TURN_TEAM, 0);
        TurnIndex = GetIntProp(KEY_TURN_IDX, 1);

        if (turnText != null)
            turnText.text = $"Tour: {(CurrentPlayer == 0 ? "Rouge" : "Bleu")}";

        var mask = FindFirstObjectByType<BaronSamediMaskPiece>();
        if (POtext != null)
            POtext.text = $"PO: {mask?.GetShadowPoints()}";
    }

    private bool GetBoolProp(string key, bool fallback)
    {
        var rp = PhotonNetwork.CurrentRoom?.CustomProperties;
        if (rp == null) return fallback;
        if (rp.TryGetValue(key, out var v) && v is bool b) return b;
        return fallback;
    }

    private int GetIntProp(string key, int fallback)
    {
        var rp = PhotonNetwork.CurrentRoom?.CustomProperties;
        if (rp == null) return fallback;
        if (rp.TryGetValue(key, out var v) && v is int i) return i;
        return fallback;
    }

    #endregion
}
