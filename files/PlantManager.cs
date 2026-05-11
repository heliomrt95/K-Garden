using UnityEngine;

/// <summary>
/// Représente une plante individuelle qui pousse dans un pot.
/// Construite par capsules empilées + une "fleur" en sphère au sommet.
/// La taille augmente au fil du temps jusqu'à atteindre la maturité.
/// </summary>
public class PlantManager : MonoBehaviour
{
    public int seedIndex { get; private set; }
    public PotInteraction parentPot { get; private set; }

    public float health = 100f;
    public bool isMature = false;

    private float growthTime;
    private float growthDuration;
    private float boostMultiplier = 1f;

    private GameObject stem;
    private GameObject flower;
    private Color stemColor;
    private Color flowerColor;

    public void Initialize(int idx, PotInteraction pot)
    {
        seedIndex = idx;
        parentPot = pot;

        PlantData data = GameManager.Instance.plantDataset[idx];
        growthDuration = data.growthDuration;
        stemColor = data.stemColor;
        flowerColor = data.flowerColor;

        BuildVisual();
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

        growthTime += Time.deltaTime * boostMultiplier;
        float t = Mathf.Clamp01(growthTime / growthDuration);

        // Interpolation de la taille (de 0.05 à 1.0 sur la tige)
        float scale = Mathf.Lerp(0.05f, 1.0f, t);
        stem.transform.localScale = new Vector3(scale * 0.4f, scale, scale * 0.4f);
        flower.transform.localPosition = new Vector3(0, scale, 0);
        flower.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.35f, t);

        // Décroissance de la consommation d'eau (juste pour réinit le boost)
        boostMultiplier = Mathf.MoveTowards(boostMultiplier, 1f, Time.deltaTime * 0.1f);

        if (t >= 1f) Mature();
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
