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
    public float dureeCreuser = 1.5f;  // pelle : enlève la plante mature ou morte

    [Header("Exigences (bac normal = 1, bac carnivore = 2)")]
    public int terreRequise = 1;
    public int eauRequise = 1;
    public bool accepteUniquementCarnivore = false; // true pour le BacCarnivore

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
    public bool aGraine { get; private set; }
    public bool morte { get; private set; }
    public int niveauPlante { get; private set; } = 1;
    public bool recompenseDonnee { get; private set; }

    // Compteurs (au lieu de bools) pour gérer le bac carnivore qui demande
    // plusieurs actions de terre / d'eau. Les propriétés aTerre/aEau exposent
    // un bool basé sur ces compteurs, pour ne pas casser le reste du code.
    private int nbTerreAjoutee = 0;
    private int nbEauAjoutee = 0;
    public bool aTerre => nbTerreAjoutee >= terreRequise;
    public bool aEau   => nbEauAjoutee   >= eauRequise;

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
        if (visuelTerre != null) visuelTerre.SetActive(false);
        if (visuelGraine != null)
        {
            // Mémorise la scale d'origine pour pouvoir y revenir après un Reset
            scaleGraineInit = visuelGraine.transform.localScale;
            visuelGraine.SetActive(false);
        }
        if (visuelEau != null) visuelEau.SetActive(false);
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
        // La pelle : enlève une plante mature OU morte. Test avant le check morte.
        if (item == "pelle" && (recompenseDonnee || morte)) return dureeCreuser;

        if (morte) return 0f;

        // Filtre carnivore : ce bac n'accepte QUE la graine_4 ;
        //                    inversement les autres bacs la refusent.
        if (EstUneGraine(item))
        {
            bool estCarnivore = NiveauDeGraine(item) == 4;
            if (accepteUniquementCarnivore && !estCarnivore) return 0f;
            if (!accepteUniquementCarnivore && estCarnivore) return 0f;
        }

        if (item == "terre" && nbTerreAjoutee < terreRequise) return dureeRemplirTerre;
        if (EstUneGraine(item) && aTerre && !aGraine)         return dureePlanterGraine;
        if (item == "eau" && aGraine && nbEauAjoutee < eauRequise) return dureeArroser;
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

    // Couleur de la plante selon son niveau (alignée sur la couleur du sachet)
    static Color CouleurPourNiveau(int n)
    {
        switch (n)
        {
            case 2: return new Color(0.60f, 0.45f, 0.85f); // violet
            case 3: return new Color(0.30f, 0.55f, 0.95f); // bleu
            case 4: return new Color(0.85f, 0.15f, 0.15f); // rouge (carnivore)
            default: return new Color(0.30f, 0.65f, 0.30f); // vert (N1)
        }
    }

    public string RaisonRefus(string item)
    {
        if (item == "pelle")   return (recompenseDonnee || morte) ? ""
                                     : "Rien à enlever : laisse la plante pousser.";
        if (morte)             return "Cette plante est morte. Utilise la pelle pour la retirer.";
        if (item == "")        return "Mains vides. Prends terre, graine ou arrosoir.";
        if (item == "terre")   return aTerre  ? "Le pot a déjà assez de terre." : "";
        if (EstUneGraine(item))
        {
            bool estCarnivore = NiveauDeGraine(item) == 4;
            if (accepteUniquementCarnivore && !estCarnivore)
                return "Ce bac n'accepte que la graine carnivore.";
            if (!accepteUniquementCarnivore && estCarnivore)
                return "La graine carnivore a besoin d'un bac spécial.";
            return !aTerre ? "Mets d'abord assez de terre dans le pot." :
                   aGraine ? "Une graine est déjà plantée." : "";
        }
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
        // La pelle marche même si la plante est morte (= nettoyage)
        if (item == "pelle" && (recompenseDonnee || morte))
        {
            ReinitialiserPot();
            // La pelle est un outil → retourne à sa place, n'est PAS consommée
            // GameState.consommerAuRelache vaut false pour les Pickup, donc OK
            return;
        }

        if (morte) return;

        if (item == "terre" && nbTerreAjoutee < terreRequise)
        {
            nbTerreAjoutee++;
            if (visuelTerre != null && nbTerreAjoutee == 1) visuelTerre.SetActive(true);
            GameState.LibererMain();
            Debug.Log(name + " : terre " + nbTerreAjoutee + "/" + terreRequise + ".");
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play(AudioManager.Instance.pickupDirt);
        }
        else if (EstUneGraine(item) && aTerre && !aGraine)
        {
            aGraine = true;
            niveauPlante = NiveauDeGraine(item);
            if (visuelGraine != null)
            {
                visuelGraine.SetActive(true);
                scaleGraineInit = visuelGraine.transform.localScale;
                // Couleur de la plante alignée sur celle du sachet
                Renderer r = visuelGraine.GetComponent<Renderer>();
                if (r != null) r.material.color = CouleurPourNiveau(niveauPlante);
            }
            GameState.LibererMain();
            Debug.Log(name + " : graine N" + niveauPlante + " plantée.");
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play(AudioManager.Instance.plantSeed);
        }
        else if (item == "eau" && aGraine && nbEauAjoutee < eauRequise)
        {
            nbEauAjoutee++;
            if (visuelEau != null && nbEauAjoutee == 1) visuelEau.SetActive(true);
            // La croissance démarre quand on a atteint le seuil d'arrosage
            if (nbEauAjoutee >= eauRequise) tempsDepuisArrosage = 0f;
            // L'arrosoir reste en main : le joueur le repose avec R
            Debug.Log(name + " : eau " + nbEauAjoutee + "/" + eauRequise +
                      (nbEauAjoutee >= eauRequise ? " — la plante commence à pousser." : ""));

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(AudioManager.Instance.wateringCan);
                // Quand l'arrosage déclenche la croissance, son spécifique
                if (nbEauAjoutee >= eauRequise)
                {
                    AudioClip clipPousse = (niveauPlante == 4)
                        ? AudioManager.Instance.carnivoreGrow
                        : AudioManager.Instance.plantGrow;
                    AudioManager.Instance.PlayAt(clipPousse, transform.position, 0.8f);
                }
            }
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

        // Hauteur : moitié au sol (rampant), moitié en l'air (volant). Les
        // volants sont bien au-dessus du pot, à hauteur de la plante.
        bool volant = Random.value < 0.5f;
        n.hauteurCentre = volant
            ? Random.Range(0.70f, 1.10f)   // insectes volants à mi-hauteur / au-dessus
            : Random.Range(0.20f, 0.35f);  // insectes rampant au-dessus du rebord du pot

        nuisibles.Add(n);
        Debug.Log(name + " : nuisible " + (volant ? "volant" : "au sol") +
                  " apparu (" + (nuisiblesSpawnes + 1) + "/" + taillesAttaque + ").");
    }

    // ── Reset complet du pot (déclenché par la pelle) ────────────────────────
    void ReinitialiserPot()
    {
        // 1) Détruit les nuisibles restants
        foreach (var n in nuisibles) if (n != null) Destroy(n.gameObject);
        nuisibles.Clear();

        // 2) Cache et reset les visuels
        if (visuelTerre  != null) visuelTerre.SetActive(false);
        if (visuelEau    != null) visuelEau.SetActive(false);
        if (visuelGraine != null)
        {
            visuelGraine.transform.localScale = scaleGraineInit;
            // Remet la couleur d'origine si la plante était morte
            Renderer r = visuelGraine.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(0.30f, 0.65f, 0.30f);
            visuelGraine.SetActive(false);
        }

        // 3) Reset de l'état logique
        nbTerreAjoutee = 0;
        nbEauAjoutee = 0;
        aGraine = false;
        morte = false;
        recompenseDonnee = false;
        attaqueDeclenchee = false;
        attaqueTerminee = false;
        niveauPlante = 1;
        nuisiblesSpawnes = 0;
        tempsDepuisArrosage = 0f;
        tempsDepuisDernierSpawn = 0f;
        vie = vieMax;

        // 4) La pelle reste en main : le joueur la repose avec R quand il veut
        Debug.Log(name + " : pot vidé. Tu peux replanter.");
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
