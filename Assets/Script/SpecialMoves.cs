using UnityEngine;
using System.Collections.Generic;

public struct SpecialMoveEffect
{
    // Si true, on empêche la capture par défaut (utile pour une poussée)
    public bool cancelDefaultCapture;

    // Pousser la victime (Ravageur)
    public bool pushVictim;
    public Vector2Int pushTo;

    // Victimes supplémentaires à détruire (Cavalier, Manieur, Sentinelle, etc.)
    public List<int> extraVictimViewIds;
}

public interface ISpecialMoveResolver
{
    /// <summary>
    /// Permet au pion de modifier la résolution du mouvement/capture.
    /// Appelé par le Master pendant RPC_RequestMove_Master, AVANT l’application.
    /// </summary>
    SpecialMoveEffect ResolveSpecial(BoardManager board, Piece self, 
                                     Vector2Int from, Vector2Int to, Piece landingVictim);
}

/// <summary> Marqueur pour les unités qui tuent ce qui entre en case adjacente. </summary>
public interface IAdjacencyKiller { }
