using System.Collections.Generic;
using UnityEngine;

public class MaskPiece : Piece
{
    // Déplacements diagonaux et orthogonaux avec prise en compte du bonus temporaire
    public override List<Vector2Int> GetAvailableMoves(BoardManager board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        int range = GetEffectiveRange(1); // portée de base = 1

        Vector2Int[] directions = new Vector2Int[]
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right,
            new Vector2Int( 1,  1),
            new Vector2Int( 1, -1),
            new Vector2Int(-1,  1),
            new Vector2Int(-1, -1)
        };

        foreach (var dir in directions)
        {
            for (int dist = 1; dist <= range; dist++)
            {
                Vector2Int targetPos = currentGridPos + dir * dist;
                Tile tile = board.GetTileAt(targetPos);
                if (tile == null)
                    break; // hors plateau

                if (!tile.isOccupied)
                {
                    moves.Add(targetPos);
                }
                else
                {
                    Piece otherPiece = tile.currentOccupant?.GetComponent<Piece>();
                    if (otherPiece != null && IsEnemy(otherPiece))
                    {
                        moves.Add(targetPos); // Capture possible
                    }
                    break; // on s'arrête après rencontre
                }
            }
        }

        return moves;
    }
}
