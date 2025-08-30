using System.Collections.Generic;
using UnityEngine;

public class HurleurCreuxPiece : Piece
{
    // Pour chaque allié buffé, nombre de tours restants
    private readonly Dictionary<Piece, int> buffDurations = new Dictionary<Piece, int>();

    protected override void OnAllyDeath(Piece ally)
    {
        // Seulement si c'est un véritable allié
        if (ally == this || ally.isRed != isRed) 
            return;

        // Distance Chebyshev pour tester ≤2 cases
        int dx = Mathf.Abs(ally.currentGridPos.x - currentGridPos.x);
        int dy = Mathf.Abs(ally.currentGridPos.y - currentGridPos.y);
        if (Mathf.Max(dx, dy) > 2) 
            return;

        ApplyBuff();
    }

    private void ApplyBuff()
    {
        // Balayage 5×5 autour du hurleur
        for (int dx = -2; dx <= 2; dx++)
        for (int dy = -2; dy <= 2; dy++)
        {
            var pos  = currentGridPos + new Vector2Int(dx, dy);
            var tile = BoardManager.Instance.GetTileAt(pos);
            if (tile?.currentOccupant == null) continue;

            var ally = tile.currentOccupant.GetComponent<Piece>();
            if (ally == null || ally == this || ally.isRed != isRed) 
                continue;

            if (buffDurations.ContainsKey(ally))
            {
                // Réarme à 3 tours
                buffDurations[ally] = 3;
            }
            else
            {
                // Nouveau buff : +1 portée
                buffDurations.Add(ally, 3);
                ally.TempIncreaseRange(1);
            }

            // On rafraîchit immédiatement son highlight
            BoardManager.Instance.ShowPossibleMoves(ally);
        }
    }

    protected override void OnTurnStart()
    {
        // On décrémente et purge SEULEMENT les buffs expirés
        var toExpire = new List<Piece>(buffDurations.Keys);
        foreach (var ally in toExpire)
        {
            if (ally == null)
            {
                buffDurations.Remove(ally);
                continue;
            }

            buffDurations[ally]--;
            if (buffDurations[ally] <= 0)
            {
                buffDurations.Remove(ally);
                ally.TempResetRange();
            }
        }
    }

    protected override void OnTurnEnd()
    {
        // Ne rien faire : le reset global de portée est géré en OnTurnEnd() de Piece
    }

    public override List<Vector2Int> GetAvailableMoves(BoardManager board)
    {
        var moves = new List<Vector2Int>();
        int range = GetEffectiveRange(1);  // baseRange = 1

        // Directions orthogonales + diagonales
        Vector2Int[] dirs = {
            Vector2Int.up,    Vector2Int.down,
            Vector2Int.left,  Vector2Int.right,
            new Vector2Int( 1,  1), new Vector2Int( 1, -1),
            new Vector2Int(-1,  1), new Vector2Int(-1, -1),
        };

        foreach (var d in dirs)
        {
            for (int dist = 1; dist <= range; dist++)
            {
                var np = currentGridPos + d * dist;
                var t  = board.GetTileAt(np);
                if (t == null) break;

                if (!t.isOccupied)
                {
                    moves.Add(np);
                }
                else
                {
                    var other = t.currentOccupant?.GetComponent<Piece>();
                    if (other != null && IsEnemy(other))
                        moves.Add(np);
                    break;
                }
            }
        }

        return moves;
    }
}
