using UnityEngine;
using TMPro;
using System.Collections.Generic;

// Hérite de MaskPiece pour réutiliser les déplacements diagonaux+orthogonaux
public class BaronSamediMaskPiece : MaskPiece
{
    [Header("UI")]
    public TextMeshProUGUI shadowPointsText;

    // Compteur de Points d'Ombre
    public int shadowPoints = 0;
    private int cost = 0;

    public Vector2Int position;

    // ─────────────────────────────────────────────────────────────
    // ★ Carreaux PO (violets) — +1 PO lorsqu'un allié BLEU marche dessus
    // ─────────────────────────────────────────────────────────────
    [Header("Carreaux PO (violets)")]
    [Tooltip("Coordonnées des cases violettes en grille (ex: 1,1 / 7,1 / 1,7 / 7,7)")]
    [SerializeField] private Vector2Int[] poTiles;

    [Tooltip("Si coché, chaque case ne rapporte qu’une seule fois pendant toute la partie.")]
    [SerializeField] private bool awardOncePerTile = true;

    // Sets runtime (rapides) pour test d'appartenance et mémorisation des cases déjà créditées
    private HashSet<Vector2Int> _poTileSet = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> _poTilesClaimed = new HashSet<Vector2Int>();

    // ─────────────────────────────────────────────────────────────

    protected override void Start()
    {
        base.Start();
        if (!IsMyPiece(FindFirstObjectByType<BaronSamediMaskPiece>()))
        shadowPointsText.text = "";
        // S'abonner aux événements de capture et destruction
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnPieceCaptured += OnSpiritCaptured;
                TurnManager.Instance.OnPieceDestroyed += OnSpiritDestroyed;
            }

