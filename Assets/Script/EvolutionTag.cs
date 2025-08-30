using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class EvolutionTag : MonoBehaviour
{
    [Tooltip("Identifiant unique de cette évolution (ex: OGOUN_RAVAGEUR, OGOUN_SENTINELLE, BARON_FLAMME_VIOLETTE, etc.). " +
             "Toutes les variantes/instances d’une même évolution doivent partager la même valeur.")]
    [FormerlySerializedAs("evoKey")]
    [FormerlySerializedAs("uniqueKey")]
    public string key;

    /// <summary>
    /// Retourne key si définie, sinon le nom du prefab/GO (fallback pour ne pas casser).
    /// </summary>
    public string KeyOrName => string.IsNullOrEmpty(key) ? gameObject.name : key;
}
