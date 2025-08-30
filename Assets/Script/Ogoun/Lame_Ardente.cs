using System.Collections.Generic;
using UnityEngine;

public class Lame_Ardente : Piece
{
    public override List<Vector2Int> GetAvailableMoves(BoardManager b)
    {
        var moves = new List<Vector2Int>();
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            var p = currentGridPos;
            for (int i = 0; i < 2; i++)
            {
                p += d;
                var t = b.GetTileAt(p);
                if (t == null) break;

                if (t.currentOccupant == null) moves.Add(p);
                else
                {
                    var other = t.currentOccupant.GetComponent<Piece>();
                    if (other != null && IsEnemy(other)) moves.Add(p);
                    break; // stoppe sur obstacle
                }
            }
        }
        return moves;
    }

    // “Ignore protections” : si tu as des auras/boucliers ailleurs, checke ce tag depuis leur code.
}
