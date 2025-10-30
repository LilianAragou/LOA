using System;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoaSelectUI : MonoBehaviourPun
{
    [Serializable]
    public class LoaButton
    {
        public string loaId;   // == LoaDefinition.loaId
        public Button button;
    }

    [Header("Boutons (6)")]
    [SerializeField] private LoaButton[] buttons;

    [Header("Nom de la scène de jeu")]
    [SerializeField] private string gameSceneName = "Game";

    private LoaDefinition[] allDefs;

    void Awake()
    {
        allDefs = Resources.LoadAll<LoaDefinition>("LoaDefinitions");
        if (allDefs == null || allDefs.Length == 0)
            Debug.LogWarning("Aucune LoaDefinition trouvée dans Resources/LoaDefinitions/");
    }

    void Start()
    {
        foreach (var b in buttons)
        {
            if (b?.button == null || string.IsNullOrEmpty(b.loaId)) continue;
            string id = b.loaId;
            b.button.onClick.RemoveAllListeners();
            b.button.onClick.AddListener(() => OnClickLoa(id));
        }
    }

    private void OnClickLoa(string loaId)
    {
        // Désactiver IMMÉDIATEMENT en local (anti-spam/retard réseau)
        SetButtonInteractable(loaId, false);

#if UNITY_EDITOR
        // Solo / hors room : un clic suffit → on lance la scène
            var cfg = EnsureGameConfig();
            var def = ResolveDef(loaId);
            cfg.player1Loa = def;
            cfg.player2Loa = def; // miroir simple
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
        return;
#else
        // Multi : annonce la sélection à tous
        photonView.RPC(nameof(RPC_SelectLoa), RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber, loaId);
#endif
    }

    [PunRPC]
    private void RPC_SelectLoa(int actorNumber, string newLoaId)
    {
        // Joueur qui a cliqué
        Player player = PhotonNetwork.CurrentRoom?.GetPlayer(actorNumber);
        if (player == null) return;

        // Ancien choix (s'il existe)
        string previousId = player.CustomProperties.TryGetValue("LOA_ID", out var prev) ? prev as string : null;

        // Met à jour sa propriété (source of truth)
        var props = new Hashtable { { "LOA_ID", newLoaId } };
        player.SetCustomProperties(props);

        // Réactive l'ancien bouton (si le joueur change d'avis)
        if (!string.IsNullOrEmpty(previousId))
            SetButtonInteractable(previousId, true);

        // Désactive le nouveau bouton pour TOUT LE MONDE
        SetButtonInteractable(newLoaId, false);

        // Si les deux joueurs ont un LOA_ID, on charge "Game"
        TryStartGameIfReady();
    }

    private void TryStartGameIfReady()
    {
        if (!PhotonNetwork.InRoom) return;
        var players = PhotonNetwork.PlayerList;
        if (players.Length < 2) return;

        string loaA = players[0].CustomProperties.TryGetValue("LOA_ID", out var la) ? la as string : null;
        string loaB = players[1].CustomProperties.TryGetValue("LOA_ID", out var lb) ? lb as string : null;
        if (string.IsNullOrEmpty(loaA) || string.IsNullOrEmpty(loaB)) return;

        if (PhotonNetwork.IsMasterClient)
        {
            // Optionnel : assigner des sides cohérents
            players[0].SetCustomProperties(new Hashtable { { "SIDE", 1 } });
            players[1].SetCustomProperties(new Hashtable { { "SIDE", 2 } });

            // Verrouiller la room si tu veux
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;

            // Charger la scène pour TOUS via RPC (et non PhotonNetwork.LoadLevel)
            photonView.RPC(nameof(RPC_LoadSceneForAll), RpcTarget.All, gameSceneName);
        }
    }

    [PunRPC]
    private void RPC_LoadSceneForAll(string sceneName)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    private void SetButtonInteractable(string loaId, bool value)
    {
        var entry = Array.Find(buttons, x => x.loaId == loaId);
        if (entry?.button != null)
            entry.button.interactable = value;
    }

    private GameConfig EnsureGameConfig()
    {
        if (GameConfig.Instance != null) return GameConfig.Instance;
        var go = new GameObject("GameConfig");
        var cfg = go.AddComponent<GameConfig>();
        DontDestroyOnLoad(go);
        return cfg;
    }

    private LoaDefinition ResolveDef(string id)
    {
        if (string.IsNullOrEmpty(id) || allDefs == null) return null;
        return allDefs.FirstOrDefault(d => d != null && d.loaId == id);
    }
}
