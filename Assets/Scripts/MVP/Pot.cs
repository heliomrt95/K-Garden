// Pot.cs
// -----------------------------------------------------------------------------
// Cycle de vie d'un pot dans K-Garden :
//
//   Vide → Terre → Graine plantée → Arrosé → Croissance → Adulte
//
// Une fois arrosée, la graine grandit progressivement (scale interpolée).
// Pendant la croissance, des nuisibles (Nuisible.cs) peuvent apparaître et
// infliger des dégâts à la plante (champ vie). Le spray supprime les nuisibles
// (Player.cs spam-clic → Pot.TuerNuisible). Si vie ≤ 0, la plante meurt.
//
// Composants Unity requis :
//   - un Collider (BoxCollider) sur le parent
//   - 3 enfants optionnels : visuelTerre, visuelGraine, visuelEau
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

public class Pot : MonoBehaviour
{
    [Header("Visuels (enfants)")]
    public GameObject visuelTerre;
    public GameObject visuelGraine;
    public GameObject visuelEau;

    [Header("Durées clic maintenu (s)")]
    public float dureeRemplirTerre = 1.2f;
    public float dureePlanterGraine = 1.5f;
    public float dureeArroser = 1.0f;

    [Header("Croissance")]
    public float dureeCroissance = 30f;   // secondes pour atteindre l'adulte
    public float facteurAdulte = 3.5f;    // taille adulte = scale initiale * ce facteur

    [Header("Vie & nuisibles")]
    public float vieMax = 100f;
    public float intervalleSpawnNuisible = 8f;   // s entre 2 chances de spawn
    public float chanceSpawn = 0.55f;             // proba par tick
    public int nuisiblesMax = 3;
    public float delaiAvantPremierSpawn = 12f;    // s après arrosage

    // ── État interne ─────────────────────────────────────────────────────────
    public float vie { get; private set; }
    public bool aTerre { get; private set; }
    public bool aGraine { get; private set; }
    public bool aEau { get; private set; }
    public bool morte { get; private set; }

    private Vector3 scaleGraineInit;     // scale de la graine au moment de planter
    private float tempsDepuisArrosage;
    private float tempsDepuisDernierSpawn;
    private readonly List<Nuisible> nuisibles = new List<Nuisible>();

    void Start()
    {
        vie = vieMax;
        if (visuelTerre  != null) visuelTerre.SetActive(false);
        if (visuelGraine != null) visuelGraine.SetActive(false);
        if (visuelEau    != null) visuelEau.SetActive(false);
    }

    void Update()
    {
        if (!aEau || morte) return;

        // 1) Croissance de la plante (échelle de la graine grandit)
        tempsDepuisArrosage += Time.deltaTime;
        if (visuelGraine != null && visuelGraine.activeSelf)
        {
            float r = Mathf.Clamp01(tempsDepuisArrosage / dureeCroissance);
            float factor = Mathf.Lerp(1f, facteurAdulte, r);
            visuelGraine.transform.localScale = scaleGraineInit * factor;
        }

        // 2) Spawn aléatoire de nuisibles
        if (tempsDepuisArrosage > delaiAvantPremierSpawn)
        {
            tempsDepuisDernierSpawn += Time.deltaTime;
            if (tempsDepuisDernierSpawn >= intervalleSpawnNuisible &&
                nuisibles.Count < nuisiblesMax)
            {
                tempsDepuisDernierSpawn = 0f;
                if (Random.value < chanceSpawn) SpawnNuisible();
            }
        }

        // 3) Mort si vie ≤ 0
        if (vie <= 0f) Mourir();
    }

    // ── Actions du joueur (appelées par Player.cs) ───────────────────────────
    public float DureeAction(string item)
    {
        if (morte) return 0f;
        if (item == "terre" && !aTerre)             return dureeRemplirTerre;
        if (item == "graine" && aTerre && !aGraine) return dureePlanterGraine;
        if (item == "eau" && aGraine && !aEau)      return dureeArroser;
        return 0f;
    }

