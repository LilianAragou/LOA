using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections.Generic;

public class BoardManager : MonoBehaviourPunCallbacks
{
    public static BoardManager Instance { get; private set; }

    [Header("Dimensions du plateau")]
    public int width = 9;
    public int height = 9;
    public float tileSize = 1f;

    [Header("Prefabs Tuiles & Texte")]
    public GameObject tilePrefab;
    public GameObject coordTextPrefab;

    [Header("Rouges — noms des prefabs (Resources/PhotonPrefabs)")]
    public string[] redPieceKeys = {
        "Spirit_Red",
        "Spirit_Red",
        "Ogoun_Mask",
        "Spirit_Red",
        "Spirit_Red",
        "Spirit_Red",
        "Spirit_Red"
    };
    public Vector2Int[] redPositions = {
        new Vector2Int(2, 0),
        new Vector2Int(3, 0),
        new Vector2Int(4, 0),
        new Vector2Int(5, 0),
        new Vector2Int(6, 0),
        new Vector2Int(3, 1),
        new Vector2Int(5, 1)
    };

    [Header("Bleues — noms des prefabs (Resources/PhotonPrefabs)")]
    public string[] bluePieceKeys = {
        "Spirit_Blue",
        "Spirit_Blue",
        "BaronSamediMask",
        "Spirit_Blue",
        "Spirit_Blue",
        "Spirit_Blue",
        "Spirit_Blue"
    };
    public Vector2Int[] bluePositions = {
        new Vector2Int(2, 8),
        new Vector2Int(3, 8),
        new Vector2Int(4, 8),
        new Vector2Int(5, 8),
        new Vector2Int(6, 8),
        new Vector2Int(3, 7),
        new Vector2Int(5, 7)
    };

    // ─────────────────────────────────────────────────────────────
    // Baron — cases violettes qui donnent +PO (une seule fois/case)
    // ─────────────────────────────────────────────────────────────
    [Header("Baron — cases bonus PO (xy plateau)")]
    [Tooltip("Cases qui rapportent des PO au Baron lorsqu’une pièce BLEUE termine dessus (une seule fois par case).")]
    public List<Vector2Int> baronBonusTiles = new List<Vector2Int> {
        new Vector2Int(1,4), new Vector2Int(3,4),
        new Vector2Int(5,4), new Vector2Int(7,4)
    };
    [Tooltip("PO gagnés par case (par défaut 1).")]
    public int baronBonusPerTile = 1;

    // cases déjà déclenchées (clé “x#y”), master-only
    private HashSet<string> _baronClaimedKeys = new HashSet<string>();
    private string Key(Vector2Int p) => p.x + "#" + p.y;

    private Tile[,] tiles;

    // Rituel 2 (vol de coup)
    const string ROOM_PROP_STEAL = "STEAL_TEAM"; // -1 = inactif, 0 red, 1 blue

    // ─── OGOUN: état d'extra-coup (côté Master) ───────────────────────
    private int freeMoveTeam = -1;   // -1: inactif, sinon 0/1
    private int freeMovePieceId = 0; // ViewID de la pièce qui a capturé

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    void Start()
    {
        
        GenerateTiles();

        if (!PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode)
        {
            SpawnPiecesLocal();
        }
        else if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            SpawnPiecesNetwork();
            EnsureRoomPropInitialized();
        }

        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStart += ResetFreeMoveState;

