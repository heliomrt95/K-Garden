using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Construit toute la scène K-Garden uniquement via des primitives Unity.
/// À placer sur un GameObject vide "LevelBuilder" dans la scène.
/// S'exécute au Start() et instancie : sol, murs en verre, table, pots,
/// outils (arrosoir, sprays), graine de départ, tas de terreau.
/// </summary>
public class LevelBuilder : MonoBehaviour
{
    [Header("Dimensions de la serre")]
    public float greenhouseWidth = 16f;
    public float greenhouseLength = 24f;
    public float greenhouseHeight = 5f;
    public float wallThickness = 0.2f;

    [Header("Pots")]
    public int potsPerSide = 5;
    public float potSpacing = 4f;

    // Matériaux mis en cache pour éviter les doublons
    private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

    // Références exposées pour les autres managers
    [HideInInspector] public List<GameObject> pots = new List<GameObject>();
    [HideInInspector] public GameObject centralTable;
    [HideInInspector] public GameObject soilPile;
    [HideInInspector] public GameObject wateringCan;
    [HideInInspector] public GameObject fertilizerSpray;
    [HideInInspector] public GameObject pestSpray;
    [HideInInspector] public GameObject startingSeed;

    private void Awake()
    {
        BuildLevel();
    }

    private void Start()
    {
        BakeNavMesh();
    }

    private void BakeNavMesh()
    {
        System.Type surfaceType = System.Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
        if (surfaceType == null)
        {
            Debug.LogWarning("[LevelBuilder] Package 'AI Navigation' introuvable. Installez-le pour activer le NavMesh.");
            return;
        }

        Component surface = GetComponent(surfaceType) ?? gameObject.AddComponent(surfaceType);

        // CollectObjects.All = 0
        var collectField = surfaceType.GetField("collectObjects");
        if (collectField != null) collectField.SetValue(surface, 0);

        var buildMethod = surfaceType.GetMethod("BuildNavMesh");
        if (buildMethod != null) buildMethod.Invoke(surface, null);

        Debug.Log("[LevelBuilder] NavMesh baked.");
    }

    private void BuildLevel()
    {
        BuildFloor();
        BuildWalls();
        BuildRoof();
        BuildCentralTable();
        BuildPots();
        BuildStartingItems();
        BuildSoilPile();
        BuildWateringCan();
        SetupLighting();
    }

    // ─────────────────────────────────────────────
    // SOL : carrelage en pierre au centre + terre battue sur les bords
    // ─────────────────────────────────────────────
    private void BuildFloor()
    {
        // Terre battue (toute la surface, sous le carrelage)
        GameObject dirt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dirt.name = "Floor_Dirt";
        dirt.transform.parent = transform;
        dirt.transform.position = new Vector3(0, -0.1f, 0);
        dirt.transform.localScale = new Vector3(greenhouseWidth, 0.2f, greenhouseLength);
        dirt.GetComponent<Renderer>().material = GetMaterial("Dirt", new Color(0.36f, 0.25f, 0.18f));
        dirt.isStatic = true;

        // Allée centrale en carrelage de pierre
        GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
        path.name = "Floor_StonePath";
        path.transform.parent = transform;
        path.transform.position = new Vector3(0, 0.01f, 0);
        path.transform.localScale = new Vector3(3f, 0.05f, greenhouseLength - 0.5f);
        path.GetComponent<Renderer>().material = GetMaterial("Stone", new Color(0.65f, 0.62f, 0.58f));
        path.isStatic = true;

        // NavMesh : on ajoute un NavMeshSurface au runtime via composant statique
        // Note : nécessite le package "AI Navigation" — voir instructions plus bas
    }

    // ─────────────────────────────────────────────
    // MURS : 4 murs transparents simulant le verre
    // ─────────────────────────────────────────────
    private void BuildWalls()
    {
        Material glass = GetTransparentMaterial("Glass", new Color(0.7f, 0.85f, 0.9f, 0.25f));

        // Mur Nord (porte au milieu — on la simule en laissant un trou simple : 2 sous-murs)
        BuildWallWithDoor(new Vector3(0, greenhouseHeight / 2f, greenhouseLength / 2f),
                          new Vector3(greenhouseWidth, greenhouseHeight, wallThickness), glass);

        // Mur Sud
        BuildWall("Wall_South", new Vector3(0, greenhouseHeight / 2f, -greenhouseLength / 2f),
                  new Vector3(greenhouseWidth, greenhouseHeight, wallThickness), glass);

        // Mur Est
        BuildWall("Wall_East", new Vector3(greenhouseWidth / 2f, greenhouseHeight / 2f, 0),
                  new Vector3(wallThickness, greenhouseHeight, greenhouseLength), glass);

        // Mur Ouest
        BuildWall("Wall_West", new Vector3(-greenhouseWidth / 2f, greenhouseHeight / 2f, 0),
                  new Vector3(wallThickness, greenhouseHeight, greenhouseLength), glass);

        // Cadres en bois sur les arêtes (esthétique)
        BuildWoodenFrame();
    }