    public string RaisonRefus(string item)
    {
        if (morte)             return "Cette plante est morte.";
        if (item == "")        return "Mains vides. Prends terre, graine ou arrosoir.";
        if (item == "terre")   return aTerre  ? "Le pot a déjà de la terre." : "";
        if (item == "graine")  return !aTerre ? "Mets d'abord de la terre dans le pot." :
                                      aGraine ? "Une graine est déjà plantée." : "";
        if (item == "eau")     return !aTerre  ? "Mets d'abord de la terre." :
                                      !aGraine ? "Plante d'abord une graine." :
                                       aEau    ? "Le pot est déjà arrosé." : "";
        if (item == "spray")   return NuisiblesActifs() == 0
                                      ? "Aucun nuisible à éliminer ici."
                                      : ""; // spray géré par spam-clic, pas DureeAction
        return "Cet item (" + item + ") ne va pas dans le pot.";
    }

    public void ValiderAction(string item)
    {
        if (morte) return;

        if (item == "terre" && !aTerre)
        {
            aTerre = true;
            if (visuelTerre != null) visuelTerre.SetActive(true);
            GameState.LibererMain();
            Debug.Log(name + " : rempli de terre.");
        }
        else if (item == "graine" && aTerre && !aGraine)
        {
            aGraine = true;
            if (visuelGraine != null)
            {
                visuelGraine.SetActive(true);
                scaleGraineInit = visuelGraine.transform.localScale;
            }
            GameState.LibererMain();
            Debug.Log(name + " : graine plantée.");
        }
        else if (item == "eau" && aGraine && !aEau)
        {
            aEau = true;
            tempsDepuisArrosage = 0f;
            if (visuelEau != null) visuelEau.SetActive(true);
            GameState.LibererMain();
            Debug.Log(name + " : arrosé — la plante commence à pousser.");
        }
    }

    // ── Vie & nuisibles ──────────────────────────────────────────────────────
    public void RecevoirDegats(float montant)
    {
        if (morte) return;
        vie = Mathf.Max(0f, vie - montant);
    }

    public int NuisiblesActifs()
    {
        nuisibles.RemoveAll(n => n == null);
        return nuisibles.Count;
    }

    // Appelée par Player.cs quand la barre du spam-clic atteint 100 %
    public void TuerUnNuisible()
    {
        nuisibles.RemoveAll(n => n == null);
        if (nuisibles.Count == 0) return;
        Nuisible cible = nuisibles[0];
        nuisibles.RemoveAt(0);
        if (cible != null) Destroy(cible.gameObject);
        Debug.Log(name + " : 1 nuisible éliminé. Restants : " + nuisibles.Count);
    }

    void SpawnNuisible()
    {
        // Petit cube rouge avec lueur d'émission, sans collider
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Nuisible";
        go.transform.localScale = Vector3.one * 0.08f;
        Object.Destroy(go.GetComponent<Collider>());

        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.85f, 0.2f, 0.15f);
        mat.SetColor("_EmissionColor", new Color(0.4f, 0.05f, 0.05f));
        mat.EnableKeyword("_EMISSION");
        go.GetComponent<Renderer>().sharedMaterial = mat;

        Nuisible n = go.AddComponent<Nuisible>();
        n.potCible = this;
        nuisibles.Add(n);
        Debug.Log(name + " : nuisible apparu (total " + nuisibles.Count + ").");
    }

    void Mourir()
    {
        morte = true;
        // Marron foncé pour signaler la mort
        if (visuelGraine != null)
        {
            Renderer r = visuelGraine.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.30f, 0.20f, 0.10f);
        }
        // Détruit tous les nuisibles restants
        foreach (var n in nuisibles) if (n != null) Destroy(n.gameObject);
        nuisibles.Clear();
        Debug.Log(name + " : la plante est morte.");
    }
}
