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

    void OnEnable()
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
        if (victim.isRed && victim is MaskPiece)
        {
            // Le masque BLEU est mort -> Victoire ROUGE
            ShowVictoryAll(redWinsText);
        }
        else if (!victim.isRed && victim is MaskPiece)
        {
            // Le masque ROUGE est mort -> Victoire BLEUE
            ShowVictoryAll(blueWinsText);
        }
    }

    private void ShowVictoryAll(string message)
    {
        if (gameOver) return;
        gameOver = true;

        if (photonView != null && PhotonNetwork.InRoom)
            photonView.RPC(nameof(RPC_ShowVictory), RpcTarget.All, message);
        else
            RPC_ShowVictory(message); // offline
    }

    [PunRPC]
    private void RPC_ShowVictory(string message)
    {
        if (victoryText)  victoryText.text = message;
        if (victoryPanel) victoryPanel.SetActive(true);

        // (Optionnel) : geler les interactions ici si tu veux
        // InputManager.Instance?.DisableAllInputs();
        // TurnManager.Instance?.StopMatch(); // seulement si tu as un tel hook
    }
}
