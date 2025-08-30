using System.Collections.Generic;
using UnityEngine;

public class Ravageur : Piece
{
    // 1 case toutes directions, capture possible (case vide ou ennemie).
    public override List<Vector2Int> GetAvailableMoves(BoardManager b)
    {
        var moves = new List<Vector2Int>();
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            var p = currentGridPos + new Vector2Int(dx, dy);

            // AddStep autorise soit une case vide, soit une case ennemie (capture).
            AddStep(moves, b, p);
        }
        return moves;
    }
}
