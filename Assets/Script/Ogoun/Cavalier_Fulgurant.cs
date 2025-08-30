using System.Collections.Generic;
using UnityEngine;

public class Cavalier_Fulgurant : Piece, ISpecialMoveResolver
{
    // y+ pour Rouge, y- pour Bleu (à adapter si ton plateau est inversé)
    int ForwardSign => isRed ? +1 : -1;

    public override List<Vector2Int> GetAvailableMoves(BoardManager b)
{
    var moves = new List<Vector2Int>();
    var pos   = currentGridPos;

    int forward = isRed ? +1 : -1;

    // ── AVANT : 1 à 3 cases, même X, wrap vertical, traverse tout ──
    for (int step = 1; step <= 3; step++)
    {
        int ty = pos.y + step * forward;
        ty %= b.height; if (ty < 0) ty += b.height;

        var p = new Vector2Int(pos.x, ty);
        var t = b.GetTileAt(p);
        if (t == null) continue;

        if (t.currentOccupant == null)
        {
            moves.Add(p);
        }
        else
        {
            var other = t.currentOccupant.GetComponent<Piece>();
            if (other != null && IsEnemy(other) && !(other is Sentinelle_Ecarlate))
                moves.Add(p); // capture OK, sauf Sentinelle
        }
        // pas de break: il "traverse" les pièces intermédiaires
    }

    return moves;
}



    // === Effet spécial: tuer TOUT ennemi traversé en ligne droite vers l’avant ===
    public SpecialMoveEffect ResolveSpecial(BoardManager board, Piece self, Vector2Int from, Vector2Int to, Piece victimOnDestination)
{
    var effect = new SpecialMoveEffect
    {
        extraVictimViewIds = new List<int>()
    };
        if (victimOnDestination is null)
            return effect;
    // Seulement si on avance tout droit (même X), avec wrap vertical autorisé,
            // et sur 1..3 cases dans la direction "avant" du Cavalier.
            if (to.x != from.x) return effect;

    int H    = board.height;
    int sign = isRed ? +1 : -1;

    // Nombre de pas "vers l'avant" en tenant compte du wrap:
    // steps = ((to.y - from.y) * sign) modulo H, mis dans [0..H-1]
    int dy    = to.y - from.y;
    int steps = ((dy * sign) % H + H) % H;

    if (steps < 1 || steps > 3) return effect; // pas un move avant 1..3 → aucun effet

    // Balaye les cases INTERMÉDIAIRES (exclut la destination), avec wrap
    for (int i = 1; i < steps; i++)
    {
        int y = from.y + i * sign;
        y %= H; if (y < 0) y += H;

        var t = board.GetTileAt(new Vector2Int(from.x, y));
        if (t == null || t.currentOccupant == null) continue;

        var other = t.currentOccupant.GetComponent<Piece>();
        if (other == null) continue;

        // Traverse les alliés sans effet; ignore la sentinelle (indestructible)
        if (!IsEnemy(other)) continue;
        if (other is Sentinelle_Ecarlate) continue;

        if (other.photonView != null)
            effect.extraVictimViewIds.Add(other.photonView.ViewID);
    }

    // La capture éventuelle sur la case d'arrivée est gérée ailleurs.
    return effect;
}

}