        // 🟣 Afficher les cases bonus Baron au lancement
        RefreshBaronBonusHighlights();
    }

    void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStart -= ResetFreeMoveState;
    }

    void ResetFreeMoveState()
    {
        freeMoveTeam = -1;
        freeMovePieceId = 0;
    }

    public override void OnJoinedRoom()
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode)
        {
            SpawnPiecesLocal();
        }
        else if (PhotonNetwork.IsMasterClient)
        {
            SpawnPiecesNetwork();
            EnsureRoomPropInitialized();
        }
    }

    void EnsureRoomPropInitialized()
    {
        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return;
        if (room.CustomProperties == null || !room.CustomProperties.ContainsKey(ROOM_PROP_STEAL))
        {
            var tb = new ExitGames.Client.Photon.Hashtable { { ROOM_PROP_STEAL, -1 } };
            room.SetCustomProperties(tb);
        }
    }

    // ─── Génération des tuiles ────────────────────────────────────────
    private void GenerateTiles()
    {
        tiles = new Tile[width, height];
        float offX = -((width - 1) * tileSize) / 2f;
        float offY = -((height - 1) * tileSize) / 2f;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                Vector2 pos = new Vector2(x * tileSize + offX, y * tileSize + offY);
                var go = Instantiate(tilePrefab, pos, Quaternion.identity, transform);
                var tile = go.GetComponent<Tile>();
                tile.gridPos = new Vector2Int(x, y);
                tiles[x, y] = tile;

                if (coordTextPrefab != null)
                {
                    var txt = Instantiate(coordTextPrefab, pos, Quaternion.identity, go.transform)
                              .GetComponent<TextMeshPro>();
                    txt.text = $"{(char)('A' + x)}{y + 1}";
                }
            }
    }

    // ─── Spawn réseau (bufferisé) ─────────────────────────────────────
    private void SpawnPiecesNetwork()
    {
        for (int i = 0; i < redPieceKeys.Length; i++)
            SpawnNetworkOne(redPieceKeys[i], redPositions[i], true);

        for (int i = 0; i < bluePieceKeys.Length; i++)
            SpawnNetworkOne(bluePieceKeys[i], bluePositions[i], false);
    }

    private void SpawnNetworkOne(string prefabKey, Vector2Int coord, bool isRed)
    {
        var tile = tiles[coord.x, coord.y];
        PhotonNetwork.InstantiateRoomObject(
            "PhotonPrefabs/" + prefabKey,
            tile.transform.position,
            Quaternion.identity,
            0,
            new object[] { coord.x, coord.y, isRed }
        );
    }

    // ─── Spawn local (offline) ────────────────────────────────────────
    private void SpawnPiecesLocal()
    {
        for (int i = 0; i < redPieceKeys.Length; i++)
            SpawnLocalOne(redPieceKeys[i], redPositions[i], true);

        for (int i = 0; i < bluePieceKeys.Length; i++)
            SpawnLocalOne(bluePieceKeys[i], bluePositions[i], false);
    }

    private void SpawnLocalOne(string prefabKey, Vector2Int coord, bool isRed)
    {
        var tile = tiles[coord.x, coord.y];
        var loaded = Resources.Load<GameObject>("PhotonPrefabs/" + prefabKey);
        if (loaded == null)
        {
            Debug.LogError($"Prefab introuvable : Resources/PhotonPrefabs/{prefabKey}.prefab");
            return;
        }
        var go = Instantiate(loaded, tile.transform.position, Quaternion.identity, transform);
        go.GetComponent<Piece>().Initialize(coord, isRed, this);
    }

    // ─── UI helpers ───────────────────────────────────────────────────
    public void ShowAdjacentTiles(Vector2Int center)
    {
        ClearHighlights();
        Vector2Int[] deltas = {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
            new Vector2Int(1,1), new Vector2Int(1,-1),
            new Vector2Int(-1,1), new Vector2Int(-1,-1)
        };
        foreach (var d in deltas)
        {
            var t = GetTileAt(center + d);
            if (t != null && !t.isOccupied)
                t.Highlight(Color.magenta);
        }
    }

    public void ShowPossibleMoves(Piece piece)
    {
        ClearHighlights();

        // ❌ PAS de boost ici : déplacement normal
        var moves = piece.GetAvailableMoves(this);
        Debug.Log($"[Moves] ShowPossibleMoves for {piece.name} @ {piece.currentGridPos} -> {moves.Count} cases (boost de)");
        foreach (var m in moves)
        {
            var t = GetTileAt(m);
            if (t != null) t.Highlight(Color.yellow);
        }
    }

    // Spécial UX Ogoun : n'afficher que les cases VIDES pour la même pièce
    public void ShowFreeMoveTargets(Piece piece)
    {
        ClearHighlights();

        bool boost = IsOgounPassiveBoostActiveFor(piece);
        Debug.Log($"[OGOUN] ShowFreeMoveTargets piece={piece.name} team={(piece.isRed ? 0 : 1)} boostActive={boost}");

        // ✅ Le boost ne s’applique qu’à l’extra-coup
        var moves = GetFreeMoveTargetsConsideringBoost(piece);
        Debug.Log($"[OGOUN] FreeMoveTargets count={moves.Count}");

        foreach (var m in moves)
        {
            var t = GetTileAt(m);
            if (t != null && !t.isOccupied) t.Highlight(Color.cyan);
        }
    }

    public void ClearHighlights()
    {
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            tiles[x, y].ResetHighlight();

        // Réafficher les cases bonus en violet (si encore actives)
        RefreshBaronBonusHighlights();
    }


    // ─── Déplacement ARBITRÉ PAR LE MASTER ────────────────────────────
    public void MovePiece(Piece piece, Vector2Int targetPos)
    {
        if (piece == null) return;
        int id = piece.photonView ? piece.photonView.ViewID : 0;
        photonView.RPC(nameof(RPC_RequestMove_Master), RpcTarget.MasterClient, id, targetPos.x, targetPos.y);
    }

    // ⚠️ Ogoun est maintenant l’équipe ROUGE
    private bool IsOgounTeam(int team) => team == 0;

    [PunRPC]
    private void RPC_RequestMove_Master(int pieceViewId, int toX, int toY, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (TurnManager.Instance == null || !TurnManager.Instance.Started) return;

        var pv = PhotonNetwork.GetPhotonView(pieceViewId);
        if (pv == null) return;

        var p = pv.GetComponent<Piece>();
        if (p == null) return;

        Vector2Int from = p.currentGridPos;
        Vector2Int to = new Vector2Int(toX, toY);

        // Équipes / tour
        int requesterTeam = info.Sender != null && info.Sender.IsMasterClient ? 0 : 1;
        int pieceTeam = p.isRed ? 0 : 1;
        int currentTeam = TurnManager.Instance.CurrentPlayer;

        // Rituel 2 (steal) actif ?
        int steal = -1;
        var room = PhotonNetwork.CurrentRoom;
        if (room != null && room.CustomProperties != null && room.CustomProperties.ContainsKey(ROOM_PROP_STEAL))
            steal = (int)room.CustomProperties[ROOM_PROP_STEAL];

        // ✅ Quand STEAL est actif, on interdit les coups "normaux" du joueur dont c'est le tour
        bool isNormal = (steal == -1) && (requesterTeam == currentTeam) && (pieceTeam == requesterTeam);

        // ✅ Seul l'équipe qui a déclenché STEAL peut jouer, et uniquement en déplaçant une pièce de l'équipe adverse
        bool isSteal = (steal == requesterTeam) && (currentTeam != requesterTeam) && (pieceTeam == currentTeam);

        bool isExtraFreeMove = (!isSteal) && (freeMoveTeam == pieceTeam && freeMovePieceId != 0);
        Debug.Log($"[Move-MASTER] steal={steal} reqTeam={requesterTeam} pieceTeam={pieceTeam} curTeam={currentTeam} isNormal={isNormal} isSteal={isSteal} isExtraFreeMove={isExtraFreeMove} from={from} to={to}");

        if (!(isNormal || isSteal))
        {
            Debug.Log("[Move-MASTER] Rejeté: coup non autorisé (STEAL actif ou règle de tour).");
            return;
        }

        // Sélection du set de coups légaux :
        List<Vector2Int> legalMoves = isExtraFreeMove
            ? GetFreeMoveTargetsConsideringBoost(p)
            : p.GetAvailableMoves(this);

        Debug.Log($"[Move-MASTER] legalMoves={legalMoves.Count} (using {(isExtraFreeMove ? "BOOST WRAPPER" : "normal moves")})");
        if (!legalMoves.Contains(to)) { Debug.Log("[Move-MASTER] Rejeté: destination non légale"); return; }

        Tile fromTile = GetTileAt(from);
        Tile toTile = GetTileAt(to);
        if (fromTile == null || toTile == null) return;

        // Pendant l'extra-coup: même pièce, case vide uniquement
        if (isExtraFreeMove)
        {
            if (freeMovePieceId != pieceViewId) { Debug.Log("[Move-MASTER] Rejeté: freeMovePieceId différent"); return; }
            if (toTile.currentOccupant != null) { Debug.Log("[Move-MASTER] Rejeté: case destination occupée pendant free-move"); return; }
        }

        // Capture à la case d'arrivée ?
        Piece victimAtDest = null;
        if (toTile.currentOccupant != null)
        {
            var vicPiece = toTile.currentOccupant.GetComponent<Piece>();
            if (vicPiece != null)
            {
                // Indestructible : impossible de capturer une Sentinelle
                if (vicPiece is Sentinelle_Ecarlate) { Debug.Log("[Move-MASTER] Rejeté: Sentinelle indestructible en destination"); return; }

                if (vicPiece.isRed == p.isRed) { Debug.Log("[Move-MASTER] Rejeté: pièce alliée en destination"); return; }
                victimAtDest = vicPiece;
            }
        }

        // === Effets spéciaux (Cavalier Fulgurant, Manieur de Lame, etc.) ===
        List<int> extraVictimIds = null;
        HashSet<Vector2Int> extraVictimPositions = null;
        var resolver = p as ISpecialMoveResolver;
        if (resolver != null)
        {
            SpecialMoveEffect eff = resolver.ResolveSpecial(this, p, from, to, victimAtDest);
            if (eff.extraVictimViewIds != null && eff.extraVictimViewIds.Count > 0)
            {
                extraVictimIds = new List<int>();
                extraVictimPositions = new HashSet<Vector2Int>();
                foreach (int vid in eff.extraVictimViewIds)
                {
                    var v = PhotonNetwork.GetPhotonView(vid);
                    if (v == null) continue;
                    var pc = v.GetComponent<Piece>();
                    if (pc == null) continue;

                    // Indestructible : ne jamais tuer une Sentinelle via effets
                    if (pc is Sentinelle_Ecarlate) continue;

                    extraVictimIds.Add(vid);
                    extraVictimPositions.Add(pc.currentGridPos);
                }
                Debug.Log($"[Move-MASTER] SpecialMove extraVictims={extraVictimIds.Count}");
            }
        }

        // ─── RAVAGEUR : pousser uniquement dans la direction du mouvement ─────────────────
        List<int> pushIds = null;
        List<Vector2Int> pushTargets = null;
        List<int> pushKillIds = null;

        var rav = p as Ravageur;
        if (rav != null)
        {
            pushIds = new List<int>();
            pushTargets = new List<Vector2Int>();
            pushKillIds = new List<int>();

            bool IsEmptyAfter(Vector2Int pos)
            {
                if (!InBounds(pos)) return false;
                if (pos == from) return true;                 // départ libéré
                if (pos == to) return false;                  // destination occupée par Ravageur
                if (victimAtDest != null && victimAtDest.currentGridPos == pos) return true;
                if (extraVictimPositions != null && extraVictimPositions.Contains(pos)) return true;
                var t = GetTileAt(pos);
                return t != null && t.currentOccupant == null;
            }

            Vector2Int dirMove = new Vector2Int(
                Mathf.Clamp(to.x - from.x, -1, 1),
                Mathf.Clamp(to.y - from.y, -1, 1)
            );

            if (dirMove != Vector2Int.zero)
            {
                var ePos = to + dirMove;
                var t = GetTileAt(ePos);
                if (t != null && t.currentOccupant != null)
                {
                    var enemy = t.currentOccupant.GetComponent<Piece>();
                    if (enemy != null && enemy.isRed != p.isRed && !(enemy is Sentinelle_Ecarlate))
                    {
                        Vector2Int target = ePos + dirMove;

                        if (!InBounds(target) || !IsEmptyAfter(target))
                        {
                            if (enemy.photonView != null) pushKillIds.Add(enemy.photonView.ViewID);
                        }
                        else
                        {
                            if (enemy.photonView != null)
                            {
                                pushIds.Add(enemy.photonView.ViewID);
                                pushTargets.Add(target);
                            }
                        }
                    }
                }
            }
            Debug.Log($"[Ravageur] pushes={pushIds.Count} kills={pushKillIds.Count}");
        }

        // ─── SENTINELLE : calculer les morts par aura (après move & pushes) ─────
        var sentKillIds = new List<int>();

        bool OrthAdjacentToEnemySentinel(Vector2Int pos, bool isRedOfUnit)
        {
            var sents = Object.FindObjectsByType<Sentinelle_Ecarlate>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var s in sents)
            {
                if (s == null) continue;
                if (s.isRed == isRedOfUnit) continue; // seulement ennemie
                int dx = Mathf.Abs(pos.x - s.currentGridPos.x);
                int dy = Mathf.Abs(pos.y - s.currentGridPos.y);
                if (dx + dy == 1) return true; // orthogonal
            }
            return false;
        }

        // le joueur qui bouge : meurt s'il finit adjacent ortho à une sentinelle ennemie
        if (OrthAdjacentToEnemySentinel(to, p.isRed))
        {
            if (p.photonView != null) sentKillIds.Add(p.photonView.ViewID);
        }

        // pièces poussées : si leur case d'arrivée est adjacente ortho à une sentinelle ennemie → meurent
        if (pushIds != null && pushTargets != null)
        {
            for (int i = 0; i < pushIds.Count; i++)
            {
                if (pushKillIds != null && pushKillIds.Contains(pushIds[i])) continue;

                var pvPushed = PhotonNetwork.GetPhotonView(pushIds[i]);
                if (pvPushed == null) continue;
                var pushedPiece = pvPushed.GetComponent<Piece>();
                if (pushedPiece == null) continue;

                var toPushed = pushTargets[i];
                if (OrthAdjacentToEnemySentinel(toPushed, pushedPiece.isRed))
                {
                    sentKillIds.Add(pushIds[i]);
                }
            }
        }

        // ─── APPLICATION : extraKills → move → (bonus Baron) → pushes → aura ─────
        if (extraVictimIds != null && extraVictimIds.Count > 0)
        {
            photonView.RPC(nameof(RPC_ApplyExtraVictims_All),
                        RpcTarget.All,
                        pieceViewId,
                        extraVictimIds.ToArray());
        }

        photonView.RPC(nameof(RPC_ApplyMove_All),
            RpcTarget.All,
            pieceViewId, from.x, from.y, to.x, to.y, victimAtDest ? victimAtDest.photonView.ViewID : 0
        );

        // 🟣 BONUS BARON : créditer PO si une pièce BLEUE foule une case violette (une fois par case)
        TryAwardBaronBonusOnLanding(to, p.isRed);

        if (rav != null && ((pushIds?.Count ?? 0) + (pushKillIds?.Count ?? 0)) > 0)
        {
            photonView.RPC(nameof(RPC_ApplyRavageurPush_All),
                        RpcTarget.All,
                        pieceViewId,
                        pushIds?.ToArray() ?? new int[0],
                        PackX(pushTargets ?? new List<Vector2Int>()).ToArray(),
                        PackY(pushTargets ?? new List<Vector2Int>()).ToArray(),
                        pushKillIds?.ToArray() ?? new int[0]);
        }

        if (sentKillIds.Count > 0)
        {
            photonView.RPC(nameof(RPC_ApplySentinelAuraKills_All),
                        RpcTarget.All,
                        sentKillIds.ToArray());
        }

        // ─── FIN DE LOGIQUE DE TOUR / OGOUN ─────────────────────────────
        bool anyKill =
            (victimAtDest != null) ||
            (extraVictimIds != null && extraVictimIds.Count > 0);
        bool moverDiesBySentinel = (p.photonView != null) && sentKillIds.Contains(p.photonView.ViewID);

        Debug.Log($"[Move-MASTER] anyKill={anyKill} moverDiesBySentinel={moverDiesBySentinel} freeMoveTeam={freeMoveTeam} freeMovePieceId={freeMovePieceId}");

        // Steal: refermer + fin du tour
        if (isSteal)
        {
            var tb = new ExitGames.Client.Photon.Hashtable { { ROOM_PROP_STEAL, -1 } };
            room.SetCustomProperties(tb);
            RitualSystem.Instance.photonView.RPC(nameof(RitualSystem.RPC_EndStealModeClient), RpcTarget.All);

            ResetFreeMoveState();
            photonView.RPC(nameof(RPC_EndFreeMove), RpcTarget.All);
            TurnManager.Instance.RequestEndTurn();
            return;
        }

        // Extra-coup d’Ogoun (ne compte pas la poussée, NI l’aura de Sentinelle)
        if (anyKill && IsOgounTeam(pieceTeam) && freeMoveTeam == -1 && !moverDiesBySentinel)
        {
            // pré-check rapide : y a-t-il au moins une case vide jouable ?
            bool hasEmptyTarget = false;
            var moves = GetFreeMoveTargetsConsideringBoost(p);
            foreach (var m in moves)
            {
                var t = GetTileAt(m);
                if (t != null && !t.isOccupied) { hasEmptyTarget = true; break; }
            }
            Debug.Log($"[OGOUN] Check free-move -> hasEmptyTarget={hasEmptyTarget} boostActive={IsOgounPassiveBoostActiveFor(p)}");

            if (hasEmptyTarget)
            {
                freeMoveTeam = pieceTeam;
                freeMovePieceId = pieceViewId;
                photonView.RPC(nameof(RPC_NotifyFreeMove), RpcTarget.All, pieceViewId);
                return; // on attend le 2e déplacement
            }
            else
            {
                // pas de cible -> on finit le tour immédiatement
                ResetFreeMoveState();
                photonView.RPC(nameof(RPC_EndFreeMove), RpcTarget.All);
                TurnManager.Instance.RequestEndTurn();
                return;
            }
        }

        // Si on était dans l'extra-coup et que cette même pièce vient de jouer,
        // on clôt l'état + UX puis fin de tour
        if (freeMoveTeam == pieceTeam && freeMovePieceId == pieceViewId)
        {
            ResetFreeMoveState();
            photonView.RPC(nameof(RPC_EndFreeMove), RpcTarget.All);
            TurnManager.Instance.RequestEndTurn();
            return;
        }

        // Cas normal
        ResetFreeMoveState();
        photonView.RPC(nameof(RPC_EndFreeMove), RpcTarget.All);
        TurnManager.Instance.RequestEndTurn();
    }


    // Applique le déplacement (tous)
    [PunRPC]
    private void RPC_ApplyMove_All(int pieceId, int fromX, int fromY, int toX, int toY, int victimId)
    {
        var pv = PhotonNetwork.GetPhotonView(pieceId);
        if (pv == null) return;

        var p = pv.GetComponent<Piece>();
        if (p == null) return;

        Tile fromTile = GetTileAt(new Vector2Int(fromX, fromY));
        Tile toTile = GetTileAt(new Vector2Int(toX, toY));
        if (fromTile == null || toTile == null) return;

        // Capture à l'arrivée (si présente)
        if (victimId != 0)
        {
            var vicPV = PhotonNetwork.GetPhotonView(victimId);
            if (vicPV != null)
            {
                var vicPiece = vicPV.GetComponent<Piece>();
                if (vicPiece != null)
                {
                    TurnManager.Instance.NotifyCapture(p, vicPiece);
                    toTile.SetOccupant(null);
                    PhotonNetwork.Destroy(vicPV);
                }
            }
        }

        // Déplacement
        fromTile.SetOccupant(null);
        toTile.SetOccupant(p.gameObject);
        p.currentGridPos = new Vector2Int(toX, toY);
        p.transform.position = toTile.transform.position;
    }

    // ─── Appliquer les victimes supplémentaires (Manieur, Cavalier, etc.) ─────
    [PunRPC]
    private void RPC_ApplyExtraVictims_All(int attackerId, int[] victimIds)
    {
        if (victimIds == null || victimIds.Length == 0) return;

        var atkPV = PhotonNetwork.GetPhotonView(attackerId);
        var attacker = atkPV != null ? atkPV.GetComponent<Piece>() : null;

        foreach (var id in victimIds)
        {
            var vicPV = PhotonNetwork.GetPhotonView(id);
            if (vicPV == null) continue;

            var vicPiece = vicPV.GetComponent<Piece>();
            if (vicPiece == null) continue;

            if (attacker != null)
                TurnManager.Instance.NotifyCapture(attacker, vicPiece);
            else
                TurnManager.Instance.NotifyDestruction(vicPiece);

            var tile = GetTileAt(vicPiece.currentGridPos);
            if (tile != null && tile.currentOccupant == vicPiece.gameObject)
                tile.SetOccupant(null);

            PhotonNetwork.Destroy(vicPV);
        }
    }

    // ─── Ravageur : appliquer pushes/kills post-move ───────────────────
    [PunRPC]
    private void RPC_ApplyRavageurPush_All(int attackerId, int[] pushIds, int[] toXs, int[] toYs, int[] killIds)
    {
        var atkPV = PhotonNetwork.GetPhotonView(attackerId);
        var attacker = atkPV != null ? atkPV.GetComponent<Piece>() : null;

        // Kills d’abord (pour libérer des cases si jamais)
        if (killIds != null)
        {
            foreach (var id in killIds)
            {
                var vicPV = PhotonNetwork.GetPhotonView(id);
                if (vicPV == null) continue;
                var vic = vicPV.GetComponent<Piece>();
                if (vic == null) continue;

                if (attacker != null)
                    TurnManager.Instance.NotifyCapture(attacker, vic);
                else
                    TurnManager.Instance.NotifyDestruction(vic);

                var t = GetTileAt(vic.currentGridPos);
                if (t != null && t.currentOccupant == vic.gameObject)
                    t.SetOccupant(null);

                PhotonNetwork.Destroy(vicPV);
            }
        }

        // Puis pushes
        if (pushIds != null && toXs != null && toYs != null)
        {
            int n = Mathf.Min(pushIds.Length, Mathf.Min(toXs.Length, toYs.Length));
            for (int i = 0; i < n; i++)
            {
                var pv = PhotonNetwork.GetPhotonView(pushIds[i]);
                if (pv == null) continue;
                var piece = pv.GetComponent<Piece>();
                if (piece == null) continue;

                var from = piece.currentGridPos;
                var to = new Vector2Int(toXs[i], toYs[i]);

                var fromTile = GetTileAt(from);
                var toTile = GetTileAt(to);
                if (fromTile == null || toTile == null) continue;

                fromTile.SetOccupant(null);
                toTile.SetOccupant(piece.gameObject);
                piece.currentGridPos = to;
                piece.transform.position = toTile.transform.position;
            }
        }
    }

    // ─── Sentinelle : appliquer les kills d’aura ───────────────────────
    [PunRPC]
    private void RPC_ApplySentinelAuraKills_All(int[] victimIds)
    {
        if (victimIds == null || victimIds.Length == 0) return;

        foreach (var id in victimIds)
        {
            var vicPV = PhotonNetwork.GetPhotonView(id);
            if (vicPV == null) continue;

            var vicPiece = vicPV.GetComponent<Piece>();
            if (vicPiece == null) continue;

            // Aura = kill environnemental → on n'enregistre pas comme "capture"
            TurnManager.Instance.NotifyDestruction(vicPiece);

            var tile = GetTileAt(vicPiece.currentGridPos);
            if (tile != null && tile.currentOccupant == vicPiece.gameObject)
                tile.SetOccupant(null);

            PhotonNetwork.Destroy(vicPV);
        }
    }

    // ──────────────────────────── ÉVOLUTION ────────────────────────────
    [PunRPC]
    private void RPC_RequestEvolve_Master(int pieceViewId, string newPrefabKey, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (TurnManager.Instance == null || !TurnManager.Instance.Started) return;

        var pv = PhotonNetwork.GetPhotonView(pieceViewId);
        if (pv == null) return;

        var piece = pv.GetComponent<Piece>();
        if (piece == null) return;

        // ⛔ Sécurité Master : empêcher la ré-évolution d’une pièce déjà évoluée
        if (piece.GetComponent<EvolutionTag>() != null)
        {
            Debug.Log("[EVO] Refus: la pièce possède déjà EvolutionTag (ré-évolution interdite).");
            return;
        }

        Vector2Int pos = piece.currentGridPos;
        Tile tile = GetTileAt(pos);
        if (tile == null) return;

        // Équipe / tour
        int requesterTeam = info.Sender != null && info.Sender.IsMasterClient ? 0 : 1;
        int pieceTeam = piece.isRed ? 0 : 1;
        int currentTeam = TurnManager.Instance.CurrentPlayer;

        // Évolution autorisée uniquement à son tour et sur ses propres pièces
        if (!(requesterTeam == currentTeam && requesterTeam == pieceTeam)) return;

        // 🚫 Verrou d’Ogoun : bloque l'évolution au niveau Master (sécurité réseau)
        if (RitualSystem.Instance != null && RitualSystem.Instance.IsTeamLocked(pieceTeam)) return;

        // Vérifier proximité au masque correspondant (sécurité Master)
        if (!IsAdjacentToOwnMask(piece)) return;

        // Charger le prefab cible
        var loaded = Resources.Load<GameObject>("PhotonPrefabs/" + newPrefabKey);
        if (loaded == null)
        {
            Debug.LogError($"[EVO] Prefab introuvable: Resources/PhotonPrefabs/{newPrefabKey}");
            return;
        }

        // Remplacer la pièce au même endroit (bufferisé)
        tile.SetOccupant(null);
        PhotonNetwork.Destroy(pv);

        PhotonNetwork.InstantiateRoomObject(
            "PhotonPrefabs/" + newPrefabKey,
            tile.transform.position,
            Quaternion.identity,
            0,
            new object[] { pos.x, pos.y, piece.isRed }
        );

        // ✅ L’évolution consomme le tour entier
        ResetFreeMoveState();
        photonView.RPC(nameof(RPC_EndFreeMove), RpcTarget.All);
        TurnManager.Instance.RequestEndTurn();
    }

    // ⚠️ Mapping corrigé : rouge → Ogoun_Mask, bleu → BaronSamediMaskPiece
    private bool IsAdjacentToOwnMask(Piece p)
    {
        if (p.isRed)
        {
            var masks = Object.FindObjectsByType<Ogoun_Mask>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var m in masks)
                if (m != null && m.isRed == p.isRed && IsAdjacent(p.currentGridPos, m.currentGridPos))
                    return true;
        }
        else
        {
            var masks = Object.FindObjectsByType<BaronSamediMaskPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var m in masks)
                if (m != null && m.isRed == p.isRed && IsAdjacent(p.currentGridPos, m.currentGridPos))
                    return true;
        }
        return false;
    }

    private bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx <= 1 && dy <= 1 && (dx + dy) > 0;
    }

    [PunRPC]
    private void RPC_NoFreeMove_PassTurn_Master(int pieceViewId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // sécurité : on ne clôture que si on est bien en mode extra-coup pour cette pièce
        if (freeMoveTeam != -1 && freeMovePieceId == pieceViewId)
        {
            ResetFreeMoveState();
            photonView.RPC(nameof(RPC_EndFreeMove), RpcTarget.All);
            TurnManager.Instance.RequestEndTurn();
        }
    }

    // ─── Extra-coup d’Ogoun : UX ───────────────────────────────────────
    [PunRPC]
    private void RPC_NotifyFreeMove(int pieceId)
    {
        Debug.Log($"[OGOUN] Extra-coup: rejouez la MÊME pièce (sur case vide). ViewID={pieceId}");

        var pv = PhotonNetwork.GetPhotonView(pieceId);
        if (pv == null) return;
        var piece = pv.GetComponent<Piece>();
        if (piece == null) return;

        bool boostActive = IsOgounPassiveBoostActiveFor(piece);
        Debug.Log($"[OGOUN] RPC_NotifyFreeMove piece={piece.name} boostActive={boostActive}");

        // Vérifie tout de suite s'il existe au moins UNE case vide jouable
        bool hasEmptyTarget = false;
        var moves = GetFreeMoveTargetsConsideringBoost(piece);
        foreach (var m in moves)
        {
            var t = GetTileAt(m);
            if (t != null && !t.isOccupied) { hasEmptyTarget = true; break; }
        }
        Debug.Log($"[OGOUN] FreeMove pre-check -> hasEmptyTarget={hasEmptyTarget} candidates={moves.Count}");

        if (!hasEmptyTarget)
        {
            int id = piece.photonView ? piece.photonView.ViewID : 0;
            if (PhotonNetwork.IsMasterClient)
            {
                ResetFreeMoveState();
                photonView.RPC(nameof(RPC_EndFreeMove), RpcTarget.All);
                TurnManager.Instance.RequestEndTurn();
            }
            else
            {
                photonView.RPC(nameof(RPC_NoFreeMove_PassTurn_Master), RpcTarget.MasterClient, id);
            }
            return;
        }

        // Sinon, on affiche normalement les cibles
        if (InputManager.Instance != null)
            InputManager.Instance.EnterOgounFreeMove(pieceId);

        ShowFreeMoveTargets(piece);
    }

    [PunRPC]
    private void RPC_EndFreeMove()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.ExitOgounFreeMove();
        ClearHighlights();
    }

    // ─── Accès tuile / utils ──────────────────────────────────────────
    public Tile GetTileAt(Vector2Int pos)
    {
        if (!InBounds(pos)) return null;
        return tiles[pos.x, pos.y];
    }

    private bool InBounds(Vector2Int pos)
        => pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;

    private static List<int> PackX(List<Vector2Int> a)
    {
        var r = new List<int>(a.Count);
        for (int i = 0; i < a.Count; i++) r.Add(a[i].x);
        return r;
    }
    private static List<int> PackY(List<Vector2Int> a)
    {
        var r = new List<int>(a.Count);
        for (int i = 0; i < a.Count; i++) r.Add(a[i].y);
        return r;
    }

    // Helper d’identification pour le correctif
    private bool IsCavalierFulgurant(Piece p)
    {
        if (p == null) return false;
        string tn = p.GetType().Name;
        return tn == "Cavalier_Fulgurant" || (tn.Contains("Cavalier") && tn.Contains("Fulgurant"));
    }
    private bool IsManieurDeLames(Piece p)
    {
        if (p == null) return false;
        string tn = p.GetType().Name;
        return tn == "Manieur_De_Lames" || (tn.Contains("Manieur") && tn.Contains("Lames"));
    }

    // ───────────────────────────────────────────────────────────────────
    // 🔥 RITUEL #2 OGOUN : wrapper utilisé UNIQUEMENT pour l’extra-coup
    // ───────────────────────────────────────────────────────────────────
    /// <summary>
    /// Renvoie les cibles de déplacement de la pièce pour l’EXTRA-COUP (case vide),
    /// en tenant compte du boost d’Ogoun si actif : +1 case supplémentaire dans
    /// chaque direction « pas de 1 » que la pièce pouvait déjà emprunter.
    /// L’extension n’ajoute que des cases ARRIVÉES VIDES (pas de capture à +2).
    /// Règle spéciale Cavalier Fulgurant : autorise un "saut" au pas intermédiaire
    /// si ce n’est pas une pièce alliée (conserve les kills sur la route).
    /// </summary>
    public List<Vector2Int> GetFreeMoveTargetsConsideringBoost(Piece piece)
    {
        // Base = coups normaux
        var moves = piece.GetAvailableMoves(this);

        bool boostActive = IsOgounPassiveBoostActiveFor(piece);
        Debug.Log($"[OGOUN][BoostCalc] piece={piece.name} origin={piece.currentGridPos} baseMoves={moves.Count} boostActive={boostActive}");

        // Si pas de boost actif (ou pièce non rouge), on renvoie tel quel.
        if (!boostActive)
            return moves;

        Vector2Int origin = piece.currentGridPos;
        var result = new HashSet<Vector2Int>(moves);

        bool isCavalier = IsCavalierFulgurant(piece);
        bool isManieur = IsManieurDeLames(piece);

        // Cas spécial : Manieur de Lames
        if (isManieur)
        {
            Vector2Int[] offsets = {
                new Vector2Int(2, 2),
                new Vector2Int(2, -2),
                new Vector2Int(-2, 2),
                new Vector2Int(-2, -2)
            };

            foreach (var off in offsets)
            {
                var target = origin + off;
                var tile = GetTileAt(target);
                if (tile == null) continue;

                // Case finale doit être vide
                if (tile.currentOccupant == null)
                {
                    result.Add(target);
                    Debug.Log($"[OGOUN][BoostCalc] +Manieur OK origin={origin} target={target}");
                }
                else
                {
                    Debug.Log($"[OGOUN][BoostCalc] +Manieur REFUS occupée par {tile.currentOccupant.name} en {target}");
                }
            }

            return new List<Vector2Int>(result);
        }

        // Cas spécial : Cavalier Fulgurant
        if (isCavalier)
        {
            Vector2Int[] ortho = {
                new Vector2Int(1,0), new Vector2Int(-1,0),
                new Vector2Int(0,1), new Vector2Int(0,-1)
            };

            foreach (var d in ortho)
            {
                var target = origin + d;
                var t = GetTileAt(target);
                if (t == null) continue;

                bool block = false;
                if (t.currentOccupant != null)
                {
                    var inter = t.currentOccupant.GetComponent<Piece>();
                    if (inter != null && inter.isRed == piece.isRed)
                        block = true; // allié = bloqué
                }

                if (!block && t.currentOccupant == null)
                {
                    result.Add(target);
                    Debug.Log($"[OGOUN][BoostCalc] Cavalier +1 Ortho OK origin={origin} target={target}");
                }
                else
                {
                    Debug.Log($"[OGOUN][BoostCalc] Cavalier REFUS sur {target}");
                }
            }

            return new List<Vector2Int>(result);
        }

        // Cas général : chaque coup normal est prolongé de +1 dans sa direction
        foreach (var m in moves)
        {
            var delta = m - origin;

            int dx = Mathf.Clamp(delta.x, -1, 1);
            int dy = Mathf.Clamp(delta.y, -1, 1);
            if (dx == 0 && dy == 0) continue;

            var stepNext = m + new Vector2Int(dx, dy);
            var tNext = GetTileAt(stepNext);
            if (tNext == null) continue;

            if (tNext.currentOccupant == null)
            {
                result.Add(stepNext);
                Debug.Log($"[OGOUN][BoostCalc] +1 général OK origin={origin} baseMove={m} stepNext={stepNext}");
            }
            else
            {
                Debug.Log($"[OGOUN][BoostCalc] +1 général REFUS occupée par {tNext.currentOccupant.name} en {stepNext}");
            }
        }

        Debug.Log($"[OGOUN][BoostCalc] resultCount={result.Count}");
        return new List<Vector2Int>(result);
    }



    private bool IsOgounPassiveBoostActiveFor(Piece p)
    {
        if (p == null || !p.isRed) return false;
        if (RitualSystem.Instance == null) return false;
        return RitualSystem.Instance.IsOgounPassiveBoostActive();
    }

    // ───────────────────────────────────────────────────────────────────
    // 🟣 BONUS BARON — implémentation
    // ───────────────────────────────────────────────────────────────────
    private void TryAwardBaronBonusOnLanding(Vector2Int landingPos, bool moverIsRed)
    {
        if (!PhotonNetwork.IsMasterClient) return;      // autorité Master
        if (moverIsRed) return;                         // uniquement pièces BLEUES
        if (baronBonusTiles == null || baronBonusTiles.Count == 0) return;

        // la case fait-elle partie du set ?
        for (int i = 0; i < baronBonusTiles.Count; i++)
        {
            if (baronBonusTiles[i] == landingPos)
            {
                string k = Key(landingPos);
                if (_baronClaimedKeys.Contains(k)) return; // déjà prise → rien

                photonView.RPC(nameof(RPC_ConsumeBaronTile), RpcTarget.All, landingPos.x, landingPos.y);

                return;
            }
        }
    }
    [PunRPC]
    private void RPC_ConsumeBaronTile(int x, int y)
    {
        string k = Key(new Vector2Int(x, y));
        _baronClaimedKeys.Add(k);

        var tile = GetTileAt(new Vector2Int(x, y));
        if (tile != null)
            tile.ResetHighlight(); // elle redevient normale

        var mask = FindBlueBaronMask();
        if (mask != null && baronBonusPerTile > 0)
        {
            // Suppose une méthode AddShadowPoints(int). Si ton API diffère, dis-le moi.
            mask.AddShadowPoints(baronBonusPerTile);
            Debug.Log($"[BARON][PO] +{baronBonusPerTile} PO)");
        }
    }

    private BaronSamediMaskPiece FindBlueBaronMask()
    {
        var masks = Object.FindObjectsByType<BaronSamediMaskPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var m in masks)
            if (m != null && m.isRed == false)
                return m;
        return null;
    }
    // ─── Visuel permanent des cases bonus du Baron ───────────────────────
    public void RefreshBaronBonusHighlights()
    {
        if (baronBonusTiles == null || baronBonusTiles.Count == 0) return;

        foreach (var pos in baronBonusTiles)
        {
            string key = Key(pos);
            if (_baronClaimedKeys.Contains(key)) continue; // déjà consommée → normale

            var tile = GetTileAt(pos);
            if (tile != null)
            {
                // Violet permanent tant qu’elle n’a pas été activée
                tile.Highlight(Color.magenta);
            }
        }
    }
}
