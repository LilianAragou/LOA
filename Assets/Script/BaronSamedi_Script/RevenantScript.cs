using System.Collections.Generic;
using UnityEngine;

public class RevenantPiece : Piece
{
    // Compte le nombre de tours COMPLETS de l’équipe sans capture
    private int teamTurnsWithoutCapture = 0;

    protected override void Start()
    {
        base.Start();
        // Abonnement aux événements de capture et de fin de tour
        TurnManager.Instance.OnPieceCaptured += HandleAnyCapture;
        TurnManager.Instance.OnTurnEnd     += OnAnyTurnEnd;
    }

    protected override void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPieceCaptured -= HandleAnyCapture;
            TurnManager.Instance.OnTurnEnd     -= OnAnyTurnEnd;
        }
        base.OnDestroy();
    }

    // Réinitialise le compteur dès qu’un allié capture
    private void HandleAnyCapture(Piece attacker, Piece victim)
    {
        if (attacker.isRed == isRed)
        {
            teamTurnsWithoutCapture = 0;
        }
    }

    // À la fin de chaque tour de notre équipe, on incrémente et éventuellement on explose
    private void OnAnyTurnEnd()
    {
        int endingPlayer = TurnManager.Instance.CurrentPlayer;
        if (endingPlayer == (isRed ? 0 : 1))
        {
            teamTurnsWithoutCapture++;
            if (teamTurnsWithoutCapture >= 2)
            {
                Explode();
            }
        }
    }

    // Déplacements orthogonaux avec prise en compte du bonus temporaire (baseRange = 1)
    public override List<Vector2Int> GetAvailableMoves(BoardManager board)
    {
        var moves = new List<Vector2Int>();
        int range = GetEffectiveRange(1);

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            for (int dist = 1; dist <= range; dist++)
            {
                Vector2Int np = currentGridPos + d * dist;
                Tile t = board.GetTileAt(np);
                if (t == null) break;
                if (!t.isOccupied)
                {
                    moves.Add(np);
                }
                else
                {
                    Piece other = t.currentOccupant?.GetComponent<Piece>();
                    if (other != null && IsEnemy(other))
                        moves.Add(np);
                    break;
                }
            }
        }

        return moves;
    }

    // Explosion : détruit les pièces orthogonales adjacentes puis se détruit
    private void Explode()
    {
        // Assure-toi d'avoir une référence au BoardManager
        BoardManager bm = board ?? BoardManager.Instance;
        if (bm == null)
        {
            return;
        }

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            Vector2Int np = currentGridPos + d;
            Tile t = bm.GetTileAt(np);
            if (t == null)
            {
                continue;
            }
            if (t.currentOccupant != null)
            {
                Piece p = t.currentOccupant.GetComponent<Piece>();
                if (p != null)
                {
                    // Retirer la référence avant destruction
                    t.SetOccupant(null);
                    TurnManager.Instance.NotifyDestruction(p);
                    Destroy(p.gameObject);
                }
            }
        }

        // Retirer l'occupant de la case du Revenant avant auto-destruction
        Tile selfTile = bm.GetTileAt(currentGridPos);
        if (selfTile != null)
            selfTile.SetOccupant(null);

        TurnManager.Instance.NotifyDestruction(this);
        Destroy(gameObject);
    }
}
