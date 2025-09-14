using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dévoreur d’Âme (Baron) :
/// - Déplacement : uniquement ORTHOGONAL sur cases vides (glisse par défaut).
/// - Capture : uniquement sur les 4 DIAGONALES à 1 case (si ennemi présent).
///   -> Impossible de capturer en orthogonal.
///   -> Impossible d’aller sur une diagonale vide.
/// </summary>
public class DevoreurDAmesPiece : Piece
{
    [Header("Déplacement orthogonal")]
    [Tooltip("Si true, la pièce peut glisser en ligne/colonne jusqu'à obstacle. Si false, 1 case max.")]
    [SerializeField] private bool slideOrthogonally = true;

    [Tooltip("Portée max en orthogonal si slideOrthogonally=false (1 = une case)")]
    [SerializeField] private int orthMaxSteps = 1;

    [Header("Capture en diagonale")]
    [Tooltip("Offsets de capture autorisés (par défaut : diagonales à 1).")]
    [SerializeField] private Vector2Int[] diagonalCaptureOffsets =
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };

    public override List<Vector2Int> GetAvailableMoves(BoardManager board)
    {
        var moves = new List<Vector2Int>();
        var origin = currentGridPos;

        // ============ 1) DÉPLACEMENT : ORTHOGONAL UNIQUEMENT ============

        // Directions orthogonales
        Vector2Int[] orthDirs = {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        if (slideOrthogonally)
        {
            // On glisse jusqu'à obstacle (cases vides uniquement)
            foreach (var dir in orthDirs)
            {
                var pos = origin + dir;
                while (true)
                {
                    var t = board.GetTileAt(pos);
                    if (t == null) break; // bord du plateau

                    if (t.currentOccupant == null)
                    {
                        // Case vide → on peut s'y déplacer
                        moves.Add(pos);
                        pos += dir; // continue
                    }
                    else
                    {
                        // Occupé → on ne capture PAS en orthogonal
                        break;
                    }
                }
            }
        }
        else
        {
            // Portée limitée (par défaut 1 case)
            int steps = Mathf.Max(1, orthMaxSteps);
            foreach (var dir in orthDirs)
            {
                for (int d = 1; d <= steps; d++)
                {
                    var pos = origin + dir * d;
                    var t = board.GetTileAt(pos);
                    if (t == null) break;

                    if (t.currentOccupant == null)
                    {
                        moves.Add(pos);
                    }
                    else
                    {
                        // Occupé → pas de capture orthogonale
                        break;
                    }
                }
            }
        }

        // ============ 2) CAPTURE : DIAGONALES UNIQUEMENT (cases violettes) ============

        foreach (var off in diagonalCaptureOffsets)
        {
            var pos = origin + off;
            var t = board.GetTileAt(pos);
            if (t == null) continue;

            // On ne peut aller en diagonale QUE s'il y a un ennemi (capture)
            if (t.currentOccupant != null)
            {
                var other = t.currentOccupant.GetComponent<Piece>();
                if (other != null && IsEnemy(other))
                {
                    // BoardManager rejettera de toute façon la capture d'une Sentinelle,
                    // mais on propose tout de même la case ici pour l'UX.
                    moves.Add(pos);
                }
            }
        }

        // Debug rapide (optionnel)
        // Debug.Log($"[DevoreurDAmes] {name} @ {origin} -> moves={moves.Count}");

        return moves;
    }

#if UNITY_EDITOR
    // Gizmos pour voir le pattern en mode éditeur (sélectionné)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.2f, 1f, 0.6f); // violet
        var origin = Application.isPlaying ? (Vector3)transform.position : transform.position;

        // Montre les cibles potentielles de capture diagonale (à 1)
        foreach (var off in diagonalCaptureOffsets)
        {
            var p = origin + new Vector3(off.x, off.y, 0f);
            Gizmos.DrawWireCube(p, Vector3.one * 0.8f);
        }
    }
#endif
}
