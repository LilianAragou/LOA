using System.Collections.Generic;
using UnityEngine;

public class DevoreurDAmesPiece : Piece
{
    // Bonus de portée diagonale (max 3)
    private int diagBonus = 0;

    // À la capture, on augmente le bonus (max 3)
    protected override void OnCapture(Piece victim)
    {
        diagBonus = Mathf.Min(3, diagBonus + 1);
    }

    // Les déplacements : orthogonaux et diagonales, avec bonuses
    public override List<Vector2Int> GetAvailableMoves(BoardManager board)
    {
        
        var moves = new List<Vector2Int>();

        // 1) Orthogonaux : baseRange = 1, + tempRangeBonus
        int orthoRange = GetEffectiveRange(1);
        Vector2Int[] orthos = {
            Vector2Int.up, Vector2Int.down,
            Vector2Int.left, Vector2Int.right
        };
        foreach (var dir in orthos)
        {
            for (int dist = 1; dist <= orthoRange; dist++)
            {
                Vector2Int np = currentGridPos + dir * dist;
                Tile t = board.GetTileAt(np);
                if (t == null) break; // hors plateau

                if (!t.isOccupied)
                {
                    moves.Add(np);
                }
                else
                {
                    var other = t.currentOccupant?.GetComponent<Piece>();
                    if (other != null && IsEnemy(other))
                        moves.Add(np);
                    break; // arrêt sur collision
                }
            }
        }

        // 2) Diagonales : baseRange = diagBonus, + tempRangeBonus
        int diagRange = GetEffectiveRange(diagBonus);
        if (diagRange > 0)
        {
            Vector2Int[] diags = {
                new Vector2Int(1,  1), new Vector2Int( 1, -1),
                new Vector2Int(-1,  1), new Vector2Int(-1, -1)
            };
            foreach (var dir in diags)
            {
                for (int dist = 1; dist <= diagRange; dist++)
                {
                    Vector2Int np = currentGridPos + dir * dist;
                    Tile t = board.GetTileAt(np);
                    if (t == null) break; // hors plateau

                    if (!t.isOccupied)
                    {
                        moves.Add(np);
                    }
                    else
                    {
                        var other = t.currentOccupant?.GetComponent<Piece>();
                        if (other != null && IsEnemy(other))
                            moves.Add(np);
                        break; // arrêt sur collision
                    }
                }
            }
        }

        return moves;
    }
}
