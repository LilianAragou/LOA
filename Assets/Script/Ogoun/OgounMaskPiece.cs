using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Masque d’Ogoun : se déplace d’UNE case dans toutes les directions (8 voisins).
/// </summary>
public class Ogoun_Mask : MaskPiece
{
    public override List<Vector2Int> GetAvailableMoves(BoardManager board)
    {
        var moves = new List<Vector2Int>();

        // 8 directions (1 pas)
        AddStep(moves, board, currentGridPos + Vector2Int.up);
        AddStep(moves, board, currentGridPos + Vector2Int.down);
        AddStep(moves, board, currentGridPos + Vector2Int.left);
        AddStep(moves, board, currentGridPos + Vector2Int.right);
        AddStep(moves, board, currentGridPos + new Vector2Int(1, 1));
        AddStep(moves, board, currentGridPos + new Vector2Int(1, -1));
        AddStep(moves, board, currentGridPos + new Vector2Int(-1, 1));
        AddStep(moves, board, currentGridPos + new Vector2Int(-1, -1));

        return moves;
    }

    // Hooks facultatifs si tu veux ajouter quelque chose au début/fin de tour :
    // protected override void OnTurnStart() { }
    // protected override void OnTurnEnd()   { base.OnTurnEnd(); }
}
