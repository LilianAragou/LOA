using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class EvolutionSystem : MonoBehaviourPunCallbacks
{
    public static EvolutionSystem Instance { get; private set; }

    // ===== Interface optionnelle pour brancher une monnaie externe =====
    public interface IEvolutionCurrencyProvider
    {
        // team: 0=Rouge (Ogoun), 1=Bleu (Baron)
        int  GetCurrencyForTeam(int team);
        bool TrySpendForTeam(int team, int amount);
    }

    [Header("Panels")]
    [SerializeField] GameObject panelOgoun; // équipe ROUGE
    [SerializeField] GameObject panelBaron; // équipe BLEUE

    [Header("Ogoun (rouge)")]
    [SerializeField] Button[] ogounButtons;
    [SerializeField] string[] ogounPrefabKeys = {
        "Lame_Ardente", "Manieur_De_Lame", "Ravageur", "Sentinelle_Ecarlate", "Cavalier_Fulgurant"
    };

    [Header("Baron (bleu)")]
    [SerializeField] Button[] baronButtons;
    [SerializeField] string[] baronPrefabKeys = {
        // Ex: "Revenant","Egaree_P","Devoreur_Ame","Hurleur_Creux","Flamme_Violette"
    };

    [Header("Monnaie / intégration")]
    [Tooltip("OPTIONNEL : un MonoBehaviour qui implémente IEvolutionCurrencyProvider. Si vide, on utilise automatiquement les Shadow Points du Baron.")]
    [SerializeField] MonoBehaviour currencyProviderBehaviour; // implémente IEvolutionCurrencyProvider
    IEvolutionCurrencyProvider currencyProvider;              // choisi au Awake()
    [SerializeField] int baronCurrencyLocal = 0;              // fallback si vraiment rien

    // runtime
    Piece current;
    HashSet<string> ogounKeySet = new HashSet<string>();
    HashSet<string> baronKeySet = new HashSet<string>();

    // Cache: prefabKey -> TypeName pour comparer correctement
    readonly Dictionary<string, string> _prefabKeyToTypeName = new Dictionary<string, string>();

    void Awake()
    {
        Instance = this;

        // Provider = externe si dispo, sinon auto-bridge Shadow Points
        currencyProvider = currencyProviderBehaviour as IEvolutionCurrencyProvider;
        if (currencyProvider == null)
            currencyProvider = new ShadowPointsProvider();

        RebuildKeySets();
        HideAllImmediate();
        WireButtons();
    }

    // === Provider interne basé sur BaronSamediMaskPiece ===
    class ShadowPointsProvider : IEvolutionCurrencyProvider
    {
        BaronSamediMaskPiece FindBaronMaskBlue()
        {
            var masks = Object.FindObjectsByType<BaronSamediMaskPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var m in masks)
                if (m != null && m.isRed == false)
                    return m;
            return null;
        }

        public int GetCurrencyForTeam(int team)
        {
            if (team != 1) return 0; // on ne gère que le Baron (bleu)
            var mask = FindBaronMaskBlue();
            return mask != null ? mask.GetShadowPoints() : 0;
        }

        public bool TrySpendForTeam(int team, int amount)
        {
            if (amount <= 0) return true;
            if (team != 1) return false;

            var mask = FindBaronMaskBlue();
            if (mask == null) return false;

            int cur = mask.GetShadowPoints();
            if (cur < amount) return false;

            mask.SpendShadowPoints(amount);
            return true;
        }
    }

    void RebuildKeySets()
    {
        ogounKeySet.Clear();
        if (ogounPrefabKeys != null)
            foreach (var k in ogounPrefabKeys) if (!string.IsNullOrEmpty(k)) ogounKeySet.Add(k);

        baronKeySet.Clear();
        if (baronPrefabKeys != null)
            foreach (var k in baronPrefabKeys) if (!string.IsNullOrEmpty(k)) baronKeySet.Add(k);
    }

    void WireButtons()
    {
        if (ogounButtons != null && ogounPrefabKeys != null)
        {
            int n = Mathf.Min(ogounButtons.Length, ogounPrefabKeys.Length);
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                ogounButtons[i].onClick.RemoveAllListeners();
                ogounButtons[i].onClick.AddListener(() => OnClickEvolve(ogounPrefabKeys[idx]));
            }
        }

        if (baronButtons != null && baronPrefabKeys != null)
        {
            int n = Mathf.Min(baronButtons.Length, baronPrefabKeys.Length);
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                baronButtons[i].onClick.RemoveAllListeners();
                baronButtons[i].onClick.AddListener(() => OnClickEvolve(baronPrefabKeys[idx]));
            }
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        RefreshUI();
    }
    public override void OnDisable()
    {
        base.OnDisable();
    }

    // ==== API InputManager / externes ====
    public void ShowMenu(Piece p) => ShowForPiece(p);
    public void HideMenu() => HideAll();
    public void ForceRefresh() => RefreshButtons(); // pratique à appeler depuis RitualSystem quand le lock change

    // ==== Fallback local ====
    public void SetBaronCurrencyLocal(int value)
    {
        baronCurrencyLocal = Mathf.Max(0, value);
        RefreshButtons();
    }
    public void AddBaronCurrencyLocal(int delta) => SetBaronCurrencyLocal(baronCurrencyLocal + delta);

    // ==== Affichage ====
    public void ShowForPiece(Piece p)
    {
        current = p;

        if (current == null || !IsMyTurn() || !IsMyPiece(current))
        {
            HideAllImmediate();
            return;
        }

        bool isOgounSide = current.isRed; // ROUGE = Ogoun
        if (panelOgoun) panelOgoun.SetActive(isOgounSide);
        if (panelBaron) panelBaron.SetActive(!isOgounSide);

        RefreshButtons();
    }

    public void HideAll()
    {
        if (panelOgoun) panelOgoun.SetActive(false);
        if (panelBaron) panelBaron.SetActive(false);
        current = null;
    }
    void HideAllImmediate()
    {
        if (panelOgoun) panelOgoun.SetActive(false);
        if (panelBaron) panelBaron.SetActive(false);
    }

    void RefreshUI()
    {
        if (current == null) return;
        RefreshButtons();
    }

    void RefreshButtons()
    {
        if (current == null) return;

        // IMPORTANT : on teste le lock sur l’ÉQUIPE DE LA PIÈCE COURANTE,
        // pas seulement sur MyTeam (ça évite toute désynchro d’affichage)
        int teamOfCurrent = current.isRed ? 0 : 1;
        bool teamLocked = IsTeamLockedForTeam(teamOfCurrent);

        // NOUVEAU : pièce déjà évoluée ? → on bloque tout net les évolutions
        bool alreadyEvolved = current.GetComponent<EvolutionTag>() != null;
        if (alreadyEvolved)
            Debug.Log($"[EVO][UI] Boutons désactivés: la pièce '{current.name}' @ {current.currentGridPos} possède déjà EvolutionTag.");

        bool isOgounSide = current.isRed;

        if (isOgounSide)
        {
            if (ogounButtons != null && ogounPrefabKeys != null)
            {
                int n = Mathf.Min(ogounButtons.Length, ogounPrefabKeys.Length);
                for (int i = 0; i < n; i++)
                    ogounButtons[i].interactable = !teamLocked && !alreadyEvolved && CanEvolve(current, ogounPrefabKeys[i]);
            }
        }
        else
        {
            if (baronButtons != null && baronPrefabKeys != null)
            {
                int n = Mathf.Min(baronButtons.Length, baronPrefabKeys.Length);
                for (int i = 0; i < n; i++)
                {
                    string key = baronPrefabKeys[i];
                    bool interact = !teamLocked && !alreadyEvolved && CanEvolve(current, key);
                    baronButtons[i].interactable = interact;
                }
            }
        }
    }

    void OnClickEvolve(string prefabKey)
    {
        if (current == null) return;

        // re-sécurité : lock basé sur l’équipe de la pièce
        int teamOfCurrent = current.isRed ? 0 : 1;
        if (IsTeamLockedForTeam(teamOfCurrent))
        {
            Debug.Log("[EVO] Click ignoré: équipe verrouillée.");
            RefreshButtons();
            return;
        }

        // re-sécurité : pièce déjà évoluée ?
        if (current.GetComponent<EvolutionTag>() != null)
        {
            Debug.Log("[EVO] Click refusé: la pièce possède déjà EvolutionTag (ré-évolution interdite).");
            RefreshButtons();
            return;
        }

        if (!CanEvolve(current, prefabKey))
        {
            Debug.Log($"[EVO] Click refusé: CanEvolve=false pour key '{prefabKey}'.");
            RefreshButtons();
            return;
        }

        // Dépense pour BARON (bleu) avant la requête réseau
        if (!current.isRed)
        {
            int cost = GetCostFromEvolutionCost(prefabKey);
            if (cost > 0 && !TrySpendBaron(cost))
            {
                Debug.Log($"[EVO] Click refusé: fonds insuffisants (coût={cost}).");
                RefreshButtons();
                return;
            }
        }

        Debug.Log($"[EVO] Requête d'évolution envoyée pour '{current.name}' -> '{prefabKey}'.");
        current.RequestEvolve(prefabKey);
        HideAll(); // BoardManager finira le tour côté Master
    }

    // ==== Conditions ====
    bool IsMyTurn()
    {
        return TurnManager.Instance != null && TurnManager.Instance.Started && TurnManager.Instance.IsMyTurn;
    }
    bool IsMyPiece(Piece p)
    {
        int myTeam = TurnManager.Instance != null ? TurnManager.Instance.MyTeam : -1; // 0=rouge, 1=bleu
        if (myTeam < 0) return false;
        return (myTeam == 0 && p.isRed) || (myTeam == 1 && !p.isRed);
    }

    bool AdjacentToMyMask(Piece p)
    {
        if (p == null) return false;

        if (p.isRed) // Ogoun -> Ogoun_Mask
        {
            var masks = Object.FindObjectsByType<Ogoun_Mask>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var m in masks)
                if (m != null && m.isRed == p.isRed && IsAdjacent(p.currentGridPos, m.currentGridPos))
                    return true;
            return false;
        }
        else // Baron -> BaronSamediMaskPiece
        {
            var masks = Object.FindObjectsByType<BaronSamediMaskPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var m in masks)
                if (m != null && m.isRed == p.isRed && IsAdjacent(p.currentGridPos, m.currentGridPos))
                    return true;
            return false;
        }
    }

    bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx <= 1 && dy <= 1 && (dx + dy) > 0;
    }

    bool CanEvolve(Piece p, string prefabKey)
    {
        if (p == null) return false;
        if (string.IsNullOrEmpty(prefabKey)) return false;

        // Doit être mon tour et ma pièce
        if (!IsMyTurn() || !IsMyPiece(p)) { Debug.Log("[EVO] CanEvolve: pas mon tour ou pas ma pièce."); return false; }

        // Lock : sur l’équipe de la PIÈCE candidate (sécurité)
        int teamOfPiece = p.isRed ? 0 : 1;
        if (IsTeamLockedForTeam(teamOfPiece)) { Debug.Log("[EVO] CanEvolve: équipe verrouillée."); return false; }

        // Adjacence au masque (commun aux deux camps)
        if (!AdjacentToMyMask(p)) { Debug.Log("[EVO] CanEvolve: pas adjacent au masque."); return false; }

        // ⛔ NOUVEAU : si la pièce possède déjà EvolutionTag → interdiction
        if (p.GetComponent<EvolutionTag>() != null)
        {
            Debug.Log("[EVO] CanEvolve: EvolutionTag détecté sur la pièce -> ré-évolution interdite.");
            return false;
        }

        if (p.isRed)
        {
            // ==== OGOUN (ROUGE) ====
            // - pas de doublon d'un même type déjà sur le board côté rouge
            if (TeamHasEvolutionKey(true, prefabKey)) { Debug.Log("[EVO] CanEvolve: doublon Ogoun interdit."); return false; }

            // - max 2 évolutions simultanées
            int activeCount = CountTeamEvolutions(true, ogounKeySet);
            if (activeCount >= 2) { Debug.Log("[EVO] CanEvolve: cap d'unités évoluées Ogoun atteint (2)."); return false; }

            return true;
        }
        else
        {
            // ==== BARON (BLEU) ====
            // - pas plus d’une unité du même type en même temps (blocage des doublons)
            if (TeamHasEvolutionKey(false, prefabKey)) { Debug.Log("[EVO] CanEvolve: doublon Baron interdit."); return false; }

            // - respecte le coût (Shadow Points via provider)
            int cost   = GetCostFromEvolutionCost(prefabKey);
            int wallet = GetBaronCurrency();
            if (wallet < cost) { Debug.Log($"[EVO] CanEvolve: fonds insuffisants (wallet={wallet}, cost={cost})."); return false; }

            return true;
        }
    }

    // ---- Helpers: mapping prefabKey -> nom de type C# (avec cache) ----
    string TypeNameFromPrefabKey(string prefabKey)
    {
        if (string.IsNullOrEmpty(prefabKey)) return string.Empty;

        if (_prefabKeyToTypeName.TryGetValue(prefabKey, out var cached))
            return cached;

        var go = Resources.Load<GameObject>("PhotonPrefabs/" + prefabKey);
        string typeName = string.Empty;

        if (go != null)
        {
            var piece = go.GetComponent<Piece>();
            if (piece != null)
                typeName = piece.GetType().Name;
        }

        // Fallback: si on ne trouve pas, on essaie la key telle quelle
        if (string.IsNullOrEmpty(typeName)) typeName = prefabKey;

        _prefabKeyToTypeName[prefabKey] = typeName;
        return typeName;
    }

    // ---- Comptage & doublons (Ogoun: total sur whitelist) ----
    int CountTeamEvolutions(bool teamIsRed, HashSet<string> allowedKeys)
    {
        var allowedTypeNames = new HashSet<string>();
        foreach (var key in allowedKeys)
        {
            var tn = TypeNameFromPrefabKey(key);
            if (!string.IsNullOrEmpty(tn))
                allowedTypeNames.Add(tn);
        }

        int count = 0;
        var all = Object.FindObjectsByType<Piece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var pc in all)
        {
            if (pc == null || pc.isRed != teamIsRed) continue;
            string typeName = pc.GetType().Name;
            if (allowedTypeNames.Contains(typeName))
                count++;
        }
        return count;
    }

    bool TeamHasEvolutionKey(bool teamIsRed, string prefabKey)
    {
        string targetType = TypeNameFromPrefabKey(prefabKey);
        if (string.IsNullOrEmpty(targetType)) return false;

        var all = Object.FindObjectsByType<Piece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var pc in all)
        {
            if (pc == null || pc.isRed != teamIsRed) continue;
            if (pc.GetType().Name == targetType)
                return true;
        }
        return false;
    }

    // ---- Coûts via EvolutionCost sur les prefabs ----
    int GetCostFromEvolutionCost(string prefabKey)
    {
        if (string.IsNullOrEmpty(prefabKey)) return 0;
        var go = Resources.Load<GameObject>("PhotonPrefabs/" + prefabKey);
        if (go == null) return 0;

        var ec = go.GetComponent<EvolutionCost>();
        if (ec == null) return 0;
        return Mathf.Max(0, ec.cost);
    }

    // ---- Currency Baron (provider externe, Shadow Points ou fallback local) ----
    int GetBaronCurrency()
    {
        if (currencyProvider != null) return currencyProvider.GetCurrencyForTeam(1);
        return baronCurrencyLocal;
    }

    bool TrySpendBaron(int amount)
    {
        if (amount <= 0) return true;

        if (currencyProvider != null)
            return currencyProvider.TrySpendForTeam(1, amount);

        if (baronCurrencyLocal >= amount)
        {
            baronCurrencyLocal -= amount;
            return true;
        }
        return false;
    }

    // ===== Vérif simple du verrou via RitualSystem =====
    bool IsTeamLockedForTeam(int team)
    {
        var rs = RitualSystem.Instance;
        if (rs == null) return false;
        return rs.IsTeamLocked(team);
    }
}
