using UnityEngine;

public class GameConfig : MonoBehaviour
{
    public static GameConfig Instance { get; private set; }

    [Header("Selected Loa")]
    public LoaDefinition player1Loa;
    public LoaDefinition player2Loa; // utile pour hotseat / IA / multi local

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
