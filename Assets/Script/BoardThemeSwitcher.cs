using UnityEngine;
using Photon.Pun;      // pour vérifier que 2 joueurs sont bien dans la room
using Photon.Realtime;

public class BoardThemeSwitcher : MonoBehaviour
{
    [Header("Mode 1 — Un seul SpriteRenderer")]
    public SpriteRenderer targetRenderer;
    public Sprite redSprite;
    public Sprite blueSprite;

    [Header("Mode 2 — Deux SpriteRenderers (optionnel)")]
    public SpriteRenderer redRenderer;
    public SpriteRenderer blueRenderer;

    [Header("Options")]
    public bool invertColors = false;

    [Header("SFX de changement de tour")]
    [Tooltip("AudioSource utilisé pour jouer les sons (créé automatiquement si laissé vide)")]
    public AudioSource sfxSource;

    [Tooltip("Clip quand c'est au ROUGE (si non défini, on utilise 'genericClip' ou 'randomClips')")]
    public AudioClip redTurnClip;

    [Tooltip("Clip quand c'est au BLEU (si non défini, on utilise 'genericClip' ou 'randomClips')")]
    public AudioClip blueTurnClip;

    [Tooltip("Clip générique si red/blue sont vides (sinon ignoré)")]
    public AudioClip genericClip;

    [Tooltip("Pool facultatif de clips (on en choisit un aléatoirement quand red/blue/generic sont vides)")]
    public AudioClip[] randomClips;

    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("Variation aléatoire de pitch ± cette valeur (0 = désactivé)")]
    [Range(0f, 0.5f)] public float randomPitch = 0f;

    // On mémorise le dernier joueur pour éviter de jouer le SFX deux fois sur le même tour
    int lastPlayerPlayed = -2;

    // On n’active le SFX qu’après le premier vrai changement de tour (donc après la 1ère action)
    bool sfxArmedAfterFirstChange = false;

    void OnEnable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnMatchStarted += OnMatchStarted;
            TurnManager.Instance.OnTurnChanged  += OnTurnChanged; // arme le SFX ici
            TurnManager.Instance.OnTurnStart    += OnTurnStart;
        }

        // Met juste le visuel au bon état, sans jouer de son
        ApplyThemeFromTurn();

        // On initialise le "dernier joueur" observé pour éviter le bip dès l'arrivée
        if (TurnManager.Instance != null)
            lastPlayerPlayed = TurnManager.Instance.CurrentPlayer;
    }

    void OnDisable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnMatchStarted -= OnMatchStarted;
            TurnManager.Instance.OnTurnChanged  -= OnTurnChanged;
            TurnManager.Instance.OnTurnStart    -= OnTurnStart;
        }
    }

    void OnMatchStarted()
    {
        // Les deux joueurs peuvent ne pas être encore là (selon ta logique réseau),
        // on n’arme pas le SFX et on ne joue rien ici.
        ApplyThemeFromTurn();
    }

    void OnTurnChanged()
    {
        // Le changement de tour reflète qu’un coup a été joué.
        ApplyThemeFromTurn();

        // On arme le SFX seulement si on a bien une partie "valide" (2 joueurs présents).
        if (!sfxArmedAfterFirstChange && PlayersReady() && TurnManager.Instance != null && TurnManager.Instance.Started)
            sfxArmedAfterFirstChange = true;

        MaybePlayTurnSfx();
    }

    void OnTurnStart()
    {
        // Appelé au début du tour — on applique visuel et on tente de jouer le SFX
        // (si c’est le même joueur que celui qui vient d’être traité, le guard empêchera un doublon).
        ApplyThemeFromTurn();
        MaybePlayTurnSfx();
    }

    void ApplyThemeFromTurn()
    {
        int current = (TurnManager.Instance != null) ? TurnManager.Instance.CurrentPlayer : 0; // 0 rouge, 1 bleu
        bool isBlueTurn = (current == 1);
        if (invertColors) isBlueTurn = !isBlueTurn;

        // --- Mode 1 : un seul renderer + switch sprite ---
        if (targetRenderer != null && (redSprite != null || blueSprite != null))
        {
            targetRenderer.sprite = isBlueTurn ? blueSprite : redSprite;

            if (redRenderer)  redRenderer.enabled  = false;
            if (blueRenderer) blueRenderer.enabled = false;
            return;
        }

        // --- Mode 2 : deux renderers, on active/désactive ---
        if (redRenderer != null)  redRenderer.enabled  = !isBlueTurn;
        if (blueRenderer != null) blueRenderer.enabled =  isBlueTurn;
    }

    void MaybePlayTurnSfx()
    {
        if (TurnManager.Instance == null) return;
        if (!TurnManager.Instance.Started) return;
        if (!PlayersReady()) return;                 // attend 2 joueurs
        if (!sfxArmedAfterFirstChange) return;       // attend le 1er vrai changement de tour

        int current = TurnManager.Instance.CurrentPlayer; // 0 rouge / 1 bleu
        if (current == lastPlayerPlayed) return;          // pas de changement → rien

        // Choix du clip
        AudioClip clip = null;
        if (current == 0 && redTurnClip != null)        clip = redTurnClip;
        else if (current == 1 && blueTurnClip != null)  clip = blueTurnClip;
        else if (genericClip != null)                   clip = genericClip;
        else if (randomClips != null && randomClips.Length > 0)
                                                       clip = randomClips[Random.Range(0, randomClips.Length)];

        if (clip == null)
        {
            lastPlayerPlayed = current; // on avance quand même l’état pour ne pas spam
            return;
        }

        // Garantir un AudioSource
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        float originalPitch = sfxSource.pitch;
        if (randomPitch > 0f)
        {
            float delta = Random.Range(-randomPitch, randomPitch);
            sfxSource.pitch = Mathf.Clamp(1f + delta, 0.5f, 2f);
        }

        sfxSource.PlayOneShot(clip, sfxVolume);
        sfxSource.pitch = originalPitch;

        lastPlayerPlayed = current;
    }

    bool PlayersReady()
    {
        // En offline, on considère "ok".
        if (!PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode) return true;

        var room = PhotonNetwork.CurrentRoom;
        if (room == null) return false;

        return room.PlayerCount >= 2;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying) ApplyThemeFromTurn();
    }
#endif
}
