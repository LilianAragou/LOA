using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public abstract class Piece : MonoBehaviourPun, IPunInstantiateMagicCallback
{
    public Vector2Int currentGridPos;
    public bool isRed;
    protected BoardManager board;
    [HideInInspector] public GameObject sourcePrefab;

    private int tempRangeBonus = 0;

    protected virtual void OnTurnStart() { }
    protected virtual void OnTurnEnd()   { TempResetRange(); }
    protected virtual void OnCapture(Piece victim) { }
    protected virtual void OnAllyDeath(Piece ally) { }

    void HandleGlobalCapture(Piece attacker, Piece victim)
    {
        if (attacker == this) OnCapture(victim);
        else if (victim && victim.isRed == isRed && victim != this) OnAllyDeath(victim);
    }
    void HandleGlobalDestruction(Piece victim)
    {
        if (victim && victim.isRed == isRed && victim != this) OnAllyDeath(victim);
    }

    protected virtual void Start()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStart      += OnTurnStart;
            TurnManager.Instance.OnTurnEnd        += OnTurnEnd;
            TurnManager.Instance.OnPieceCaptured  += HandleGlobalCapture;
            TurnManager.Instance.OnPieceDestroyed += HandleGlobalDestruction;
        }
    }
    protected virtual void OnDestroy()
    {
        if (TurnManager.Instance == null) return;
        TurnManager.Instance.OnTurnStart      -= OnTurnStart;
        TurnManager.Instance.OnTurnEnd        -= OnTurnEnd;
        TurnManager.Instance.OnPieceCaptured  -= HandleGlobalCapture;
        TurnManager.Instance.OnPieceDestroyed -= HandleGlobalDestruction;
    }

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = info.photonView.InstantiationData;
        int x    = (int)data[0];
        int y    = (int)data[1];
        bool red = (bool)data[2];

        sourcePrefab = gameObject;
        board = BoardManager.Instance;
        Initialize(new Vector2Int(x, y), red, board);
    }

    public void Initialize(Vector2Int startPos, bool redTeam, BoardManager bm)
    {
        currentGridPos = startPos;
        isRed          = redTeam;
        board          = bm;

        var tile = board.GetTileAt(startPos);
        if (tile != null)
        {
            tile.SetOccupant(gameObject);
            transform.position = tile.transform.position;
        }
    }

    public abstract List<Vector2Int> GetAvailableMoves(BoardManager board);

    protected void AddStep(List<Vector2Int> acc, BoardManager b, Vector2Int target)
    {
        var t = b.GetTileAt(target);
        if (t == null) return;
        if (t.currentOccupant == null) acc.Add(target);
        else
        {
            var other = t.currentOccupant.GetComponent<Piece>();
            if (other != null && IsEnemy(other)) acc.Add(target);
        }
    }
    protected void Ray(List<Vector2Int> acc, BoardManager b, Vector2Int dir, int maxSteps = 8)
    {
        var p = currentGridPos;
        for (int i = 0; i < maxSteps; i++)
        {
            p += dir;
            var t = b.GetTileAt(p);
            if (t == null) break;

            if (t.currentOccupant == null)
            {
                acc.Add(p);
                continue;
            }

            var other = t.currentOccupant.GetComponent<Piece>();
            if (other != null && IsEnemy(other)) acc.Add(p);
            break;
        }
    }

    public bool IsEnemy(Piece other) => other != null && other.isRed != isRed;
    public Vector2Int GetPosition()  => currentGridPos;
    public void TempIncreaseRange(int amount) => tempRangeBonus += amount;
    public void TempResetRange()              => tempRangeBonus = 0;
    protected int GetEffectiveRange(int baseRange) => baseRange + tempRangeBonus;

    public void TryMoveTo(Vector2Int targetPos)
    {
        board.MovePiece(this, targetPos);
    }

    // === ÉVOLUTION : API appelée par l'UI (EvolutionSystem) ===========
    public void Evolve(string newPrefabKey) => RequestEvolve(newPrefabKey);

    public void RequestEvolve(string newPrefabKey)
    {
        // Offline / non connecté : on fait tout localement
        if (!PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode)
        {
            ApplyEvolutionLocal(newPrefabKey);
            return;
        }

        // Gardes client (UX) : bon tour ? bonne équipe ?
        if (TurnManager.Instance != null)
        {
            if (!TurnManager.Instance.Started || !TurnManager.Instance.IsMyTurn) return;
            int myTeam = TurnManager.Instance.MyTeam; // 0=RED, 1=BLUE
            bool isMine = (myTeam == 0 && isRed) || (myTeam == 1 && !isRed);
            if (!isMine) return;
        }

        // On demande au Master d'arbitrer l’évolution pour ce PhotonView
        int viewId = photonView ? photonView.ViewID : 0;
        BoardManager.Instance.photonView.RPC(
            "RPC_RequestEvolve_Master",
            RpcTarget.MasterClient,
            viewId,
            newPrefabKey
        );
    }

    /// <summary>
    /// Évolution locale (offline). Le Master en réseau n’utilise pas ça : il détruit/respawn via RPC.
    /// </summary>
    public void ApplyEvolutionLocal(string newPrefabKey)
    {
        var bm = BoardManager.Instance;
        if (bm == null) return;

        Vector2Int pos = currentGridPos;
        bool wasRed    = isRed;

        var tile = bm.GetTileAt(pos);
        if (tile == null) return;

        // Libère l'ancienne pièce
        tile.SetOccupant(null);
        Destroy(gameObject);

        // Instancie le nouveau prefab localement
        var loaded = Resources.Load<GameObject>("PhotonPrefabs/" + newPrefabKey);
        if (loaded == null)
        {
            Debug.LogError($"[ApplyEvolutionLocal] Prefab introuvable: Resources/PhotonPrefabs/{newPrefabKey}.prefab");
            return;
        }

        var go = Instantiate(loaded, tile.transform.position, Quaternion.identity, bm.transform);
        var newPiece = go.GetComponent<Piece>();
        if (newPiece != null)
            newPiece.Initialize(pos, wasRed, bm);
        else
            Debug.LogError($"[ApplyEvolutionLocal] Le prefab {newPrefabKey} ne contient pas de Piece.");
    }
}