    private void BuildWall(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.parent = transform;
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().material = mat;
        wall.isStatic = true;
    }

    private void BuildWallWithDoor(Vector3 center, Vector3 scale, Material mat)
    {
        // Porte simulée par un cube vert au centre, mur découpé en 2 bandes latérales + 1 linteau
        float doorWidth = 2f;
        float doorHeight = 3f;
        float sideWidth = (scale.x - doorWidth) / 2f;

        // Bande gauche
        BuildWall("Wall_North_Left",
            new Vector3(center.x - (doorWidth / 2f + sideWidth / 2f), center.y, center.z),
            new Vector3(sideWidth, scale.y, scale.z), mat);

        // Bande droite
        BuildWall("Wall_North_Right",
            new Vector3(center.x + (doorWidth / 2f + sideWidth / 2f), center.y, center.z),
            new Vector3(sideWidth, scale.y, scale.z), mat);

        // Linteau au-dessus de la porte
        BuildWall("Wall_North_Top",
            new Vector3(center.x, doorHeight + (scale.y - doorHeight) / 2f, center.z),
            new Vector3(doorWidth, scale.y - doorHeight, scale.z), mat);

        // Porte en bois (verte)
        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Door";
        door.tag = "Door";
        door.transform.parent = transform;
        door.transform.position = new Vector3(center.x, doorHeight / 2f, center.z);
        door.transform.localScale = new Vector3(doorWidth, doorHeight, wallThickness * 1.5f);
        door.GetComponent<Renderer>().material = GetMaterial("DoorWood", new Color(0.18f, 0.35f, 0.18f));
    }

