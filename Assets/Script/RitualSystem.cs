using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Linq;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Système de rituels :
///  - Baron : Résurrection, Vol de Coup (steal), PO (via masque)
///  - Ogoun : Marque (rituel 1), Boost passif de mouvement (rituel 2), extra-coup géré côté BoardManager
///  - Verrou d'équipe (évolutions et rituels bloqués pendant N tours globaux)
///  - Gestion PR locales (Points de rituel)
///  - Intégration Carreaux PO : gain de PO lorsque les conditions sont réunies (appel depuis BoardManager)
/// </summary>
public class RitualSystem : MonoBehaviourPun
{
    public static RitualSystem Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject panelOgoun;   // équipe ROUGE (team=0)
    public GameObject panelBaron;   // équipe BLEUE (team=1)
    [Tooltip("Ancien panneau unique (fallback si panelOgoun/panelBaron non assignés)")]
    public GameObject ritualPanel;

    [Header("Boutons (Baron)")]
    public Button btnResurrect;
    public Button btnStealMove;
    public Button btnPassTurn;

    [Header("Boutons (Ogoun)")]
    public Button btnOgounMark;   // Rituel #1 (marquer)
    public Button btnOgounBoost;  // Rituel #2 (boost passif)

    [Header("Coûts (PR / PO)")]
    public int costResurrectPO     = 3; // Points d’ombre (Baron)
    public int costResurrectRitual = 1; // Points de rituel
    public int costStealMoveRitual = 2;
    public int costOgounMarkRitual = 1;
    public int costOgounBoostRitual = 1;

    [Header("Cap d'unités vivantes")]
    [SerializeField] private int maxUnitsPerTeam = 7;

    [Header("Points de rituel (locaux au client)")]
    public int maxRitualPoints = 3;
    private int currentRitualPoints;
    public TextMeshProUGUI ritualPointsText;

    private bool isSpawnMode = false;     // Baron: choix de la case de résurrection
    private BaronSamediMaskPiece currentMask; // masque actif côté Baron

    // Vol de coup
    public bool IsStealActive { get; private set; } = false;
    const string ROOM_PROP_STEAL = "STEAL_TEAM"; // -1 inactif, 0 red, 1 blue

    // Détection d’équipe locale
    int myTeam = -1; // 0=rouge (Ogoun), 1=bleu (Baron)

    // ========= Ogoun Rituel 1 (marque) =========
    class OgounMarkState
    {
        public bool active;
        public int targetViewId;
        public int turnsLeft; // décrémenté uniquement au début des tours d’Ogoun
    }
    OgounMarkState ogounMark = new OgounMarkState();

    // ========= Ogoun Rituel 2 (boost passif de mouvement) =========
    class OgounBoostState
    {
        public bool active;
        public int turnsLeft; // décrémenté uniquement au début des tours d’Ogoun
    }
    OgounBoostState ogounBoost = new OgounBoostState();

