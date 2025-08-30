using System.Collections.Generic;
using UnityEngine;

public class Sentinelle_Ecarlate : Piece, IAdjacencyKiller
{
    public override List<Vector2Int> GetAvailableMoves(BoardManager b)
    {
        // immobile par design
        return new List<Vector2Int>();
    }
    // Rien à coder ici : le BoardManager tuera les ennemis qui entrent à côté.
}
