// SeedUnlockManager.cs
// -----------------------------------------------------------------------------
// Vérifie en boucle si le joueur a assez de ressources pour débloquer un
// nouveau niveau de graine. Quand c'est le cas :
//   - active le sachet correspondant dans la scène (caché au départ)
//   - affiche un message via SimpleUIMessage
//
// Conditions :
//   - Seed Level 2 : 3 pollen + 2 sève
//   - Seed Level 3 : 2 essenceRare + 2 spores + 2 pollen
//
// À mettre sur le GameObject "GameManager".
// -----------------------------------------------------------------------------

using UnityEngine;

public class SeedUnlockManager : MonoBehaviour
{
    [Header("Sachets à afficher quand débloqués (à glisser depuis la Hierarchy)")]
    public GameObject sachetGrainesN2;
    public GameObject sachetGrainesN3;

    [Header("État (lecture seule, géré automatiquement)")]
    public bool seedLevel2Debloque = false;
    public bool seedLevel3Debloque = false;

    void Start()
    {
        // Au démarrage, on cache les sachets N2 et N3 — ils apparaîtront
        // automatiquement quand débloqués.
        if (sachetGrainesN2 != null) sachetGrainesN2.SetActive(false);
        if (sachetGrainesN3 != null) sachetGrainesN3.SetActive(false);
    }

    void Update()
    {
        if (InventoryManager.Instance == null) return;

        // ── Déblocage Seed Level 2 ───────────────────────────────────────────
        if (!seedLevel2Debloque &&
            InventoryManager.Instance.HasEnough("pollen", 3) &&
            InventoryManager.Instance.HasEnough("seve", 2))
        {
            seedLevel2Debloque = true;
            if (sachetGrainesN2 != null) sachetGrainesN2.SetActive(true);
            SimpleUIMessage.Afficher("Nouvelle graine débloquée ! (Niveau 2)");
            Debug.Log("[Unlock] Seed Level 2 débloqué.");
        }

        // ── Déblocage Seed Level 3 ───────────────────────────────────────────
        if (!seedLevel3Debloque &&
            InventoryManager.Instance.HasEnough("essenceRare", 2) &&
            InventoryManager.Instance.HasEnough("spores", 2) &&
            InventoryManager.Instance.HasEnough("pollen", 2))
        {
            seedLevel3Debloque = true;
            if (sachetGrainesN3 != null) sachetGrainesN3.SetActive(true);
            SimpleUIMessage.Afficher("Nouvelle graine débloquée ! (Niveau 3)");
            Debug.Log("[Unlock] Seed Level 3 débloqué.");
        }
    }
}