    // ========= Verrou d’actions (évo/rituels) =========
    // 3 tours “globaux” = décrémenté à CHAQUE début de tour (rouge OU bleu).
    int[] teamLockGlobalTurns = new int[2]; // [0]=rouge, [1]=bleu
    public bool IsTeamLocked(int team)
        => team >= 0 && team < 2 && teamLockGlobalTurns[team] > 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        SafeSetActive(panelOgoun, false);
        SafeSetActive(panelBaron, false);
        SafeSetActive(ritualPanel, false);
    }

    void Start()
    {
        currentRitualPoints = maxRitualPoints;
        UpdateTextRitualPoints();

        // Hook UI
        if (btnResurrect) btnResurrect.onClick.AddListener(OnResurrectClicked);
        if (btnStealMove)  btnStealMove.onClick.AddListener(OnStealMoveClicked);
        if (btnPassTurn)   btnPassTurn.onClick.AddListener(OnPassTurnClicked);
        if (btnOgounMark)  btnOgounMark.onClick.AddListener(OnOgounMarkClicked);
        if (btnOgounBoost) btnOgounBoost.onClick.AddListener(OnOgounBoostClicked);

        // Events
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStart    += OnTurnStarted;
            TurnManager.Instance.OnTurnChanged  += OnTurnChanged;
            TurnManager.Instance.OnTurnEnd      += OnTurnEnded;
            TurnManager.Instance.OnMatchStarted += OnMatchStarted;

            // Master only (mais on se souscrit partout, les méthodes recheckent Master)
            TurnManager.Instance.OnPieceCaptured  += OnAnyPieceCaptured;
            TurnManager.Instance.OnPieceDestroyed += OnAnyPieceDestroyed;
        }

        EnsureRoomPropInitialized();
        DetectMyTeam();
        if (myTeam == 1) currentMask = FindMyBaronMask();

        ApplyPanelVisibility();
        UpdateButtonStates();
    }
    void UpdateTextRitualPoints()
    {
        ritualPointsText.text = $"PR: {currentRitualPoints}/{maxRitualPoints}";
    }

    void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStart    -= OnTurnStarted;
            TurnManager.Instance.OnTurnChanged  -= OnTurnChanged;
            TurnManager.Instance.OnTurnEnd      -= OnTurnEnded;
            TurnManager.Instance.OnMatchStarted -= OnMatchStarted;

            TurnManager.Instance.OnPieceCaptured  -= OnAnyPieceCaptured;
            TurnManager.Instance.OnPieceDestroyed -= OnAnyPieceDestroyed;
        }
    }

    // ─────────────────────────────────────────────
    // Utilitaires UI/Team
    // ─────────────────────────────────────────────
    void DetectMyTeam()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.MyTeam != -1)
            myTeam = TurnManager.Instance.MyTeam;
        else if (PhotonNetwork.InRoom)
            myTeam = PhotonNetwork.IsMasterClient ? 0 : 1;
        else
            myTeam = 0; // offline
    }

    void ApplyPanelVisibility()
    {
        bool haveSplit = (panelOgoun != null || panelBaron != null);
        if (haveSplit)
        {
            SafeSetActive(panelOgoun, myTeam == 0);
            SafeSetActive(panelBaron, myTeam == 1);
            SafeSetActive(ritualPanel, false);
        }
        else SafeSetActive(ritualPanel, true);
    }

    void SafeSetActive(GameObject go, bool state)
    {
        if (go != null && go.activeSelf != state) go.SetActive(state);
    }

    void EnsureRoomPropInitialized()
    {
        if (!PhotonNetwork.InRoom) return;
        var room = PhotonNetwork.CurrentRoom;
        if (room.CustomProperties == null || !room.CustomProperties.ContainsKey(ROOM_PROP_STEAL))
        {
            var tb = new ExitGames.Client.Photon.Hashtable { { ROOM_PROP_STEAL, -1 } };
            room.SetCustomProperties(tb);
        }
    }

    BaronSamediMaskPiece FindMyBaronMask()
    {
        var masks = Object.FindObjectsByType<BaronSamediMaskPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var m in masks)
            if (m != null && !m.isRed) return m; // team1 => isRed=false
        return null;
    }

    public void SetCurrentMask(BaronSamediMaskPiece mask) { currentMask = mask; UpdateButtonStates(); }
    public void SetCurrentMask(Piece maskPiece)           { SetCurrentMask(maskPiece as BaronSamediMaskPiece); }

    // ─────────────────────────────────────────────
    // Gestion du tour
    // ─────────────────────────────────────────────
    void OnTurnStarted()
    {
        // 2 tours d’Ogoun → décrément AU DÉBUT des tours d’Ogoun
        if (TurnManager.Instance != null && TurnManager.Instance.CurrentPlayer == 0)
        {
            if (ogounMark.active)
            {
                ogounMark.turnsLeft = Mathf.Max(0, ogounMark.turnsLeft - 1);
                if (ogounMark.turnsLeft == 0)
                {
                    // fin de marque → retirer le visuel
                    photonView.RPC(nameof(RPC_ApplyMarkedVisual), RpcTarget.All, ogounMark.targetViewId, false);
                    ClearOgounMarkLocal();
                }
            }

            if (ogounBoost.active)
            {
                ogounBoost.turnsLeft = Mathf.Max(0, ogounBoost.turnsLeft - 1);
                if (ogounBoost.turnsLeft == 0)
                    ogounBoost.active = false;
            }
        }

        // “3 tours globaux” → on décrémente les 2 compteurs à chaque début de tour
        for (int t = 0; t < 2; t++)
            if (teamLockGlobalTurns[t] > 0)
                teamLockGlobalTurns[t]--;

        DetectMyTeam();
        if (myTeam == 1 && currentMask == null) currentMask = FindMyBaronMask();

        ApplyPanelVisibility();
        UpdateButtonStates();
    }

    void OnTurnChanged()
    {
        DetectMyTeam();
        if (myTeam == 1 && currentMask == null) currentMask = FindMyBaronMask();
        ApplyPanelVisibility();
        UpdateButtonStates();
    }

    void OnMatchStarted()
    {
        DetectMyTeam();
        if (myTeam == 1) currentMask = FindMyBaronMask();
        ApplyPanelVisibility();
        UpdateButtonStates();
    }

    void OnTurnEnded()
    {
        isSpawnMode = false;
        BoardManager.Instance?.ClearHighlights();

        // si le vol de tour ne s'applique pas à MON équipe au prochain tour, on le coupe
        bool keepSteal = false;
        if (PhotonNetwork.InRoom && TurnManager.Instance != null)
        {
            var room = PhotonNetwork.CurrentRoom;
            if (room != null && room.CustomProperties != null && room.CustomProperties.ContainsKey(ROOM_PROP_STEAL))
            {
                int stealTeam = (int)room.CustomProperties[ROOM_PROP_STEAL];
                if (stealTeam == TurnManager.Instance.MyTeam) keepSteal = true;
            }
        }
        if (!keepSteal) CancelStealModeLocal();

        UpdateButtonStates();
    }

    // ─────────────────────────────────────────────
    // BARON : Résurrection
    // ─────────────────────────────────────────────
    void OnResurrectClicked()
    {
        if (myTeam != 1 || currentMask == null) return;
        var tm = TurnManager.Instance;
        if (tm == null || !tm.Started || !tm.IsMyTurn) return;
        if (IsTeamLocked(myTeam)) return;

        if (GetTeamAliveCount(currentMask.isRed) >= maxUnitsPerTeam) return;
        if (currentMask.GetShadowPoints() < costResurrectPO) return;
        if (currentRitualPoints < costResurrectRitual) return;

        isSpawnMode = true;
        // feedback visuel
        BoardManager.Instance?.ShowAdjacentTiles(currentMask.currentGridPos);
        InputManager.Instance?.EnterSpawnMode();
    }

    public void ConfirmSpawn(Vector2Int pos)
    {
        if (myTeam != 1 || !isSpawnMode || currentMask == null) return;
        int maskViewId = currentMask.photonView ? currentMask.photonView.ViewID : 0;
        photonView.RPC(nameof(RPC_RequestResurrect_Master), RpcTarget.MasterClient, maskViewId, pos.x, pos.y);
    }

    [PunRPC]
    private void RPC_RequestResurrect_Master(int maskViewId, int x, int y, PhotonMessageInfo info = default)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (TurnManager.Instance == null || !TurnManager.Instance.Started) return;

        var maskPV = PhotonNetwork.GetPhotonView(maskViewId);
        if (maskPV == null) return;
        var mask = maskPV.GetComponent<BaronSamediMaskPiece>();
        if (mask == null) return;

        int expectedTeam = mask.isRed ? 0 : 1;
        if (TurnManager.Instance.CurrentPlayer != expectedTeam) return;
        if (IsTeamLocked(expectedTeam)) return;

        if (GetTeamAliveCount(mask.isRed) >= maxUnitsPerTeam) return;

        var board = BoardManager.Instance;
        var target = new Vector2Int(x, y);
        var tile = board.GetTileAt(target);
        if (tile == null || tile.currentOccupant != null) return;
        if (!IsAdjacent(target, mask.currentGridPos)) return;
        if (mask.GetShadowPoints() < costResurrectPO) return;

        photonView.RPC(nameof(RPC_SpendShadowPoints), RpcTarget.All, mask.isRed, costResurrectPO);

        string prefabKey = mask.isRed ? "Spirit_Red" : "Spirit_Blue";
        PhotonNetwork.InstantiateRoomObject(
            "PhotonPrefabs/" + prefabKey,
            tile.transform.position,
            Quaternion.identity,
            0,
            new object[] { x, y, mask.isRed }
        );

        photonView.RPC(nameof(RPC_EndSpawnModeClient), RpcTarget.All);
        TurnManager.Instance.RequestEndTurn();
    }

    [PunRPC]
    private void RPC_EndSpawnModeClient()
    {
        isSpawnMode = false;
        BoardManager.Instance?.ClearHighlights();
        currentRitualPoints = Mathf.Max(0, currentRitualPoints - costResurrectRitual);
        UpdateButtonStates();
        UpdateTextRitualPoints();
    }

    [PunRPC]
    private void RPC_SpendShadowPoints(bool isRedTeam, int amount)
    {
        var barons = Object.FindObjectsByType<BaronSamediMaskPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var b in barons)
        {
            if (b.isRed == isRedTeam) { b.SpendShadowPoints(amount); break; }
        }
    }

    // ─────────────────────────────────────────────
    // BARON : Vol de coup
    // ─────────────────────────────────────────────
    void OnStealMoveClicked()
    {
        if (myTeam != 1) return;
        var tm = TurnManager.Instance;
        if (tm == null || !tm.Started || !tm.IsMyTurn) return;
        if (currentRitualPoints < costStealMoveRitual) return;
        if (IsTeamLocked(myTeam)) return;

        photonView.RPC(nameof(RPC_RequestSteal_Master), RpcTarget.MasterClient, myTeam);
        tm.RequestEndTurn(); // passe le tour immédiatement
    }

    [PunRPC]
    private void RPC_RequestSteal_Master(int requesterTeam, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (TurnManager.Instance == null || !TurnManager.Instance.Started) return;

        var room = PhotonNetwork.CurrentRoom;
        int steal = room.CustomProperties != null && room.CustomProperties.ContainsKey(ROOM_PROP_STEAL)
            ? (int)room.CustomProperties[ROOM_PROP_STEAL] : -1;

        if (steal != -1) return; // déjà prêt

        var tb = new ExitGames.Client.Photon.Hashtable { { ROOM_PROP_STEAL, requesterTeam } };
        room.SetCustomProperties(tb);

        photonView.RPC(nameof(RPC_ActivateStealClient), RpcTarget.All, requesterTeam);
    }

    [PunRPC]
    private void RPC_ActivateStealClient(int teamAllowed)
    {
        if (TurnManager.Instance != null && TurnManager.Instance.MyTeam == teamAllowed)
        {
            currentRitualPoints = Mathf.Max(0, currentRitualPoints - costStealMoveRitual);
            IsStealActive = true;
            InputManager.Instance?.EnterStealMode();
        }
        UpdateButtonStates();
        UpdateTextRitualPoints();
    }

    [PunRPC]
    public void RPC_EndStealModeClient()
    {
        CancelStealModeLocal();
        UpdateButtonStates();
    }

    void CancelStealModeLocal()
    {
        if (!IsStealActive) return;
        IsStealActive = false;
        InputManager.Instance?.ExitStealMode();
    }

    // ─────────────────────────────────────────────
    // OGOUN : Rituel #1 – Marquer un esprit
    // ─────────────────────────────────────────────
    void OnOgounMarkClicked()
    {
        if (myTeam != 0) return;
        var tm = TurnManager.Instance;
        if (tm == null || !tm.Started || !tm.IsMyTurn) return;
        if (IsTeamLocked(myTeam)) return;
        if (currentRitualPoints < costOgounMarkRitual) return;

        // déjà une marque active ?
        if (ogounMark.active && ogounMark.turnsLeft > 0) return;

        var candidates = ComputeValidOgounMarkTargets();
        if (candidates.Count == 0) return;

        // On laisse l’InputManager gérer l’UX (surlignages + clic)
        InputManager.Instance?.EnterOgounMarkMode();
    }

    public void ConfirmOgounMark(Piece target)
    {
        if (myTeam != 0) return;
        var tm = TurnManager.Instance;
        if (tm == null || !tm.Started || !tm.IsMyTurn) return;
        if (IsTeamLocked(myTeam)) return;
        if (currentRitualPoints < costOgounMarkRitual) return;
        if (target == null || target.photonView == null) return;
        if (!IsValidOgounMarkTarget(target)) return;

        photonView.RPC(nameof(RPC_RequestOgounMark_Master), RpcTarget.MasterClient, target.photonView.ViewID);
    }

    [PunRPC]
    private void RPC_RequestOgounMark_Master(int targetViewId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (TurnManager.Instance == null || !TurnManager.Instance.Started) return;

        int requesterTeam = info.Sender != null && info.Sender.IsMasterClient ? 0 : 1;
        if (requesterTeam != 0) return;
        if (TurnManager.Instance.CurrentPlayer != 0) return;

        // une seule marque active à la fois
        if (ogounMark.active && ogounMark.turnsLeft > 0) return;

        var pv = PhotonNetwork.GetPhotonView(targetViewId);
        if (pv == null) return;
        var piece = pv.GetComponent<Piece>();
        if (piece == null) return;

        if (piece.isRed) return;       // cible doit être ennemie (bleue)
        if (!IsSpirit(piece)) return;  // uniquement esprit
        if (!IsValidOgounMarkTarget(piece)) return;

        // Active pour tous : turns=2 (tours d’Ogoun)
        photonView.RPC(nameof(RPC_ActivateOgounMarkClient), RpcTarget.All, targetViewId, 2, 0);

        // Passe le tour tout de suite
        TurnManager.Instance.RequestEndTurn();
    }

    [PunRPC]
    private void RPC_ActivateOgounMarkClient(int targetViewId, int turns, int requesterTeam)
    {
        // retire le visuel d’éventuelle ancienne marque (sécurité)
        if (ogounMark.active && ogounMark.targetViewId != 0)
            photonView.RPC(nameof(RPC_ApplyMarkedVisual), RpcTarget.All, ogounMark.targetViewId, false);

        ogounMark.active = true;
        ogounMark.targetViewId = targetViewId;
        ogounMark.turnsLeft = Mathf.Max(0, turns);

        // visu : opacité 80%
        photonView.RPC(nameof(RPC_ApplyMarkedVisual), RpcTarget.All, targetViewId, true);

        // dépense PR seulement pour Ogoun local
        if (TurnManager.Instance != null && TurnManager.Instance.MyTeam == requesterTeam)
            currentRitualPoints = Mathf.Max(0, currentRitualPoints - costOgounMarkRitual);

        UpdateButtonStates();
        UpdateTextRitualPoints();
        Debug.Log($"[OGOUN] Marque activée sur ViewID={targetViewId} (2 tours d’Ogoun).");
    }

    [PunRPC]
    private void RPC_ApplyMarkedVisual(int viewId, bool enable)
    {
        var pv = PhotonNetwork.GetPhotonView(viewId);
        if (pv == null) return;
        var go = pv.gameObject;
        if (go == null) return;

        // On agit sur tous les SpriteRenderer enfants
        var sprites = go.GetComponentsInChildren<SpriteRenderer>(true);
        float alpha = enable ? 0.8f : 1f;
        foreach (var sr in sprites)
        {
            var c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }

    void ClearOgounMarkLocal()
    {
        ogounMark.active = false;
        ogounMark.targetViewId = 0;
        ogounMark.turnsLeft = 0;
    }

    // Master: si la victime est la cible marquée, appliquer le verrou 3 tours globaux
    void OnAnyPieceCaptured(Piece attacker, Piece victim)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        TryTriggerLockFromVictim(victim);
    }
    void OnAnyPieceDestroyed(Piece victim)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        TryTriggerLockFromVictim(victim);
    }

    void TryTriggerLockFromVictim(Piece victim)
    {
        if (victim == null) return;
        if (!(ogounMark.active && ogounMark.turnsLeft > 0)) return;

        var pv = victim.photonView;
        if (pv == null) return;
        if (pv.ViewID != ogounMark.targetViewId) return;

        // Verrou côté adversaire (BLEU) pour 3 tours globaux
        photonView.RPC(nameof(RPC_ApplyTeamLockGlobal), RpcTarget.All, 1, 3);

        // Nettoyage marque + visuel
        photonView.RPC(nameof(RPC_ApplyMarkedVisual), RpcTarget.All, ogounMark.targetViewId, false);
        photonView.RPC(nameof(RPC_ClearOgounMarkClient), RpcTarget.All);

        Debug.Log("[OGOUN] Cible marquée tuée → lock 3 tours globaux sur l’équipe BLEUE.");
    }

    [PunRPC]
    private void RPC_ClearOgounMarkClient()
    {
        ClearOgounMarkLocal();
        UpdateButtonStates();
    }

    [PunRPC]
    private void RPC_ApplyTeamLockGlobal(int team, int turns)
    {
        if (team < 0 || team > 1) return;
        teamLockGlobalTurns[team] = Mathf.Max(teamLockGlobalTurns[team], turns);
        UpdateButtonStates();
    }

    // ─────────────────────────────────────────────
    // OGOUN : Rituel #2 – Boost passif (3 tours d’Ogoun)
    // ─────────────────────────────────────────────
    void OnOgounBoostClicked()
    {
        if (myTeam != 0) return;
        var tm = TurnManager.Instance;
        if (tm == null || !tm.Started || !tm.IsMyTurn) return;
        if (IsTeamLocked(myTeam)) return;
        if (currentRitualPoints < costOgounBoostRitual) return;
        if (ogounBoost.active && ogounBoost.turnsLeft > 0) return; // déjà actif

        photonView.RPC(nameof(RPC_RequestOgounBoost_Master), RpcTarget.MasterClient);
    }

    [PunRPC]
    private void RPC_RequestOgounBoost_Master(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (TurnManager.Instance == null || !TurnManager.Instance.Started) return;
        int requesterTeam = info.Sender != null && info.Sender.IsMasterClient ? 0 : 1;
        if (requesterTeam != 0) return; // seuls rouges
        if (TurnManager.Instance.CurrentPlayer != 0) return; // au tour d’Ogoun
        if (IsTeamLocked(0)) return;

        // Pas de double activation
        if (ogounBoost.active && ogounBoost.turnsLeft > 0) return;

        // Active pour tous : 3 tours d’Ogoun
        photonView.RPC(nameof(RPC_ActivateOgounBoostClient), RpcTarget.All, 3, 0);

        // Passe le tour immédiatement
        TurnManager.Instance.RequestEndTurn();
    }

    [PunRPC]
    private void RPC_ActivateOgounBoostClient(int turns, int requesterTeam)
    {
        ogounBoost.active = true;
        ogounBoost.turnsLeft = Mathf.Max(0, turns);

        if (TurnManager.Instance != null && TurnManager.Instance.MyTeam == requesterTeam)
            currentRitualPoints = Mathf.Max(0, currentRitualPoints - costOgounBoostRitual);

        UpdateButtonStates();
        UpdateTextRitualPoints();
        Debug.Log($"[OGOUN] Boost passif activé ({turns} tours d’Ogoun).");
    }

    /// <summary>
    /// Appelée par BoardManager pour savoir si le bonus de portée d’Ogoun est actif.
    /// </summary>
    public bool IsOgounPassiveBoostActive()
    {
        return ogounBoost.active && ogounBoost.turnsLeft > 0;
    }

    // ─────────────────────────────────────────────
    // Cibles valides pour la marque
    // ─────────────────────────────────────────────
    List<Vector2Int> ComputeValidOgounMarkTargets()
    {
        var res = new List<Vector2Int>();
        var pieces = Object.FindObjectsByType<Piece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var pc in pieces)
        {
            if (pc == null || pc.isRed) continue; // ennemis (bleus)
            if (!IsSpirit(pc)) continue;
            if (!IsValidOgounMarkTarget(pc)) continue;
            res.Add(pc.currentGridPos);
        }
        return res;
    }

    bool IsValidOgounMarkTarget(Piece enemy)
    {
        if (enemy == null || enemy.isRed) return false;
        if (!IsSpirit(enemy)) return false;

        // “non-adjacent à un esprit allié d’Ogoun”
        var allies = Object.FindObjectsByType<Piece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var a in allies)
        {
            if (a == null || !a.isRed) continue; // alliés d’Ogoun
            if (!IsSpirit(a)) continue;
            if (IsAdjacent(a.currentGridPos, enemy.currentGridPos)) return false;
        }
        return true;
    }

    bool IsSpirit(Piece p)
    {
        if (p == null) return false;
        var tname = p.GetType().Name;
        return (tname == "Spirit") || tname.Contains("Spirit");
    }

    // ─────────────────────────────────────────────
    // Bouton Passer (+1 PR)
    // ─────────────────────────────────────────────
    void OnPassTurnClicked()
    {
        var tm = TurnManager.Instance;
        if (tm == null || !tm.Started || !tm.IsMyTurn) return;

        if (currentRitualPoints < maxRitualPoints)
            currentRitualPoints++;

        tm.RequestEndTurn();
        UpdateButtonStates();
        UpdateTextRitualPoints();
    }

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────
    private bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx <= 1 && dy <= 1 && (dx + dy) > 0;
    }

    private int GetTeamAliveCount(bool isRedTeam)
    {
        var pieces = Object.FindObjectsByType<Piece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return pieces.Count(p => p != null && p.isRed == isRedTeam);
    }

    private void UpdateButtonStates()
    {
        DetectMyTeam();
        ApplyPanelVisibility();

        bool started = TurnManager.Instance != null && TurnManager.Instance.Started;
        bool myTurn  = started && TurnManager.Instance.IsMyTurn;

        if (myTeam == 0) // Ogoun
        {
            bool locked = IsTeamLocked(0);
            if (btnOgounMark)
                btnOgounMark.interactable = started && myTurn && !locked
                      && currentRitualPoints >= costOgounMarkRitual
                      && !(ogounMark.active && ogounMark.turnsLeft > 0) // une seule marque
                      && ComputeValidOgounMarkTargets().Count > 0;

            if (btnOgounBoost)
                btnOgounBoost.interactable = started && myTurn && !locked
                      && currentRitualPoints >= costOgounBoostRitual
                      && !(ogounBoost.active && ogounBoost.turnsLeft > 0);

            if (btnResurrect) btnResurrect.interactable = false;
            if (btnStealMove) btnStealMove.interactable = false;
            if (btnPassTurn)  btnPassTurn.interactable  = started && myTurn && currentRitualPoints < maxRitualPoints;
            return;
        }

        // Baron
        if (currentMask == null) currentMask = FindMyBaronMask();
        bool baronLocked = IsTeamLocked(1);
        int  po          = currentMask ? currentMask.GetShadowPoints() : 0;
        bool underCap    = currentMask && GetTeamAliveCount(currentMask.isRed) < maxUnitsPerTeam;

        if (btnResurrect)
            btnResurrect.interactable = started && myTurn && !baronLocked && currentMask && underCap
                                      && currentRitualPoints >= costResurrectRitual && po >= costResurrectPO
                                      && !IsStealActive;

        if (btnStealMove)
            btnStealMove.interactable = started && myTurn && !baronLocked
                                      && currentRitualPoints >= costStealMoveRitual
                                      && !IsStealActive;

        if (btnPassTurn)
            btnPassTurn.interactable = started && myTurn && currentRitualPoints < maxRitualPoints;

        if (btnOgounMark)  btnOgounMark.interactable  = false;
        if (btnOgounBoost) btnOgounBoost.interactable = false;
    }

    // ─────────────────────────────────────────────
    // ★★★ Carreaux PO (gain de PO côté Baron) ★★★
    // Appelée par le BoardManager (MAÎTRE) quand une condition de carreau PO est remplie.
    // amount par défaut = +1
    // ─────────────────────────────────────────────
    public void AwardBaronShadowPoints(int amount = 1)
    {
        if (amount <= 0) return;

        // Si on est MAÎTRE (ou offline), on pousse directement le RPC.
        if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
        {
            photonView.RPC(nameof(RPC_AddShadowPoints), RpcTarget.All, false /*blue team*/, amount);
        }
        else
        {
            // Sécurité : côté client non-maître, on demande au maître de valider.
            photonView.RPC(nameof(RPC_RequestAddShadowPoints_Master), RpcTarget.MasterClient, amount);
        }
    }

    [PunRPC]
    private void RPC_RequestAddShadowPoints_Master(int amount, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (amount <= 0) return;
        // Validation additionnelle côté maître possible ici si besoin…
        photonView.RPC(nameof(RPC_AddShadowPoints), RpcTarget.All, false /*blue team*/, amount);
    }

    [PunRPC]
    private void RPC_AddShadowPoints(bool isRedTeam, int amount)
    {
        if (amount <= 0) return;

        var barons = Object.FindObjectsByType<BaronSamediMaskPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var b in barons)
        {
            if (b.isRed == isRedTeam)
            {
                b.AddShadowPoints(amount);
                Debug.Log($"[RitualSystem] +{amount} PO pour équipe {(isRedTeam ? "ROUGE" : "BLEUE")} (carreau PO). Total masque={b.GetShadowPoints()}");
                break;
            }
        }
    }
}
