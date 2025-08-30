using System.Collections.Generic;
using UnityEngine;

public class EGareDesProfondeursPiece : Piece
{
    [Header("Égaré des Profondeurs")]
    [Tooltip("Coût en PO pour invoquer cet esprit")]
    public int cost = 4;

    private BaronSamediMaskPiece baron;

    // ─── Initialise baron après base.Start() ─────────────────────────
    protected override void Start()
    {
        base.Start();  // hérite des abonnements de Piece

        // On récupère l’unique instance de BaronSamediMaskPiece
        baron = FindFirstObjectByType<BaronSamediMaskPiece>();
        if (baron == null)
            Debug.LogWarning($"{name}: Aucun BaronSamediMaskPiece trouvé → diagBonus = 0");
        else
            Debug.Log($"{name}: trouvé BaronSamediMaskPiece avec {baron.GetShadowPoints()} PO");
    }

    // ─── Détermine les cases atteignables ─────────────────────────────
    public override List<Vector2Int> GetAvailableMoves(BoardManager board)
    {
        var moves = new List<Vector2Int>();

        // 1) Orthogonal jusqu’à 2
        int orthoRange = 2;
        Vector2Int[] orthos = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var dir in orthos)
        {
            for (int d = 1; d <= orthoRange; d++)
            {
                var np = currentGridPos + dir * d;
                var t  = board.GetTileAt(np);
                if (t == null) break;

                if (!t.isOccupied) moves.Add(np);
                else
                {
                    var other = t.currentOccupant.GetComponent<Piece>();
                    if (other != null && IsEnemy(other)) moves.Add(np);
                    break;
                }
            }
        }

        // 2) Diagonales bonus = floor(PO / 5)
        int shadowPoints = baron != null ? baron.GetShadowPoints() : 0;
        int diagBonus    = shadowPoints / 5;

        if (diagBonus > 0)
        {
            Vector2Int[] diags = {
                new Vector2Int( 1,  1), new Vector2Int( 1, -1),
                new Vector2Int(-1,  1), new Vector2Int(-1, -1)
            };
            foreach (var dir in diags)
            {
                for (int d = 1; d <= diagBonus; d++)
                {
                    var np = currentGridPos + dir * d;
                    var t  = board.GetTileAt(np);
                    if (t == null) break;

                    if (!t.isOccupied) moves.Add(np);
                    else
                    {
                        var other = t.currentOccupant.GetComponent<Piece>();
                        if (other != null && IsEnemy(other)) moves.Add(np);
                        break;
                    }
                }
            }
        }

        return moves;
    }
}
