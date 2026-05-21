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

    [Header("Position des sachets auto-créés (relative au sachet N1)")]
    public Vector3 offsetSachetN2 = new Vector3(0.3f, 0f, 0f);
    public Vector3 offsetSachetN3 = new Vector3(0.6f, 0f, 0f);

    [Header("État (lecture seule, géré automatiquement)")]
    public bool seedLevel2Debloque = false;
    public bool seedLevel3Debloque = false;

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
        // Cache les sachets pré-référencés (ils apparaîtront au déblocage)
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
    }

    // Active le sachet déjà référencé OU en crée un nouveau à côté du sachet N1.
    void ActiverOuCreerSachet(int niveau)
    {
        GameObject sachet = (niveau == 2) ? sachetGrainesN2 : sachetGrainesN3;
        if (sachet != null) { sachet.SetActive(true); return; }

        // Cherche un sachet existant déjà dans la scène (au cas où il aurait
        // été créé via le menu Tools mais pas glissé dans l'Inspector)
        sachet = GameObject.Find("SachetGraines_N" + niveau);
        if (sachet != null)
        {
            sachet.SetActive(true);
            if (niveau == 2) sachetGrainesN2 = sachet;
            else sachetGrainesN3 = sachet;
            return;
        }

        // Sinon, on en crée un automatiquement à côté du sachet N1
        GameObject sachetN1 = GameObject.Find("SachetGraines_N1") ?? GameObject.Find("SachetGraines");
        Vector3 basePos = sachetN1 != null ? sachetN1.transform.position : Vector3.zero;
        Vector3 offset = (niveau == 2) ? offsetSachetN2 : offsetSachetN3;

        GameObject nouveau = SachetRuntimeBuilder.CreerSachet(niveau, basePos + offset);
        if (niveau == 2) sachetGrainesN2 = nouveau;
        else sachetGrainesN3 = nouveau;

        Debug.Log("[Unlock] Sachet N" + niveau + " créé automatiquement à " + (basePos + offset));
    }
}
