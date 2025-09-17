using System.Collections.Generic;
using UnityEngine;

/// Dévoreur d’Âme — Baron Samedi
/// - Déplacement : 1 case orthogonale vers une case vide (pas de capture orthogonale).
/// - Capture : saut (leap) uniquement vers les cases du motif suivant, SI la case d’arrivée
///            est occupée par un ennemi : (0,±4), (0,±2), (±2,0), (±4,0), (±3,±3).
public class DevoreurDAmesPiece : Piece
{
    // Offsets relatifs autorisés pour la capture (motif exact demandé)
    private static readonly Vector2Int[] DevourOffsets = new Vector2Int[]
    {
        // vertical ±2 et ±4
        new Vector2Int(0,  2), new Vector2Int(0, -2),
        new Vector2Int(0,  4), new Vector2Int(0, -4),

        // horizontal ±2 et ±4
        new Vector2Int( 2, 0), new Vector2Int(-2, 0),
        new Vector2Int( 4, 0), new Vector2Int(-4, 0),

        // diagonales ±3
        new Vector2Int( 3,  3), new Vector2Int( 3, -3),
        new Vector2Int(-3,  3), new Vector2Int(-3, -3),
    };

    public override List<Vector2Int> GetAvailableMoves(BoardManager board)
    {
        var moves = new List<Vector2Int>();
        var o = currentGridPos;

        // 1) Déplacements simples : cases orthogonales, portée buffée
        Vector2Int[] ortho = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in ortho)
        {
            int range = GetEffectiveRange(1); // base = 1, buff peut donner +1
            for (int dist = 1; dist <= range; dist++)
            {
                var p = o + d * dist;
                var t = board.GetTileAt(p);
                if (t == null) break;

                if (t.currentOccupant == null) // vide -> autorisé
                    moves.Add(p);
                else
                    break; // blocage : pas de capture orthogonale
            }
        }

        // 2) Captures spéciales : offsets fixes, non buffés
        foreach (var off in DevourOffsets)
        {
            var p = o + off;
            var t = board.GetTileAt(p);
            if (t == null) continue;

            if (t.currentOccupant != null)
            {
                var other = t.currentOccupant.GetComponent<Piece>();
                if (other != null && IsEnemy(other))
                {
                    moves.Add(p); // on "saute" sur la case ennemie
                }
            }
        }

        return moves;
    }

    protected override bool UsesDirection(Vector2Int dir)
    {
        // Le Dévoreur utilise uniquement les directions orthogonales pour ses déplacements buffables
        return dir == Vector2Int.up || dir == Vector2Int.down ||
               dir == Vector2Int.left || dir == Vector2Int.right;
    }

#if UNITY_EDITOR
    // Gizmo pratique pour visualiser le motif de capture autour de la pièce
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0f, 1f, 0.5f);
        var basePos = Application.isPlaying ? (Vector3)transform.position : transform.position;
        foreach (var off in DevourOffsets)
        {
            var p = basePos + new Vector3(off.x, off.y, 0f);
            Gizmos.DrawWireCube(p, Vector3.one * 0.85f);
        }
    }
#endif
}