        position = currentGridPos;
        RebuildPoTileSet();
        UpdateShadowUI();
    }

    protected override void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPieceCaptured  -= OnSpiritCaptured;
            TurnManager.Instance.OnPieceDestroyed -= OnSpiritDestroyed;
        }
        base.OnDestroy();
    }

    // ─────────────────────────────────────────────────────────────
    // PO via captures / destructions (existant)
    // ─────────────────────────────────────────────────────────────

    // Quand un esprit est capturé (Action<Piece,Piece>)
    private void OnSpiritCaptured(Piece attacker, Piece victim)
    {
        GainShadowPoint();
    }

    // Quand un esprit est détruit hors capture (Action<Piece>)
    private void OnSpiritDestroyed(Piece victim)
    {
        GainShadowPoint();
    }

    public int GetShadowPoints() => shadowPoints;

    /// <summary>
    /// Ajoute exactement +1 PO.
    /// </summary>
    public void GainShadowPoint()
    {
        shadowPoints++;
        UpdateShadowUI();
        Debug.Log($"[BARON] +1 PO → total={shadowPoints}");
    }

    /// <summary>
    /// Compatibilité ascendante : certains scripts appellent AddShadowPoints(+/-amount).
    /// amount > 0 => on ajoute ; amount < 0 => on dépense ; 0 => no-op.
    /// </summary>
    public void AddShadowPoints(int amount)
    {
        if (amount == 0) return;

        if (amount > 0)
        {
            shadowPoints += amount;
            UpdateShadowUI();
            Debug.Log($"[BARON] +{amount} PO → total={shadowPoints}");
        }
        else
        {
            UpdateShadowUI();
            SpendShadowPoints(-amount); // délègue aux dépenses
        }
    }

    private void UpdateShadowUI()
    {
        if (!IsMyPiece(FindFirstObjectByType<BaronSamediMaskPiece>()))
            shadowPointsText.text = "";
        else if (shadowPointsText != null)
            shadowPointsText.text = $"PO : {shadowPoints}";
    }
    bool IsMyPiece(Piece p)
    {
        int myTeam = TurnManager.Instance != null ? TurnManager.Instance.MyTeam : 0; // 0=rouge, 1=bleu
        return (myTeam == 0 && p.isRed) || (myTeam == 1 && !p.isRed);
    }

    public int GetUpgradeCost() => cost; // où `cost` est le PO requis pour évoluer

    public void SpendShadowPoints(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount == 0) return;

        shadowPoints = Mathf.Max(0, shadowPoints - amount);
        UpdateShadowUI();
        Debug.Log($"[BARON] -{amount} PO → total={shadowPoints}");
    }

    // ─────────────────────────────────────────────────────────────
    // ★ API Carreaux PO (violets)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// (Optionnel) Permet de définir/modifier à chaud la liste des cases violettes.
    /// </summary>
    public void SetPoTiles(IEnumerable<Vector2Int> tiles, bool resetClaims = false)
    {
        _poTileSet.Clear();
        if (tiles != null)
        {
            foreach (var v in tiles) _poTileSet.Add(v);
        }
        if (resetClaims) _poTilesClaimed.Clear();
    }

    /// <summary>
    /// À appeler quand une pièce ALLIÉE au Baron se déplace.
    /// </summary>
    public void OnAllyMoved(Piece who, Vector2Int to)
    {
        if (who == null) return;

        // Le Baron est côté BLEU => this.isRed == false
        // On crédite seulement si la pièce est ALLIÉE (même camp que le masque).
        if (who.isRed != this.isRed) return; // pas alliée -> ignorer

        TryAwardPoForTile(to, who);
    }

    /// <summary>
    /// Tente de donner +1 PO si 'pos' est une case violette non encore consommée (selon règles).
    /// Renvoie true si un PO a été attribué.
    /// </summary>
    public bool TryAwardPoForTile(Vector2Int pos, Piece who)
    {
        // Filtre: uniquement côté Baron (BLEU)
        if (this.isRed)
        {
            // Sécurité si jamais le prefab du masque rouge utilisait cette classe par erreur.
            return false;
        }

        if (!_poTileSet.Contains(pos))
        {
            // Pas une case PO -> ignorer
            return false;
        }

        if (awardOncePerTile && _poTilesClaimed.Contains(pos))
        {
            // Déjà consommée -> ignorer
            return false;
        }

        // Ok, on crédite
        GainShadowPoint();

        if (awardOncePerTile)
            _poTilesClaimed.Add(pos);

        string whoName = who ? who.name : "unknown";
        Debug.Log($"[BARON] Case violette atteinte par '{whoName}' @ {pos} → +1 PO (oncePerTile={awardOncePerTile}).");

        return true;
    }

    /// <summary>
    /// Remet à zéro les consommations des cases violettes (si besoin pour un reset de partie).
    /// </summary>
    public void ResetPoTileClaims()
    {
        _poTilesClaimed.Clear();
    }

    /// <summary>
    /// Helper rapide pour que le BoardManager notifie sans référence :
    /// BaronSamediMaskPiece.NotifyAllyStepped(to, movedPiece);
    /// </summary>
    public static void NotifyAllyStepped(Vector2Int pos, Piece who)
    {
        var mask = FindBlueMask();
        if (mask != null)
            mask.OnAllyMoved(who, pos);
    }

    /// <summary>
    /// Retourne l’instance du masque BLEU présente en scène.
    /// </summary>
    public static BaronSamediMaskPiece FindBlueMask()
    {
        var masks = Object.FindObjectsByType<BaronSamediMaskPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var m in masks)
            if (m != null && m.isRed == false) return m;
        return null;
    }

    // ─────────────────────────────────────────────────────────────

    private void RebuildPoTileSet()
    {
        _poTileSet.Clear();
        if (poTiles != null)
        {
            for (int i = 0; i < poTiles.Length; i++)
                _poTileSet.Add(poTiles[i]);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Reconstruit le set en Éditeur quand on change poTiles dans l’inspecteur
        RebuildPoTileSet();
    }
#endif

    // Pas d'override de Start ou GetAvailableMoves (hérités de MaskPiece)
}
