using UnityEngine;

[CreateAssetMenu(menuName = "Loa/Loa Definition", fileName = "Loa_XXX")]
public class LoaDefinition : ScriptableObject
{
    [Header("Identity")]
    public string loaId;              // "BARON_SAMEDI", "OGOUN", etc. (unique)
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Prefabs & Setup")]
    public GameObject maskPrefab;
    public GameObject spiritPrefab;   // prefab d’un Esprit de base pour ce Loa (ou liste si besoin)
    public int startingSpiritCount = 6;

    [Header("Rules & Assets (optionnels)")]
    public AudioClip sfxSelect;
    public Color themeColor = Color.white;

    // Ajoute ici des références spécifiques (rituels, evols, etc.) si tu en as déjà en code
}
