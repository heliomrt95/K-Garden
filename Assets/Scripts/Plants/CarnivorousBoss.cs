using System.Collections;
using UnityEngine;

/// <summary>
/// Plante carnivore géante : pot dédié au centre de la serre.
/// Phase finale : besoin d'eau et d'engrais X fois, vague de nuisibles intense,
/// puis ouverture des "mâchoires" et drop de la clé.
/// </summary>
public class CarnivorousBoss : MonoBehaviour
{
    [Header("Configuration")]
    public int waterRequired = 5;
    public int fertilizerRequired = 3;
    public int waveSize = 30;
    public float growthPerCare = 0.12f;

    private int waterCount, fertilizerCount;
    private float maturity = 0f;
    private bool jawsOpened = false;
    private bool waveLaunched = false;

    // Composants visuels
    private GameObject body;
    private GameObject jawTop;
    private GameObject jawBottom;
    private GameObject keyObject;

    public static CarnivorousBoss Instance { get; private set; }

    public static GameObject SpawnBossPot()
    {
        if (Instance != null) return Instance.gameObject;

        // Pot boss au centre de la serre
        GameObject root = new GameObject("CarnivorousBossPot");
        root.transform.position = new Vector3(0, 0, 0); // centre, à modifier selon table

        // Pot en cylindre marron foncé plus gros
        GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pot.name = "BossPot";
        pot.transform.parent = root.transform;
        pot.transform.localPosition = new Vector3(0, 1.0f, 0);
        pot.transform.localScale = new Vector3(2f, 0.6f, 2f);
        Material clay = new Material(Shader.Find("Standard"));
        clay.color = new Color(0.3f, 0.2f, 0.1f);
        pot.GetComponent<Renderer>().material = clay;

        // Trigger d'interaction
        SphereCollider sc = pot.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 1.5f;

        CarnivorousBoss boss = root.AddComponent<CarnivorousBoss>();
        boss.BuildBossVisual();

        return root;
    }

    private void Awake() { Instance = this; }

    private void BuildBossVisual()
    {
        Material green = new Material(Shader.Find("Standard"));
        green.color = new Color(0.2f, 0.55f, 0.2f);

        // Corps : gros cylindre vert
        body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "BossBody";
        body.transform.parent = transform;
        body.transform.localPosition = new Vector3(0, 2f, 0);
        body.transform.localScale = new Vector3(0.3f, 1.2f, 0.3f);
        body.GetComponent<Renderer>().material = green;

        // Mâchoires : 2 cubes inclinés qui s'écartent à maturité
        Material jawMat = new Material(Shader.Find("Standard"));
        jawMat.color = new Color(0.6f, 0.15f, 0.15f); // intérieur rouge

        jawTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        jawTop.name = "JawTop";
        jawTop.transform.parent = transform;
        jawTop.transform.localPosition = new Vector3(0, 3.2f, 0.3f);
        jawTop.transform.localRotation = Quaternion.Euler(20, 0, 0);
        jawTop.transform.localScale = new Vector3(1.0f, 0.1f, 0.8f);
        jawTop.GetComponent<Renderer>().material = jawMat;

        jawBottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
        jawBottom.name = "JawBottom";
        jawBottom.transform.parent = transform;
        jawBottom.transform.localPosition = new Vector3(0, 3.2f, -0.3f);
        jawBottom.transform.localRotation = Quaternion.Euler(-20, 0, 0);
        jawBottom.transform.localScale = new Vector3(1.0f, 0.1f, 0.8f);
        jawBottom.GetComponent<Renderer>().material = jawMat;

        // "Dents" : 4 petits cubes blancs sur chaque mâchoire
        AddTeeth(jawTop, 1);
        AddTeeth(jawBottom, -1);

        // Au début, la plante boss est petite : on scale tout
        transform.localScale = Vector3.one * 0.3f;

        // On ajoute aussi un PotInteraction-light : ici simplifié, on traite directement les inputs
        SphereCollider trig = gameObject.AddComponent<SphereCollider>();
        trig.isTrigger = true;
        trig.radius = 2f;
        gameObject.tag = "BossPlant";
    }

    private void AddTeeth(GameObject jaw, int dir)
    {
        Material toothMat = new Material(Shader.Find("Standard"));
        toothMat.color = Color.white;
        for (int i = 0; i < 4; i++)
        {
            GameObject tooth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tooth.name = $"Tooth_{i}";
            tooth.transform.parent = jaw.transform;
            tooth.transform.localPosition = new Vector3(-0.3f + i * 0.2f, 0, 0.4f * dir);
            tooth.transform.localScale = new Vector3(0.08f, 1.5f, 0.08f);
            tooth.GetComponent<Renderer>().material = toothMat;
        }
    }

    /// <summary>
    /// Appelée par InteractionManager via OnTriggerEnter ou interaction directe.
    /// On expose une méthode publique pour les soins.
    /// </summary>
    public void Care(ItemType item)
    {
        if (jawsOpened) return;

        if (item == ItemType.WateringCan)
        {
            waterCount++;
            maturity += growthPerCare;
            Debug.Log($"[Boss] Eau {waterCount}/{waterRequired}");
        }
        else if (item == ItemType.FertilizerSpray)
        {
            fertilizerCount++;
            maturity += growthPerCare;
            Debug.Log($"[Boss] Engrais {fertilizerCount}/{fertilizerRequired}");
        }

        // Lance la vague intense dès que le joueur a planté les 3 graines (= apparition du pot)
        if (!waveLaunched)
        {
            waveLaunched = true;
            if (WaveManager.Instance != null)
                WaveManager.Instance.StartWave(null, waveSize); // null car on traite ce pot spécialement
            // Pour simplifier on peut directement spawn une vague autour du pot
            StartCoroutine(SpawnBossWave());
        }

        if (waterCount >= waterRequired && fertilizerCount >= fertilizerRequired)
            OpenJaws();
    }

