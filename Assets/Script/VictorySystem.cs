using UnityEngine;
using Photon.Pun;
using TMPro;

public class VictorySystem : MonoBehaviourPun
{
    [Header("UI")]
    [Tooltip("Panel à afficher quand la partie est gagnée")]
    public GameObject victoryPanel;
    [Tooltip("Texte où écrire 'Victoire des rouges/bleus'")]
    public TextMeshProUGUI victoryText;

    [Header("Messages")]
    public string redWinsText  = "Victoire des rouges";
    public string blueWinsText = "Victoire des bleus";

    private bool gameOver = false;

    void Awake()
    {
        if (victoryPanel) victoryPanel.SetActive(false);
    }

    void Start()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPieceCaptured  += OnPieceCaptured;
            TurnManager.Instance.OnPieceDestroyed += OnPieceDestroyed;
        }
    }

    void OnDisable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPieceCaptured  -= OnPieceCaptured;
            TurnManager.Instance.OnPieceDestroyed -= OnPieceDestroyed;
        }
    }

    // ——— Events venant de BoardManager via TurnManager ———
    private void OnPieceCaptured(Piece attacker, Piece victim)
    {
        TryHandleMaskDeath(victim);
    }

    private void OnPieceDestroyed(Piece victim)
    {
        TryHandleMaskDeath(victim);
    }

    private void TryHandleMaskDeath(Piece victim)
    {
        if (gameOver || victim == null) return;
        // Masques :
        // - Rouge = Ogoun_Mask
        // - Bleu  = BaronSamediMaskPiece
        if (victim is BaronSamediMaskPiece)
        {
            // Le masque BLEU est mort -> Victoire ROUGE
            FindObjectOfType<RoomManager>().MakeEveryoneLeave(redWinsText);
        }
        else if (victim is Ogoun_Mask)
        {
            // Le masque ROUGE est mort -> Victoire BLEUE
            FindObjectOfType<RoomManager>().MakeEveryoneLeave(blueWinsText);
        }
    }
}
