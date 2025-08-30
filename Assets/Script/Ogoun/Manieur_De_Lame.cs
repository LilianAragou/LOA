using System.Collections.Generic;
using UnityEngine;

public class Manieur_De_Lame : Piece, ISpecialMoveResolver
{
    // ── Déplacements en L (cavalier d'échecs) ─────────────────────────
    public override List<Vector2Int> GetAvailableMoves(BoardManager b)
    {
        var moves = new List<Vector2Int>();
        var pos   = currentGridPos;

        // 8 offsets d’un cavalier
        Vector2Int[] deltas = new Vector2Int[]
        {
            new Vector2Int(+2, +1), new Vector2Int(+2, -1),
            new Vector2Int(-2, +1), new Vector2Int(-2, -1),
            new Vector2Int(+1, +2), new Vector2Int(+1, -2),
            new Vector2Int(-1, +2), new Vector2Int(-1, -2),
        };

        foreach (var d in deltas)
        {
            var p = pos + d;
            var t = b.GetTileAt(p);
            if (t == null) continue;

            if (t.currentOccupant == null)
                moves.Add(p);
            else
            {
                var other = t.currentOccupant.GetComponent<Piece>();
                if (other != null && IsEnemy(other))
                    moves.Add(p); // capture autorisée
            }
        }

        return moves;
    }

    // ── Effet spécial : si la destination est une capture, tue aussi l’ennemi sur le "coude orthogonal" du L ──
    public SpecialMoveEffect ResolveSpecial(BoardManager board,
                                            Piece self,
                                            Vector2Int from,
                                            Vector2Int to,
                                            Piece victimOnDestination)
    {
        var effect = new SpecialMoveEffect
        {
            extraVictimViewIds = new List<int>()
        };

        // L’effet ne s’applique QUE s’il y a capture sur la case d’arrivée.
        if (victimOnDestination == null)
            return effect;

        int dx = to.x - from.x;
        int dy = to.y - from.y;
        int adx = Mathf.Abs(dx);
        int ady = Mathf.Abs(dy);

        // Vérifier qu’on est bien sur un saut en L
        if (!((adx == 2 && ady == 1) || (adx == 1 && ady == 2)))
            return effect;

        // Coin orthogonal du "L"
        // - Si déplacement 2 en X et 1 en Y → coin = (from.x + sign(dx), from.y)
        // - Si déplacement 1 en X et 2 en Y → coin = (from.x, from.y + sign(dy))
        Vector2Int corner;
        if (adx == 2 && ady == 1)
            corner = new Vector2Int(from.x + dx, from.y);
        else // (adx == 1 && ady == 2)
            corner = new Vector2Int(from.x, from.y + dy);

        var tCorner = board.GetTileAt(corner);
        if (tCorner != null && tCorner.currentOccupant != null)
        {
            var cornerPiece = tCorner.currentOccupant.GetComponent<Piece>();
            if (cornerPiece != null && IsEnemy(cornerPiece))
            {
                effect.extraVictimViewIds.Add(cornerPiece.photonView.ViewID);
#if UNITY_EDITOR
                Debug.Log($"[Manieur_De_Lame] Extra kill au coude {corner} -> {cornerPiece.name}");
#endif
            }
        }

        return effect;
    }
}
