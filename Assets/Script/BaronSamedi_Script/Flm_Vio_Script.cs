using UnityEngine;
using System.Collections.Generic;

public class FlammeViolettePiece : Piece
{
    // Pour ne générer qu'une fois par tour
    private bool hasGeneratedThisTurn = false;

    public override List<Vector2Int> GetAvailableMoves(BoardManager board)
    {
        // Immobile
        return new List<Vector2Int>();
    }

    protected override void OnTurnStart()
    {
        if (hasGeneratedThisTurn) 
            return;

        hasGeneratedThisTurn = true;
        
        // Récupère l'instance de BaronSamediMaskPiece (il y en a une par équipe, ou une seule selon ton setup)
        var baron = FindFirstObjectByType<BaronSamediMaskPiece>();
        if (baron != null)
        {
            baron.GainShadowPoint();
        }
        else
        {
        }
    }

    protected override void OnTurnEnd()
    {
        // On réarme la génération pour le tour suivant
        hasGeneratedThisTurn = false;
    }
}