    private void BuildWoodenFrame()
    {
        Material wood = GetMaterial("Wood", new Color(0.42f, 0.27f, 0.15f));
        float t = 0.15f;

        // 4 piliers verticaux aux coins
        Vector3[] corners = {
            new Vector3( greenhouseWidth/2f, greenhouseHeight/2f,  greenhouseLength/2f),
            new Vector3(-greenhouseWidth/2f, greenhouseHeight/2f,  greenhouseLength/2f),
            new Vector3( greenhouseWidth/2f, greenhouseHeight/2f, -greenhouseLength/2f),
            new Vector3(-greenhouseWidth/2f, greenhouseHeight/2f, -greenhouseLength/2f),
        };
        foreach (var c in corners)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Pillar";
            pillar.transform.parent = transform;
            pillar.transform.position = c;
            pillar.transform.localScale = new Vector3(t * 2f, greenhouseHeight, t * 2f);
            pillar.GetComponent<Renderer>().material = wood;
        }
    }

    // ─────────────────────────────────────────────
    // TOIT : 2 plans inclinés en verre formant un V inversé
    // ─────────────────────────────────────────────
    private void BuildRoof()
    {
        Material glass = GetTransparentMaterial("RoofGlass", new Color(0.7f, 0.85f, 0.9f, 0.25f));
        float slope = 25f; // angle d'inclinaison en degrés
        float panelWidth = greenhouseWidth / 2f / Mathf.Cos(slope * Mathf.Deg2Rad);

        // Panneau gauche
        GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cube);
        left.name = "Roof_Left";
        left.transform.parent = transform;
        left.transform.position = new Vector3(-greenhouseWidth / 4f, greenhouseHeight + Mathf.Tan(slope * Mathf.Deg2Rad) * greenhouseWidth / 4f, 0);
        left.transform.rotation = Quaternion.Euler(0, 0, slope);
        left.transform.localScale = new Vector3(panelWidth, 0.1f, greenhouseLength);
        left.GetComponent<Renderer>().material = glass;

        // Panneau droit
        GameObject right = GameObject.CreatePrimitive(PrimitiveType.Cube);
        right.name = "Roof_Right";
        right.transform.parent = transform;
        right.transform.position = new Vector3(greenhouseWidth / 4f, greenhouseHeight + Mathf.Tan(slope * Mathf.Deg2Rad) * greenhouseWidth / 4f, 0);
        right.transform.rotation = Quaternion.Euler(0, 0, -slope);
        right.transform.localScale = new Vector3(panelWidth, 0.1f, greenhouseLength);
        right.GetComponent<Renderer>().material = glass;
    }

    // ─────────────────────────────────────────────
    // TABLE CENTRALE : plateau + 4 pieds
    // ─────────────────────────────────────────────
    private void BuildCentralTable()
    {
        centralTable = new GameObject("CentralTable");
        centralTable.transform.parent = transform;
        centralTable.transform.position = Vector3.zero;

        Material wood = GetMaterial("Wood", new Color(0.42f, 0.27f, 0.15f));

        float topW = 3f, topL = 1.6f, topH = 0.1f;
        float legH = 0.9f, legT = 0.15f;

        // Plateau
        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.name = "TableTop";
        top.transform.parent = centralTable.transform;
        top.transform.localPosition = new Vector3(0, legH, 0);
        top.transform.localScale = new Vector3(topW, topH, topL);
        top.GetComponent<Renderer>().material = wood;

        // 4 pieds
        Vector3[] legPositions = {
            new Vector3( topW/2f - legT, legH/2f,  topL/2f - legT),
            new Vector3(-topW/2f + legT, legH/2f,  topL/2f - legT),
            new Vector3( topW/2f - legT, legH/2f, -topL/2f + legT),
            new Vector3(-topW/2f + legT, legH/2f, -topL/2f + legT),
        };
        foreach (var lp in legPositions)
        {
            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leg.name = "TableLeg";
            leg.transform.parent = centralTable.transform;
            leg.transform.localPosition = lp;
            leg.transform.localScale = new Vector3(legT, legH, legT);
            leg.GetComponent<Renderer>().material = wood;
        }
    }

    // ─────────────────────────────────────────────
    // POTS : 5 à gauche + 5 à droite (cylindres)
    // ─────────────────────────────────────────────
    private void BuildPots()
    {
        Material clay = GetMaterial("Clay", new Color(0.55f, 0.27f, 0.15f));
        Material soil = GetMaterial("Soil", new Color(0.25f, 0.15f, 0.08f));

        float startZ = -(potsPerSide - 1) * potSpacing / 2f;
        float xLeft = -greenhouseWidth / 2f + 1.5f;
        float xRight = greenhouseWidth / 2f - 1.5f;

        for (int i = 0; i < potsPerSide; i++)
        {
            float z = startZ + i * potSpacing;
            pots.Add(CreatePot(new Vector3(xLeft, 0.5f, z), $"Pot_L{i}", clay, soil));
            pots.Add(CreatePot(new Vector3(xRight, 0.5f, z), $"Pot_R{i}", clay, soil));
        }
    }

    private GameObject CreatePot(Vector3 pos, string name, Material clay, Material soil)
    {
        // Le pot lui-même
        GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pot.name = name;
        pot.tag = "Pot";
        pot.transform.parent = transform;
        pot.transform.position = pos;
        pot.transform.localScale = new Vector3(1f, 0.5f, 1f);
        pot.GetComponent<Renderer>().material = clay;

        // On ajoute un trigger pour l'interaction (en plus du collider de la primitive)
        SphereCollider trigger = pot.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.8f;

        // Le composant gameplay
        pot.AddComponent<PotInteraction>();

        // Visualisation de la terre (cylindre plus petit dans le pot, masqué au début)
        GameObject soilVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        soilVisual.name = "SoilVisual";
        soilVisual.transform.parent = pot.transform;
        soilVisual.transform.localPosition = new Vector3(0, 0.4f, 0);
        soilVisual.transform.localScale = new Vector3(0.85f, 0.1f, 0.85f);
        soilVisual.GetComponent<Renderer>().material = soil;
        Destroy(soilVisual.GetComponent<Collider>());
        soilVisual.SetActive(false); // activé après ajout de terre

        return pot;
    }

    // ─────────────────────────────────────────────
    // OBJETS DE DÉPART sur la table
    // ─────────────────────────────────────────────
    private void BuildStartingItems()
    {
        float tableY = 1.0f; // hauteur du plateau

        // Spray d'engrais (cylindre bleu + petit cube blanc pour le bouton)
        fertilizerSpray = BuildSpray("FertilizerSpray", new Vector3(-0.8f, tableY + 0.2f, 0),
            new Color(0.2f, 0.5f, 0.9f), ItemType.FertilizerSpray);

        // Spray anti-nuisibles (cylindre rouge)
        pestSpray = BuildSpray("PestSpray", new Vector3(0.8f, tableY + 0.2f, 0),
            new Color(0.85f, 0.15f, 0.15f), ItemType.PestSpray);

        // Première graine (petite sphère verte)
        startingSeed = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        startingSeed.name = "Seed_Drosera";
        startingSeed.tag = "Pickup";
        startingSeed.transform.parent = transform;
        startingSeed.transform.position = new Vector3(0, tableY + 0.15f, 0.4f);
        startingSeed.transform.localScale = Vector3.one * 0.15f;
        startingSeed.GetComponent<Renderer>().material = GetMaterial("Seed1", new Color(0.6f, 0.8f, 0.2f));

        Rigidbody rb = startingSeed.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        ItemPickup pickup = startingSeed.AddComponent<ItemPickup>();
        pickup.itemType = ItemType.Seed;
        pickup.seedIndex = 0; // Drosera = plante 1
    }

    private GameObject BuildSpray(string name, Vector3 pos, Color tint, ItemType type)
    {
        GameObject root = new GameObject(name);
        root.transform.parent = transform;
        root.transform.position = pos;
        root.tag = "Pickup";

        // Corps principal (cylindre)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.parent = root.transform;
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.15f, 0.25f, 0.15f);
        body.GetComponent<Renderer>().material = GetMaterial(name + "_Body", tint);

        // Embout / gâchette (petit cube)
        GameObject trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trigger.name = "Trigger";
        trigger.transform.parent = root.transform;
        trigger.transform.localPosition = new Vector3(0.1f, 0.2f, 0);
        trigger.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
        trigger.GetComponent<Renderer>().material = GetMaterial("SprayTrigger", new Color(0.9f, 0.9f, 0.9f));

        // Buse (petite capsule horizontale)
        GameObject nozzle = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        nozzle.name = "Nozzle";
        nozzle.transform.parent = root.transform;
        nozzle.transform.localPosition = new Vector3(0.2f, 0.2f, 0);
        nozzle.transform.localRotation = Quaternion.Euler(0, 0, 90);
        nozzle.transform.localScale = new Vector3(0.04f, 0.05f, 0.04f);
        nozzle.GetComponent<Renderer>().material = GetMaterial("SprayTrigger", new Color(0.9f, 0.9f, 0.9f));

        // Collider global pour pickup
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.size = new Vector3(0.4f, 0.6f, 0.3f);
        col.isTrigger = true;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        ItemPickup pickup = root.AddComponent<ItemPickup>();
        pickup.itemType = type;

        return root;
    }

    // ─────────────────────────────────────────────
    // TAS DE TERREAU : sphère aplatie marron
    // ─────────────────────────────────────────────
    private void BuildSoilPile()
    {
        soilPile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        soilPile.name = "SoilPile";
        soilPile.tag = "SoilPile";
        soilPile.transform.parent = transform;
        soilPile.transform.position = new Vector3(-greenhouseWidth / 2f + 1f, 0.3f, greenhouseLength / 2f - 2f);
        soilPile.transform.localScale = new Vector3(1.2f, 0.6f, 1.2f);
        soilPile.GetComponent<Renderer>().material = GetMaterial("Soil", new Color(0.25f, 0.15f, 0.08f));

        // Trigger d'interaction
        SphereCollider trig = soilPile.AddComponent<SphereCollider>();
        trig.isTrigger = true;
        trig.radius = 0.8f;
    }

    // ─────────────────────────────────────────────
    // ARROSOIR : cylindre + bec en capsule + anse
    // ─────────────────────────────────────────────
    private void BuildWateringCan()
    {
        wateringCan = new GameObject("WateringCan");
        wateringCan.transform.parent = transform;
        wateringCan.transform.position = new Vector3(greenhouseWidth / 2f - 1f, 0.4f, greenhouseLength / 2f - 2f);
        wateringCan.tag = "Pickup";

        Material metal = GetMaterial("Metal", new Color(0.6f, 0.7f, 0.75f));

        // Corps
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "CanBody";
        body.transform.parent = wateringCan.transform;
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);
        body.GetComponent<Renderer>().material = metal;

        // Bec long (capsule)
        GameObject spout = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        spout.name = "Spout";
        spout.transform.parent = wateringCan.transform;
        spout.transform.localPosition = new Vector3(0.45f, 0.15f, 0);
        spout.transform.localRotation = Quaternion.Euler(0, 0, -55f);
        spout.transform.localScale = new Vector3(0.1f, 0.3f, 0.1f);
        spout.GetComponent<Renderer>().material = metal;

        // Anse (capsule courbée simulée par 2 capsules)
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        handle.name = "Handle";
        handle.transform.parent = wateringCan.transform;
        handle.transform.localPosition = new Vector3(-0.35f, 0.25f, 0);
        handle.transform.localRotation = Quaternion.Euler(0, 0, 45);
        handle.transform.localScale = new Vector3(0.05f, 0.2f, 0.05f);
        handle.GetComponent<Renderer>().material = metal;

        // Collider global pour pickup
        BoxCollider col = wateringCan.AddComponent<BoxCollider>();
        col.size = new Vector3(1.2f, 1f, 0.6f);
        col.isTrigger = true;

        Rigidbody rb = wateringCan.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        ItemPickup pickup = wateringCan.AddComponent<ItemPickup>();
        pickup.itemType = ItemType.WateringCan;
    }

    // ─────────────────────────────────────────────
    // ÉCLAIRAGE
    // ─────────────────────────────────────────────
    private void SetupLighting()
    {
        // Soleil (lumière directionnelle)
        GameObject sun = new GameObject("Sun");
        sun.transform.parent = transform;
        Light l = sun.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.35f;
        l.color = new Color(1f, 0.95f, 0.82f);
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.7f;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0);

        // Ambient en mode trichrome (ciel/équateur/sol) pour un rendu plus naturel
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.75f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.40f);
        RenderSettings.ambientGroundColor = new Color(0.20f, 0.18f, 0.14f);
        RenderSettings.ambientIntensity = 1.1f;

        // Trois suspensions chaudes le long de l'allée centrale
        float zStep = greenhouseLength / 4f;
        for (int i = -1; i <= 1; i++)
        {
            GameObject pl = new GameObject($"PendantLight_{i}");
            pl.transform.parent = transform;
            pl.transform.position = new Vector3(0, greenhouseHeight - 0.6f, i * zStep);

            Light pll = pl.AddComponent<Light>();
            pll.type = LightType.Point;
            pll.color = new Color(1f, 0.83f, 0.55f);
            pll.intensity = 1.4f;
            pll.range = 9f;
            pll.shadows = LightShadows.Soft;

            // Petit globe visible (capsule jaune émissive)
            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulb";
            bulb.transform.parent = pl.transform;
            bulb.transform.localPosition = Vector3.zero;
            bulb.transform.localScale = Vector3.one * 0.18f;
            Destroy(bulb.GetComponent<Collider>());
            Material bulbMat = new Material(Shader.Find("Standard"));
            bulbMat.color = new Color(1f, 0.9f, 0.6f);
            bulbMat.EnableKeyword("_EMISSION");
            bulbMat.SetColor("_EmissionColor", new Color(1.6f, 1.3f, 0.7f));
            bulb.GetComponent<Renderer>().material = bulbMat;

            // Câble (cylindre fin)
            GameObject cable = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cable.name = "Cable";
            cable.transform.parent = pl.transform;
            cable.transform.localPosition = new Vector3(0, 0.4f, 0);
            cable.transform.localScale = new Vector3(0.012f, 0.4f, 0.012f);
            Destroy(cable.GetComponent<Collider>());
            cable.GetComponent<Renderer>().material = GetMaterial("Cable", new Color(0.1f, 0.1f, 0.1f), 0.2f, 0.4f);
        }

        // Reflection probe au centre pour les reflets sur le verre / l'arrosoir
        GameObject probe = new GameObject("ReflectionProbe");
        probe.transform.parent = transform;
        probe.transform.position = new Vector3(0, greenhouseHeight / 2f, 0);
        ReflectionProbe rp = probe.AddComponent<ReflectionProbe>();
        rp.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        rp.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
        rp.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        rp.resolution = 128;
        rp.size = new Vector3(greenhouseWidth, greenhouseHeight, greenhouseLength);
        rp.RenderProbe();
    }

    // ─────────────────────────────────────────────
    // HELPERS MATÉRIAUX
    // ─────────────────────────────────────────────
    private Material GetMaterial(string key, Color c)
    {
        if (materialCache.TryGetValue(key, out Material m)) return m;
        m = new Material(Shader.Find("Standard"));
        m.color = c;
        materialCache[key] = m;
        return m;
    }

    private Material GetTransparentMaterial(string key, Color c)
    {
        if (materialCache.TryGetValue(key, out Material m)) return m;
        m = new Material(Shader.Find("Standard"));
        // Mode Fade pour transparence
        m.SetFloat("_Mode", 2);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        m.color = c;
        materialCache[key] = m;
        return m;
    }
}
