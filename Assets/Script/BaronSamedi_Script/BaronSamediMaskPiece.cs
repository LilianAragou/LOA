using UnityEngine;
using TMPro;
using NUnit.Framework.Internal;
using JetBrains.Annotations;
using Unity.VisualScripting;
using Unity.Hierarchy;

// Hérite de MaskPiece pour réutiliser les déplacements diagonaux+orthogonaux
public class BaronSamediMaskPiece : MaskPiece
{
    [Header("UI")]
    private TMP_Text  shadowPointsText;

    // Compteur de Points d'Ombre
    private int shadowPoints = 0;
    private int cost = 0;
    public Vector2Int position;




    protected override void Start()
    {




        base.Start();
        // S'abonner aux événements de capture et destruction
        TurnManager.Instance.OnPieceCaptured += OnSpiritCaptured;
        TurnManager.Instance.OnPieceDestroyed += OnSpiritDestroyed;
        position = currentGridPos;
        UpdateShadowUI();
    }

    protected override void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPieceCaptured -= OnSpiritCaptured;
            TurnManager.Instance.OnPieceDestroyed -= OnSpiritDestroyed;
        }
        base.OnDestroy();
    }

    // Quand un esprit est capturé (Action<Piece,Piece>)
    private void OnSpiritCaptured(Piece attacker, Piece victim)
    {
        GainShadowPoint();
    }

    // Quand un esprit est détruit hors capture (Action<Piece>)
    private void OnSpiritDestroyed(Piece victim)
    {
        GainShadowPoint();
    }
    public int GetShadowPoints() => shadowPoints;
    public void GainShadowPoint()
    {
        shadowPoints++;
        UpdateShadowUI();
    }

    private void UpdateShadowUI()
    {
        if (shadowPointsText != null)
            shadowPointsText.text = $"PO : {shadowPoints}";
    }
    public int GetUpgradeCost() => cost; // où `cost` est le PO requis pour évoluer
    public void SpendShadowPoints(int amount)
{
    shadowPoints = Mathf.Max(0, shadowPoints - Mathf.Max(0, amount));
    UpdateShadowUI(); // <- important pour rafraîchir l’UI
}




    // Pas d'override de Start ou GetAvailableMoves (hérités de MaskPiece)
}
