using UnityEngine;

/// <summary>
/// Représente une plante individuelle qui pousse dans un pot.
/// Construite par capsules empilées + une "fleur" en sphère au sommet.
/// La taille augmente au fil du temps jusqu'à atteindre la maturité.
/// </summary>
public class PlantManager : MonoBehaviour
{
    public static PlantManager ActivePlant { get; private set; }

    public int seedIndex { get; private set; }
    public PotInteraction parentPot { get; private set; }

    public float health = 100f;
    public float maxHealth = 100f;
    public bool isMature = false;

    private float growthTime;
    private float growthDuration;
    private float boostMultiplier = 1f;

    private GameObject stem;
    private GameObject flower;
    private GameObject prefabInstance; // utilisé si un FBX est chargé via Resources
    private Vector3 prefabFinalScale = Vector3.one;
    private Color stemColor;
    private Color flowerColor;

    public float GrowthProgress => growthDuration > 0 ? Mathf.Clamp01(growthTime / growthDuration) : 0f;
    public float HealthFraction => maxHealth > 0 ? Mathf.Clamp01(health / maxHealth) : 0f;

    public void Initialize(int idx, PotInteraction pot)
    {
        seedIndex = idx;
        parentPot = pot;
        ActivePlant = this;

        PlantData data = GameManager.Instance.plantDataset[idx];
        growthDuration = data.growthDuration;
        stemColor = data.stemColor;
        flowerColor = data.flowerColor;

        GameObject prefab = Resources.Load<GameObject>($"Plants/Plant_{idx}");
        if (prefab != null)
        {
            BuildFromPrefab(prefab);
        }
        else
        {
            BuildVisual();
        }
    }

    private void BuildFromPrefab(GameObject prefab)
    {
        prefabInstance = Instantiate(prefab, transform);
        prefabInstance.transform.localPosition = Vector3.zero;
        prefabInstance.transform.localRotation = Quaternion.identity;

        // Calcule un scale cible pour ramener la plante à environ 1 m de haut
        Renderer[] renderers = prefabInstance.GetComponentsInChildren<Renderer>();
        Bounds combined = new Bounds();
        bool init = false;
        foreach (var r in renderers)
        {
            if (!init) { combined = r.bounds; init = true; }
            else combined.Encapsulate(r.bounds);
        }
        if (init && combined.size.y > 0.001f)
        {
            const float targetHeight = 1f;
            prefabFinalScale = Vector3.one * (targetHeight / combined.size.y);
        }
        prefabInstance.transform.localScale = prefabFinalScale * 0.05f;

        // Les colliders du prefab gêneraient le raycast — on ne les veut pas
        foreach (var col in prefabInstance.GetComponentsInChildren<Collider>())
            Destroy(col);

        SphereCollider sc = gameObject.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.3f;
        gameObject.tag = "Plant";
    }

    private void BuildVisual()
    {
        // Tige (capsule)
        stem = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        stem.name = "Stem";
        stem.transform.parent = transform;
        stem.transform.localPosition = Vector3.zero;
        stem.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f); // début minuscule
        stem.GetComponent<Renderer>().material = CreateMat(stemColor);
        Destroy(stem.GetComponent<Collider>());

        // Fleur (sphère au sommet)
        flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flower.name = "Flower";
        flower.transform.parent = transform;
        flower.transform.localPosition = new Vector3(0, 0.1f, 0);
        flower.transform.localScale = Vector3.one * 0.05f;
        flower.GetComponent<Renderer>().material = CreateMat(flowerColor);
        Destroy(flower.GetComponent<Collider>());

        // Petites feuilles (capsules horizontales)
        for (int i = 0; i < 3; i++)
        {
            GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leaf.name = $"Leaf_{i}";
            leaf.transform.parent = stem.transform;
            float angle = i * 120f;
            leaf.transform.localPosition = new Vector3(0, 0, 0);
            leaf.transform.localRotation = Quaternion.Euler(0, angle, 90);
            leaf.transform.localScale = new Vector3(0.4f, 1.5f, 0.4f);
            leaf.GetComponent<Renderer>().material = CreateMat(stemColor * 1.2f);
            Destroy(leaf.GetComponent<Collider>());
        }

        // Collider de zone "plante" pour que les nuisibles puissent l'atteindre
        SphereCollider sc = gameObject.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.3f;
        gameObject.tag = "Plant";
    }

    private Material CreateMat(Color c)
    {
        Material m = new Material(Shader.Find("Standard"));
        m.color = c;
        return m;
    }

    private void Update()
    {
        if (isMature) return;
        if (stem == null && prefabInstance == null) return; // fakePlant (boss)

        growthTime += Time.deltaTime * boostMultiplier;
        float t = Mathf.Clamp01(growthTime / growthDuration);

        if (prefabInstance != null)
        {
            prefabInstance.transform.localScale = prefabFinalScale * Mathf.Lerp(0.05f, 1f, t);
        }
        else
        {
            // Fallback procédural : capsule + sphère
            float scale = Mathf.Lerp(0.05f, 1.0f, t);
            stem.transform.localScale = new Vector3(scale * 0.4f, scale, scale * 0.4f);
            flower.transform.localPosition = new Vector3(0, scale, 0);
            flower.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.35f, t);
        }

        boostMultiplier = Mathf.MoveTowards(boostMultiplier, 1f, Time.deltaTime * 0.1f);

        if (t >= 1f) Mature();
    }

    private void OnDestroy()
    {
        if (ActivePlant == this) ActivePlant = null;
    }

    private void Mature()
    {
        isMature = true;
        Debug.Log($"[Plant {seedIndex}] Maturité atteinte !");
        if (parentPot != null) parentPot.OnPlantMatured();
        if (WaveManager.Instance != null) WaveManager.Instance.StopWave(parentPot);
    }

    public void TakeDamage(float amount)
    {
        if (isMature) return;
        health -= amount;
        if (health <= 0)
        {
            Debug.Log($"[Plant {seedIndex}] Détruite par les nuisibles !");
            if (parentPot != null) parentPot.OnPlantDestroyed();
            if (WaveManager.Instance != null) WaveManager.Instance.StopWave(parentPot);
            Destroy(gameObject);
        }
    }

    public void AddBoost(float duration)
    {
        boostMultiplier = 2.5f;
    }
}
