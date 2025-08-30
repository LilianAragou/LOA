using UnityEngine;
using UnityEngine.EventSystems;
using Photon.Pun;
using System.Collections.Generic;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    Camera cam;
    BoardManager board;
    Piece selectedPiece;

    // Modes existants
    bool isSpawnMode = false;
    bool isStealMode = false; // rituel 2 (Baron)

    // ─── UX Ogoun : extra-coup ────────────────────────────────────────
    bool isOgounFreeMove = false;
    int ogounPieceViewId = 0;

    // ─── Rituel 1 d’Ogoun : marquage d’un esprit adverse ──────────────
    bool isOgounMarkMode = false;
    HashSet<int> ogounMarkEligibleViewIds = new HashSet<int>();

    void Awake() => Instance = this;

    void Start()
    {
        cam   = Camera.main;
        board = FindFirstObjectByType<BoardManager>();
        Debug.Log($"[InputManager] Start: BoardManager found = {board != null}");
    }

    void Update()
    {
        // CLIC DROIT : sélectionner son propre masque -> ouvrir menu rituel (BARON)
        if (Input.GetMouseButtonDown(1) && !EventSystem.current.IsPointerOverGameObject())
        {
            Vector2 wp = cam.ScreenToWorldPoint(Input.mousePosition);
            foreach (var hit in Physics2D.OverlapPointAll(wp))
            {
                var baron = hit.GetComponentInParent<BaronSamediMaskPiece>();
                if (baron == null) continue;

                int myTeam = TurnManager.Instance != null ? TurnManager.Instance.MyTeam : -1;
                int maskTeam = baron.isRed ? 0 : 1;
                if (maskTeam != myTeam) { Debug.Log("[Input] Clic droit masque adverse ignoré."); continue; }

                Debug.Log($"[Input] Right‐click on MY mask → RitualMenu for {baron.name} @ {baron.currentGridPos}");
                RitualSystem.Instance.SetCurrentMask(baron);
                return;
            }
        }

        // CLIC GAUCHE ignoré si la souris est sur l’UI
        if (!Input.GetMouseButtonDown(0) || EventSystem.current.IsPointerOverGameObject())
            return;

        Vector2 wp2 = cam.ScreenToWorldPoint(Input.mousePosition);
        var hits    = Physics2D.OverlapPointAll(wp2);

        if (hits.Length == 0)
        {
            Deselect();
            return;
        }

        Tile tile = null;
        Piece piece = null;
        foreach (var h in hits)
        {
            if (tile == null)  tile  = h.GetComponentInParent<Tile>();
            if (piece == null) piece = h.GetComponentInParent<Piece>();
        }

        // 1) Mode spawn (résurrection Baron)
        if (isSpawnMode && tile != null)
        {
            RitualSystem.Instance.ConfirmSpawn(tile.gridPos);
            isSpawnMode = false;
            board.ClearHighlights();
            return;
        }

        // 2) Mode marquage d’Ogoun (piloté par InputManager)
        if (isOgounMarkMode)
        {
            HandleOgounMarkClick(piece, tile);
            return;
        }

        // 3) Clic sur une pièce
        if (piece != null)
        {
            HandlePieceClick(piece);
        }
        // 4) Clic sur une tuile
        else if (tile != null)
        {
            HandleTileClick(tile);
        }
        else
        {
            Deselect();
        }
    }

    // ───────────────────────────────────────────────────────────────────
    // Gestion clics “mode normal”
    // ───────────────────────────────────────────────────────────────────
    void HandlePieceClick(Piece p)
    {
        if (isSpawnMode) return;

        // Si on est en mode marquage d’Ogoun, route le clic ici
        if (isOgounMarkMode)
        {
            HandleOgounMarkClick(p, null);
            return;
        }

        // ─── Mode extra-coup d’Ogoun : seule la MÊME pièce est cliquable, à MON tour
        if (isOgounFreeMove)
        {
            if (!IsMyTurn()) return;

            var pv = p.GetComponent<PhotonView>();
            if (pv == null || pv.ViewID != ogounPieceViewId)
                return; // autres pièces non sélectionnables

            // (Re)sélection visuelle de la pièce autorisée
            selectedPiece = p;
            EvolutionSystem.Instance.HideMenu();    // pas d’évolution pendant l’extra-coup
            board.ShowFreeMoveTargets(p);           // surlignage cases VIDES (cyan)
            return;
        }

        // ─── Rituel 2 (Baron) : en mode steal, on peut sélectionner une pièce adverse
        if (isStealMode)
        {
            if (!IsEnemyPiece(p)) return;
            if (selectedPiece == p) { Deselect(); return; }

            selectedPiece = p;
            EvolutionSystem.Instance.HideMenu();
            board.ShowPossibleMoves(p);
            return;
        }

        // ─── Mode normal
        if (selectedPiece == p)
        {
            Deselect();
            EvolutionSystem.Instance.HideMenu();
            return;
        }

        if (selectedPiece != null)
        {
            TryMoveSelectedTo(p.currentGridPos);
            return;
        }

        if (IsMyTurn() && IsMyPiece(p))
        {
            SelectPiece(p);
            EvolutionSystem.Instance.ShowMenu(p);
        }
        else
        {
            Deselect();
        }
    }

    void HandleTileClick(Tile t)
    {
        // ─── Extra-coup d’Ogoun : déplacement de la même pièce sur case vide
        if (isOgounFreeMove)
        {
            if (!IsMyTurn()) return;
            if (selectedPiece == null) return;
            if (t.isOccupied) return; // UX: ignore les cases occupées

            board.MovePiece(selectedPiece, t.gridPos);
            // On ne quitte pas le mode ici : le Master enverra RPC_EndFreeMove
            return;
        }

        // ─── Rituel 2 : on déplace la pièce adverse sélectionnée
        if (isStealMode)
        {
            if (selectedPiece == null) return;
            board.MovePiece(selectedPiece, t.gridPos);
            return;
        }

        EvolutionSystem.Instance.HideMenu();

        if (selectedPiece == null) return;

        // Mode normal
        TryMoveSelectedTo(t.gridPos);
    }

    void TryMoveSelectedTo(Vector2Int target)
    {
        if (selectedPiece == null) return;

        if (!isStealMode && !isOgounFreeMove)
        {
            if (!IsMyTurn() || !IsMyPiece(selectedPiece)) return;
        }

        board.MovePiece(selectedPiece, target);
        Deselect();
        // Pour Ogoun/Steal : la fermeture de mode est gérée par les RPC côté Master
    }

    // ───────────────────────────────────────────────────────────────────
    // Mode marquage d’Ogoun (rituel #1)
    // ───────────────────────────────────────────────────────────────────
    void HandleOgounMarkClick(Piece clickedPiece, Tile clickedTile)
    {
        // Clic sur pièce : si c’est une cible valide -> confirme
        if (clickedPiece != null)
        {
            var pv = clickedPiece.GetComponent<PhotonView>();
            if (pv != null && ogounMarkEligibleViewIds.Contains(pv.ViewID))
            {
                RitualSystem.Instance.ConfirmOgounMark(clickedPiece);
                ExitOgounMarkMode();
                return;
            }
        }

        // Clic hors cible : on reste en mode (ou tu peux quitter si tu préfères)
        // ExitOgounMarkMode();
    }

    // Appelé par RitualSystem quand le joueur Ogoun déclenche le rituel de marquage
    public void EnterOgounMarkMode()
    {
        // Pas pendant d’autres modes
        isSpawnMode = false;
        isStealMode = false;
        isOgounFreeMove = false;

        isOgounMarkMode = true;
        ogounMarkEligibleViewIds.Clear();
        Deselect(); // nettoie surlignages actuels

        // On ne permet ce mode qu’à Ogoun (équipe rouge / team 0)
        int myTeam = TurnManager.Instance != null ? TurnManager.Instance.MyTeam : -1;
        if (myTeam != 0)
        {
            Debug.Log("[Input] EnterOgounMarkMode ignoré (je ne suis pas Ogoun).");
            return;
        }

        // Construit la liste des cibles valides
        BuildOgounEligibleTargets();

        // Surligne les cibles
        HighlightOgounTargets();
    }

    public void ExitOgounMarkMode()
    {
        isOgounMarkMode = false;
        ogounMarkEligibleViewIds.Clear();
        if (board != null) board.ClearHighlights();
    }

    void BuildOgounEligibleTargets()
    {
        if (board == null) return;

        // Esprits alliés (Ogoun = rouges)
        var allies = new List<Piece>();
        var enemies = new List<Piece>();

        var all = Object.FindObjectsByType<Piece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var pc in all)
        {
            if (pc == null) continue;
            if (pc.isRed) // Ogoun = rouges
            {
                if (IsSpirit(pc)) allies.Add(pc);
            }
            else
            {
                if (IsSpirit(pc)) enemies.Add(pc);
            }
        }

        foreach (var e in enemies)
        {
            bool adjacentToAnyAlly = false;
            foreach (var a in allies)
            {
                if (IsAdjacent(e.currentGridPos, a.currentGridPos))
                {
                    adjacentToAnyAlly = true;
                    break;
                }
            }
            if (!adjacentToAnyAlly)
            {
                var pv = e.GetComponent<PhotonView>();
                if (pv != null) ogounMarkEligibleViewIds.Add(pv.ViewID);
            }
        }
    }

    void HighlightOgounTargets()
    {
        if (board == null) return;

        foreach (int vid in ogounMarkEligibleViewIds)
        {
            var pv = PhotonNetwork.GetPhotonView(vid);
            if (pv == null) continue;
            var pc = pv.GetComponent<Piece>();
            if (pc == null) continue;

            var t = board.GetTileAt(pc.currentGridPos);
            if (t != null) t.Highlight(Color.red);
        }
    }

    // Détection “esprit” sans dépendre d’un type concret
    bool IsSpirit(Piece p)
    {
        if (p == null) return false;
        // On utilise le nom de type pour rester souple (ex: Spirit_Red, Spirit_Blue)
        string tn = p.GetType().Name;
        return tn.Contains("Spirit");
    }

    bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx <= 1 && dy <= 1 && (dx + dy) > 0;
    }

    // ───────────────────────────────────────────────────────────────────
    // Utilitaires état/selection
    // ───────────────────────────────────────────────────────────────────
    bool IsMyTurn()
    {
        return TurnManager.Instance != null && TurnManager.Instance.Started && TurnManager.Instance.IsMyTurn;
    }

    bool IsMyPiece(Piece p)
    {
        int myTeam = TurnManager.Instance != null ? TurnManager.Instance.MyTeam : 0; // 0=rouge, 1=bleu
        return (myTeam == 0 && p.isRed) || (myTeam == 1 && !p.isRed);
    }

    bool IsEnemyPiece(Piece p) => !IsMyPiece(p);

    void SelectPiece(Piece p)
    {
        selectedPiece = p;
        board.ShowPossibleMoves(p);
    }

    void Deselect()
    {
        EvolutionSystem.Instance.HideMenu();
        selectedPiece = null;
        if (board != null) board.ClearHighlights();
    }

    // ─── Hooks utilisés par RitualSystem (BARON) ───────────────────────
    public void EnterSpawnMode() => isSpawnMode = true;

    public void EnterStealMode()
    {
        isStealMode = true;
        Deselect();
    }
    public void ExitStealMode()
    {
        isStealMode = false;
        Deselect();
    }

    // ─── UX Ogoun : appelé par BoardManager.RPC_NotifyFreeMove ────────
    public void EnterOgounFreeMove(int pieceViewId)
    {
        var pv = PhotonNetwork.GetPhotonView(pieceViewId);
        if (pv == null) return;
        var piece = pv.GetComponent<Piece>();
        if (piece == null) return;

        // Activer le mode uniquement pour le joueur concerné (à son tour)
        int pieceTeam = piece.isRed ? 0 : 1;
        if (TurnManager.Instance == null || !TurnManager.Instance.IsMyTurn) return;
        if (TurnManager.Instance.MyTeam != pieceTeam) return;

        // Quitter le mode marquage si actif
        ExitOgounMarkMode();

        isOgounFreeMove = true;
        ogounPieceViewId = pieceViewId;

        // auto-sélection de la pièce et affichage des cases Vides
        selectedPiece = piece;
        EvolutionSystem.Instance.HideMenu();
        if (board != null) board.ShowFreeMoveTargets(piece);
    }

    public void ExitOgounFreeMove()
    {
        isOgounFreeMove = false;
        ogounPieceViewId = 0;
        Deselect(); // nettoie aussi le surlignage
    }
}
