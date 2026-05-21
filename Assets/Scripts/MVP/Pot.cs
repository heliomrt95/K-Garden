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
    public float delaiAvantAttaque = 12f;   // s après arrosage avant le 1er nuisible
    public int taillesAttaque = 3;          // nombre TOTAL de nuisibles dans le cycle
    public float delaiEntreSpawns = 5f;     // s entre chaque apparition (vague progressive)

    // ── État interne ─────────────────────────────────────────────────────────
    public float vie { get; private set; }
    public bool aTerre { get; private set; }
    public bool aGraine { get; private set; }
    public bool aEau { get; private set; }
    public bool morte { get; private set; }
    public int niveauPlante { get; private set; } = 1; // 1, 2 ou 3 selon la graine plantée
    public bool recompenseDonnee { get; private set; } // évite de donner les ressources 2x

    private Vector3 scaleGraineInit;     // scale de la graine au moment de planter
    private float tempsDepuisArrosage;
    private bool attaqueDeclenchee = false; // true dès le 1er spawn de cette plante
    private bool attaqueTerminee = false;   // true quand tous spawnés ET tous morts
    private int nuisiblesSpawnes = 0;       // total cumulé (jamais ne décroît)
    private float tempsDepuisDernierSpawn = 0f;
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

        // 1b) À maturité ET vivante → récompense (une seule fois)
        if (!recompenseDonnee && tempsDepuisArrosage >= dureeCroissance && !morte)
        {
            recompenseDonnee = true;
            Debug.Log("✅ " + name + " mature (N" + niveauPlante + ") → récompense !");
            PlantReward.DonnerRecompenses(niveauPlante);
        }

        // 2) Spawn PROGRESSIF : un nuisible à la fois, espacés dans le temps.
        //    Quota total = taillesAttaque. Une fois atteint, plus jamais de spawn.
        if (!attaqueTerminee &&
            nuisiblesSpawnes < taillesAttaque &&
            tempsDepuisArrosage >= delaiAvantAttaque)
        {
            tempsDepuisDernierSpawn += Time.deltaTime;
            // Premier spawn dès qu'on atteint le délai d'attaque ; suivants espacés
            bool peutSpawner = (nuisiblesSpawnes == 0) || (tempsDepuisDernierSpawn >= delaiEntreSpawns);
            if (peutSpawner)
            {
                SpawnNuisible();
                nuisiblesSpawnes++;
                tempsDepuisDernierSpawn = 0f;
                if (!attaqueDeclenchee)
                {
                    attaqueDeclenchee = true;
                    Debug.Log(name + " : attaque commencée (" + taillesAttaque + " nuisibles arriveront en vague).");
                }
            }
        }

        // 3) Fin de l'attaque : quota épuisé ET tous morts → plus jamais rien
        if (attaqueDeclenchee && !attaqueTerminee &&
            nuisiblesSpawnes >= taillesAttaque && NuisiblesActifs() == 0)
        {
            attaqueTerminee = true;
            Debug.Log(name + " : tous les nuisibles éliminés, la plante reprend sa croissance.");
        }

        // 3) Mort si vie ≤ 0
        if (vie <= 0f) Mourir();
    }

    // ── Actions du joueur (appelées par Player.cs) ───────────────────────────
    public float DureeAction(string item)
    {
        if (morte) return 0f;
        if (item == "terre" && !aTerre)                        return dureeRemplirTerre;
        if (EstUneGraine(item) && aTerre && !aGraine)          return dureePlanterGraine;
        if (item == "eau" && aGraine && !aEau)                 return dureeArroser;
        return 0f;
    }

    // Vrai pour "graine", "graine_1", "graine_2", "graine_3"
    static bool EstUneGraine(string item)
    {
        return item == "graine" || (item != null && item.StartsWith("graine_"));
    }

    // Extrait le niveau d'une graine ("graine_2" → 2). Défaut = 1.
    static int NiveauDeGraine(string item)
    {
        if (item != null && item.StartsWith("graine_"))
        {
            int n;
            if (int.TryParse(item.Substring("graine_".Length), out n)) return n;
        }
        return 1;
    }

    public string RaisonRefus(string item)
    {
        if (morte)             return "Cette plante est morte.";
        if (item == "")        return "Mains vides. Prends terre, graine ou arrosoir.";
        if (item == "terre")   return aTerre  ? "Le pot a déjà de la terre." : "";
        if (EstUneGraine(item))return !aTerre ? "Mets d'abord de la terre dans le pot." :
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
        else if (EstUneGraine(item) && aTerre && !aGraine)
        {
            aGraine = true;
            niveauPlante = NiveauDeGraine(item);
            if (visuelGraine != null)
            {
                visuelGraine.SetActive(true);
                scaleGraineInit = visuelGraine.transform.localScale;
            }
            GameState.LibererMain();
            Debug.Log(name + " : graine N" + niveauPlante + " plantée.");
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
        // Petite sphère noire, sans collider — effet "insecte volant"
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Nuisible";
        go.transform.localScale = Vector3.one * 0.035f; // ~3.5 cm
        Object.Destroy(go.GetComponent<Collider>());

        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.04f, 0.04f, 0.04f);
        mat.SetFloat("_Glossiness", 0.15f);
        go.GetComponent<Renderer>().sharedMaterial = mat;

        Nuisible n = go.AddComponent<Nuisible>();
        n.potCible = this;
        // Variations par insecte pour un effet "nuée" (chacun son orbite)
        n.rayon            += Random.Range(-0.08f, 0.10f);
        n.vitesseAngulaire += Random.Range(-40f, 50f);
        n.amplitudeY        = Random.Range(0.04f, 0.12f);
        n.vitesseY          = Random.Range(2f, 5f);

        // Hauteur : moitié des nuisibles au sol (rampant), moitié en l'air (volant)
        bool volant = Random.value < 0.5f;
        n.hauteurCentre = volant
            ? Random.Range(0.35f, 0.60f)   // insectes volants autour de la plante
            : Random.Range(0.00f, 0.10f);  // insectes au sol près du pot

        nuisibles.Add(n);
        Debug.Log(name + " : nuisible " + (volant ? "volant" : "au sol") +
                  " apparu (" + (nuisiblesSpawnes + 1) + "/" + taillesAttaque + ").");
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
