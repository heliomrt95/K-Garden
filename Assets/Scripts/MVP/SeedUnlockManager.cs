// SeedUnlockManager.cs
// -----------------------------------------------------------------------------
// Vérifie en boucle si le joueur a assez de ressources pour débloquer un
// nouveau niveau de graine. Quand c'est le cas :
//   - active le sachet correspondant dans la scène (s'il est référencé),
//     ou en crée un automatiquement à côté du sachet N1 (via SachetRuntimeBuilder)
//   - affiche un message via SimpleUIMessage
//
// Auto-init : s'instancie tout seul au démarrage de la scène si absent.
// Inutile d'avoir un GameObject "GameManager" pré-créé.
//
// Conditions :
//   - Seed Level 2 : 3 pollen + 2 sève
//   - Seed Level 3 : 2 essenceRare + 2 spores + 2 pollen
// -----------------------------------------------------------------------------

using UnityEngine;

public class SeedUnlockManager : MonoBehaviour
{
    [Header("Sachets à afficher quand débloqués (optionnels — auto-créés sinon)")]
    public GameObject sachetGrainesN2;
    public GameObject sachetGrainesN3;
    public GameObject sachetGrainesCarnivore;

    [Header("Position des sachets auto-créés (relative au sachet N1)")]
    public Vector3 offsetSachetN2 = new Vector3(0.3f, 0f, 0f);
    public Vector3 offsetSachetN3 = new Vector3(0.6f, 0f, 0f);
    public Vector3 offsetSachetCarnivore = new Vector3(0.9f, 0f, 0f);

    [Header("Coût en cristaux pour débloquer la graine carnivore")]
    public int coutCristauxCarnivore = 1;

    [Header("État (lecture seule, géré automatiquement)")]
    public bool seedLevel2Debloque = false;
    public bool seedLevel3Debloque = false;
    public bool seedCarnivoreDebloque = false;

    // ── Auto-instanciation au chargement de la scène ─────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        if (FindAnyObjectByType<SeedUnlockManager>() != null) return;

        GameObject gm = GameObject.Find("GameManager") ?? new GameObject("GameManager");
        if (gm.GetComponent<InventoryManager>() == null) gm.AddComponent<InventoryManager>();
        if (gm.GetComponent<SimpleUIMessage>() == null) gm.AddComponent<SimpleUIMessage>();
        if (gm.GetComponent<SeedUnlockManager>() == null) gm.AddComponent<SeedUnlockManager>();
        Debug.Log("[SeedUnlockManager] Auto-initialisé.");
    }

    void Start()
    {
        // Auto-détection des sachets par nom s'ils ne sont pas référencés ici
        if (sachetGrainesN2 == null) sachetGrainesN2 = GameObject.Find("SachetGraines_N2");
        if (sachetGrainesN3 == null) sachetGrainesN3 = GameObject.Find("SachetGraines_N3");
        if (sachetGrainesCarnivore == null) sachetGrainesCarnivore = GameObject.Find("SachetGraines_N4");

        // Cache tous les sachets non encore débloqués
        if (sachetGrainesN2 != null && !seedLevel2Debloque) sachetGrainesN2.SetActive(false);
        if (sachetGrainesN3 != null && !seedLevel3Debloque) sachetGrainesN3.SetActive(false);
        if (sachetGrainesCarnivore != null && !seedCarnivoreDebloque) sachetGrainesCarnivore.SetActive(false);
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
            ActiverOuCreerSachet(2);
            SimpleUIMessage.Afficher("Nouvelle graine disponible !");
            Debug.Log("[Unlock] Seed Level 2 débloqué.");
        }

        // ── Déblocage Seed Level 3 ───────────────────────────────────────────
        if (!seedLevel3Debloque &&
            InventoryManager.Instance.HasEnough("essenceRare", 2) &&
            InventoryManager.Instance.HasEnough("spores", 2) &&
            InventoryManager.Instance.HasEnough("pollen", 2))
        {
            seedLevel3Debloque = true;
            ActiverOuCreerSachet(3);
            SimpleUIMessage.Afficher("Nouvelle graine disponible !");
            Debug.Log("[Unlock] Seed Level 3 débloqué.");
        }

        // ── Déblocage Graine Carnivore (N4) — coût en cristaux ───────────────
        if (!seedCarnivoreDebloque &&
            InventoryManager.Instance.HasEnough("cristalVegetal", coutCristauxCarnivore))
        {
            seedCarnivoreDebloque = true;
            ActiverOuCreerSachet(4);
            SimpleUIMessage.Afficher("Graine carnivore disponible !");
            Debug.Log("[Unlock] Graine carnivore débloquée.");
        }
    }

    // Active le sachet déjà référencé OU en crée un nouveau à côté du sachet N1.
    void ActiverOuCreerSachet(int niveau)
    {
        // Récupère la référence selon le niveau
        GameObject sachet = ObtenirReferenceSachet(niveau);
        if (sachet != null) { sachet.SetActive(true); return; }

        // Cherche un sachet existant dans la scène (créé via Tools mais pas référencé)
        sachet = GameObject.Find("SachetGraines_N" + niveau);
        if (sachet != null)
        {
            sachet.SetActive(true);
            DefinirReferenceSachet(niveau, sachet);
            return;
        }

        // Auto-création à côté du sachet N1
        GameObject sachetN1 = GameObject.Find("SachetGraines_N1") ?? GameObject.Find("SachetGraines");
        Vector3 basePos = sachetN1 != null ? sachetN1.transform.position : Vector3.zero;
        Vector3 offset = ObtenirOffsetSachet(niveau);

        GameObject nouveau = SachetRuntimeBuilder.CreerSachet(niveau, basePos + offset);
        DefinirReferenceSachet(niveau, nouveau);
        Debug.Log("[Unlock] Sachet N" + niveau + " créé automatiquement à " + (basePos + offset));
    }

    GameObject ObtenirReferenceSachet(int niveau)
    {
        switch (niveau)
        {
            case 2: return sachetGrainesN2;
            case 3: return sachetGrainesN3;
            case 4: return sachetGrainesCarnivore;
            default: return null;
        }
    }

    void DefinirReferenceSachet(int niveau, GameObject go)
    {
        switch (niveau)
        {
            case 2: sachetGrainesN2 = go; break;
            case 3: sachetGrainesN3 = go; break;
            case 4: sachetGrainesCarnivore = go; break;
        }
    }

    Vector3 ObtenirOffsetSachet(int niveau)
    {
        switch (niveau)
        {
            case 2: return offsetSachetN2;
            case 3: return offsetSachetN3;
            case 4: return offsetSachetCarnivore;
            default: return Vector3.zero;
        }
    }
}