    private IEnumerator SpawnBossWave()
    {
        for (int i = 0; i < waveSize; i++)
        {
            if (jawsOpened) yield break;
            GameObject pest = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pest.name = "BossPest";
            pest.transform.localScale = Vector3.one * 0.25f;
            Vector2 rnd = Random.insideUnitCircle.normalized * 8f;
            pest.transform.position = transform.position + new Vector3(rnd.x, 0.15f, rnd.y);
            Material m = new Material(Shader.Find("Standard"));
            m.color = new Color(0.6f, 0f, 0.6f); // violet pour la vague boss
            pest.GetComponent<Renderer>().material = m;

            UnityEngine.AI.NavMeshAgent agent = pest.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.baseOffset = 0.15f;

            PestEnemy enemy = pest.AddComponent<PestEnemy>();
            // On crée un PlantManager fictif pour la cible (la plante boss n'est pas un PlantManager classique)
            // Solution simple : on patche en faisant tendre directement vers transform et on inflige des dégâts au boss
            enemy.Initialize(GetOrCreateFakePlant());

            yield return new WaitForSeconds(0.7f);
        }
    }

    private PlantManager fakePlant;
    private PlantManager GetOrCreateFakePlant()
    {
        if (fakePlant != null) return fakePlant;
        GameObject go = new GameObject("BossPlantTarget");
        go.transform.parent = transform;
        go.transform.localPosition = new Vector3(0, 2f, 0);
        fakePlant = go.AddComponent<PlantManager>();
        // On bypass Initialize() en assignant les valeurs minimales requises
        fakePlant.health = 200f;
        return fakePlant;
    }

    private void Update()
    {
        // Croissance progressive de la plante
        transform.localScale = Vector3.Lerp(transform.localScale,
            Vector3.one * Mathf.Clamp(0.3f + maturity, 0.3f, 1.5f), Time.deltaTime * 0.5f);
    }

    private void OpenJaws()
    {
        if (jawsOpened) return;
        jawsOpened = true;
        Debug.Log("[Boss] Mâchoires ouvertes — la clé est expulsée !");
        StartCoroutine(JawsAnimation());
    }

    private IEnumerator JawsAnimation()
    {
        // Anime l'ouverture pendant 1.5s
        Quaternion topStart = jawTop.transform.localRotation;
        Quaternion botStart = jawBottom.transform.localRotation;
        Quaternion topEnd = Quaternion.Euler(70, 0, 0);
        Quaternion botEnd = Quaternion.Euler(-70, 0, 0);

        float t = 0;
        while (t < 1.5f)
        {
            t += Time.deltaTime;
            float u = t / 1.5f;
            jawTop.transform.localRotation = Quaternion.Slerp(topStart, topEnd, u);
            jawBottom.transform.localRotation = Quaternion.Slerp(botStart, botEnd, u);
            yield return null;
        }

        // Stoppe la vague boss
        if (WaveManager.Instance != null) WaveManager.Instance.StopWave(null);

        // Spawn de la clé (cube doré avec un anneau cylindrique)
        GameObject key = new GameObject("Key");
        key.tag = "Key";
        key.transform.position = transform.position + new Vector3(0, 3.5f, 0);

        Material gold = new Material(Shader.Find("Standard"));
        gold.color = new Color(1f, 0.84f, 0.2f);
        gold.SetFloat("_Metallic", 0.8f);
        gold.SetFloat("_Glossiness", 0.7f);

        // Anneau (cylindre aplati)
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.transform.parent = key.transform;
        ring.transform.localPosition = new Vector3(0, 0.2f, 0);
        ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
        ring.transform.localScale = new Vector3(0.25f, 0.05f, 0.25f);
        ring.GetComponent<Renderer>().material = gold;

        // Tige
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.transform.parent = key.transform;
        shaft.transform.localPosition = Vector3.zero;
        shaft.transform.localScale = new Vector3(0.05f, 0.4f, 0.05f);
        shaft.GetComponent<Renderer>().material = gold;

        // Dents de la clé
        GameObject teeth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        teeth.transform.parent = key.transform;
        teeth.transform.localPosition = new Vector3(0.08f, -0.18f, 0);
        teeth.transform.localScale = new Vector3(0.15f, 0.06f, 0.05f);
        teeth.GetComponent<Renderer>().material = gold;

        // Collider global + flotaison
        BoxCollider col = key.AddComponent<BoxCollider>();
        col.size = new Vector3(0.4f, 0.6f, 0.3f);
        col.isTrigger = true;
        key.AddComponent<KeyFloater>();
    }
}

/// <summary>Petit comportement de flottement pour la clé.</summary>
public class KeyFloater : MonoBehaviour
{
    private Vector3 origin;
    private void Start() { origin = transform.position; }
    private void Update()
    {
        transform.position = origin + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.1f;
        transform.Rotate(Vector3.up * 60f * Time.deltaTime);
    }
}
