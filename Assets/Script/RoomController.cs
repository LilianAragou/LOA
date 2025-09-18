using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoomController : MonoBehaviourPunCallbacks
{
    [Header("UI")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Button createBtn;
    [SerializeField] private Button joinBtn;

    [Header("Options")]
    [Tooltip("Laissez vide pour laisser Photon choisir. Exemple: \"eu\"")]
    [SerializeField] private string fixedRegion = "";     // "eu" recommandé si vous voulez tout forcer
    [SerializeField] private byte maxPlayers = 2;         // ajustez selon votre jeu
    [SerializeField] private bool autoSyncScene = true;   // recommandé

    private bool isReady;         // prêt (connecté + dans un lobby)
    private bool isOpInFlight;    // opération Create/Join en cours (anti double-clic)

    // ---------- LIFECYCLE ----------
    private void Awake()
    {
        if (autoSyncScene) PhotonNetwork.AutomaticallySyncScene = true;

        // GameVersion = filtre logique côté Photon (tous les joueurs doivent partager la même)
        PhotonNetwork.GameVersion = Application.version;

        // Optionnel: fixer la région si souhaité
        if (!string.IsNullOrEmpty(fixedRegion))
        {
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = fixedRegion;
        }

        SetButtonsInteractable(false);

        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("[RoomController] Connecting to Photon...");
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            // Déjà connecté (retour au menu, etc.)
            TryJoinLobbyIfNeeded();
        }

        // Sécurise si les références UI ne sont pas assignées dans l’inspector
        if (roomNameInput == null)
            roomNameInput = FindObjectOfType<TMP_InputField>();

        if (createBtn != null) createBtn.onClick.AddListener(OnCreateRoom);
        if (joinBtn   != null) joinBtn.onClick.AddListener(OnJoinRoom);
    }

    private void OnDestroy()
    {
        if (createBtn != null) createBtn.onClick.RemoveListener(OnCreateRoom);
        if (joinBtn   != null) joinBtn.onClick.RemoveListener(OnJoinRoom);
    }

    // ---------- UI HANDLERS ----------
    public void OnCreateRoom()
    {
        if (!GuardReadyForOp()) return;

        string baseName = (roomNameInput != null ? roomNameInput.text : "").Trim();
        if (string.IsNullOrEmpty(baseName))
        {
            baseName = $"Room-{Random.Range(1000, 9999)}";
            if (roomNameInput != null) roomNameInput.text = baseName;
        }

        var opts = BuildDefaultRoomOptions();

        isOpInFlight = true;
        SetButtonsInteractable(false);
        bool sent = PhotonNetwork.CreateRoom(baseName, opts, TypedLobby.Default);
        Debug.Log($"[RoomController] CreateRoom('{baseName}') sent={sent}");
    }

    public void OnJoinRoom()
    {
        if (!GuardReadyForOp()) return;

        string roomName = (roomNameInput != null ? roomNameInput.text : "").Trim();
        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogWarning("[RoomController] JoinRoom: nom vide.");
            return;
        }

        isOpInFlight = true;
        SetButtonsInteractable(false);
        bool sent = PhotonNetwork.JoinRoom(roomName);
        Debug.Log($"[RoomController] JoinRoom('{roomName}') sent={sent}");
    }

    // ---------- PHOTON CALLBACKS ----------
    public override void OnConnectedToMaster()
    {
        Debug.Log($"[RoomController] ConnectedToMaster (region={PhotonNetwork.CloudRegion}) -> JoinLobby");
        TryJoinLobbyIfNeeded();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("[RoomController] JoinedLobby => READY");
        isReady = true;
        if (!isOpInFlight) SetButtonsInteractable(true);
    }

    public override void OnLeftLobby()
    {
        Debug.Log("[RoomController] LeftLobby");
        isReady = false;
        SetButtonsInteractable(false);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogError($"[RoomController] Disconnected: {cause}");
        isReady = false;
        isOpInFlight = false;
        SetButtonsInteractable(false);
    }

    public override void OnCreatedRoom()
    {
        Debug.Log("[RoomController] Room créée 🎉 (MasterClient)");
        // Charge la scène de jeu côté Master; les autres suivront si AutomaticallySyncScene = true
        PhotonNetwork.LoadLevel("Game");
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("[RoomController] Room rejointe ✅");
        // Si vous JOIGNEZ une room déjà existante, et que le Master a déjà chargé "Game",
        // AutomaticallySyncScene vous emmènera automatiquement. Sinon, à vous de gérer.
        isOpInFlight = false;
        SetButtonsInteractable(false);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[RoomController] Échec création : code={returnCode} msg={message}");

        // Gestion simple d’un nom déjà pris / collision : retente avec suffixe
        string baseName = (roomNameInput != null ? roomNameInput.text : "Room").Trim();
        string alt = $"{baseName}-{Random.Range(1000, 9999)}";
        var opts = BuildDefaultRoomOptions();

        Debug.Log($"[RoomController] Retry CreateRoom with '{alt}'");
        bool sent = PhotonNetwork.CreateRoom(alt, opts, TypedLobby.Default);
        if (!sent)
        {
            isOpInFlight = false;
            SetButtonsInteractable(isReady);
        }
        else
        {
            if (roomNameInput != null) roomNameInput.text = alt;
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[RoomController] Échec jointure : code={returnCode} msg={message}");
        isOpInFlight = false;
        SetButtonsInteractable(isReady);
    }

    public override void OnLeftRoom()
    {
        Debug.Log("[RoomController] OnLeftRoom");
        isOpInFlight = false;
        // On revient généralement à un menu : on veut pouvoir créer/rejoindre à nouveau
        SetButtonsInteractable(isReady);
    }

    // ---------- HELPERS ----------
    private void TryJoinLobbyIfNeeded()
    {
        // Important : être dans un lobby avant d’utiliser les listes ou JoinRandom.
        // Pour JoinRoom par nom exact, ce n’est pas obligatoire, mais rester cohérent évite des états bizarres.
        if (!PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinLobby(TypedLobby.Default);
        }
        else
        {
            isReady = true;
            if (!isOpInFlight) SetButtonsInteractable(true);
        }
    }

    private RoomOptions BuildDefaultRoomOptions()
    {
        return new RoomOptions
        {
            MaxPlayers = maxPlayers,
            IsOpen = true,
            IsVisible = true,

            // Évite les “ghost slots” si vous ne gérez pas ReconnectAndRejoin
            PlayerTtl = 0,
            EmptyRoomTtl = 0,

            // Si vous filtrez par propriétés côté lobby, exposez-les aussi ici :
            // CustomRoomProperties = new ExitGames.Client.Photon.Hashtable { {"mode","ranked"} },
            // CustomRoomPropertiesForLobby = new string[] { "mode" }
        };
    }

    private bool GuardReadyForOp()
    {
        if (isOpInFlight)
        {
            Debug.Log("[RoomController] Op en cours, ignore.");
            return false;
        }
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("[RoomController] Pas prêt (connexion non finalisée).");
            return false;
        }
        if (!PhotonNetwork.InLobby)
        {
            Debug.LogWarning("[RoomController] Pas dans un lobby, on y va d’abord.");
            TryJoinLobbyIfNeeded();
            return false;
        }
        if (PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[RoomController] Déjà dans une room. Attendez OnLeftRoom.");
            return false;
        }
        return true;
    }

    private void SetButtonsInteractable(bool enabled)
    {
        if (createBtn != null) createBtn.interactable = enabled;
        if (joinBtn   != null) joinBtn.interactable   = enabled;
    }
}
