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

    [Header("Prefabs 3D (optionnel — remplace les primitives si assigné)")]
    public GameObject wateringCanPrefab;
    public GameObject fertilizerSprayPrefab;
    public GameObject pestSprayPrefab;
    public GameObject potPrefab;
    public GameObject soilPilePrefab;
    public GameObject seedPrefab;

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
        BuildOutdoorTerrain();

        // Si un modèle de serre custom (FBX exporté depuis SketchUp) est présent
        // dans Resources/Greenhouse/Greenhouse.fbx, on l'utilise à la place des
        // murs/toit/cadre procéduraux. Le sol reste procédural (gameplay).
        GameObject greenhouseFbx = Resources.Load<GameObject>("Greenhouse/Greenhouse");
        if (greenhouseFbx != null)
        {
            BuildFloor();
            InstantiateCustomGreenhouse(greenhouseFbx);
        }
        else
        {
            BuildFloor();
            BuildWalls();
            BuildRoof();
        }

        BuildCentralTable();
        BuildPots();
        BuildStartingItems();
        BuildSoilPile();
        BuildWateringCan();
        SetupLighting();
    }

    // Instancie le modèle SKP→FBX et le redimensionne à la taille de la serre procédurale
    private void InstantiateCustomGreenhouse(GameObject prefab)
    {
        GameObject gh = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
        gh.name = "Greenhouse_FBX";

        // SketchUp exporte parfois en Z-up : si on détecte un mesh très haut sur Z, on redresse
        Renderer[] renderers = gh.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        // Si le modèle est plus large en Z qu'en Y et que Y est très petit, on suppose Z-up
        if (b.size.y < b.size.x * 0.3f && b.size.z > b.size.y * 2f)
        {
            gh.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        }

        // Scale pour matcher la taille de la serre (sur la plus grande dimension XZ)
        float targetSize = Mathf.Max(greenhouseWidth, greenhouseLength);
        float modelSize = Mathf.Max(b.size.x, b.size.z);
        if (modelSize > 0.001f)
        {
            float s = targetSize / modelSize;
            gh.transform.localScale = Vector3.one * s;
        }

        // Repose la base à y=0
        Bounds finalBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) finalBounds.Encapsulate(renderers[i].bounds);
        gh.transform.position += new Vector3(0, -finalBounds.min.y, 0);

        // Patch les matériaux URP éventuels
        FixPrefabMaterials(gh, new Color(0.85f, 0.85f, 0.85f));
    }

    // ─────────────────────────────────────────────
    // GROSSE MAP : grand sol herbeux qui entoure la serre
    // ─────────────────────────────────────────────
    private const float MapSize = 200f;       // 200 x 200 m
    private const float MapHalf = MapSize / 2f;

    private Transform outdoorRoot;

    // Cache des matériaux convertis URP → Standard (pour éviter de recréer N fois le même)
    private Dictionary<Material, Material> fixedMaterialCache = new Dictionary<Material, Material>();

    // Remplace tout matériau au shader inconnu/URP par un Standard équivalent.
    // Fallback color permet de garantir une teinte cohérente si la conversion échoue.
    private void FixPrefabMaterials(GameObject go, Color fallback)
    {
        Shader std = Shader.Find("Standard");
        if (std == null) return;
        foreach (var rend in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = rend.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material src = mats[i];
                if (src == null) continue;
                if (src.shader == std) continue;

                if (!fixedMaterialCache.TryGetValue(src, out Material nm))
                {
                    nm = new Material(std);
                    // Récupère la couleur (URP _BaseColor, sinon _Color, sinon fallback)
                    Color c = fallback;
                    if (src.HasProperty("_BaseColor")) c = src.GetColor("_BaseColor");
                    else if (src.HasProperty("_Color")) c = src.GetColor("_Color");
                    if (c.maxColorComponent < 0.05f || (c.r > 0.9f && c.g < 0.1f && c.b > 0.9f))
                        c = fallback;
                    nm.color = c;

                    Texture tex = null;
                    if (src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
                    else if (src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
                    if (tex != null) nm.mainTexture = tex;

                    // Détection des matériaux à découpe alpha (feuillage d'arbres, herbe, fleurs)
                    bool isCutout = false;
                    if (src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f) isCutout = true;
                    if (src.HasProperty("_Cutoff") && src.GetFloat("_Cutoff") > 0.01f) isCutout = true;
                    if (src.IsKeywordEnabled("_ALPHATEST_ON")) isCutout = true;
                    // Heuristique : si la texture a un alpha channel et le nom du matériau évoque
                    // du feuillage, on force le cutout pour ne pas garder des plans pleins
                    string n = src.name?.ToLower() ?? "";
                    if (n.Contains("leaf") || n.Contains("leaves") || n.Contains("foliage") ||
                        n.Contains("grass") || n.Contains("flower") || n.Contains("petal") ||
                        n.Contains("daffodil") || n.Contains("hyacinth") || n.Contains("sunflower") ||
                        n.Contains("birch") || n.Contains("tree")) isCutout = true;

                    if (isCutout)
                    {
                        nm.SetFloat("_Mode", 1); // Cutout
                        nm.SetOverrideTag("RenderType", "TransparentCutout");
                        nm.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        nm.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        nm.SetInt("_ZWrite", 1);
                        nm.EnableKeyword("_ALPHATEST_ON");
                        nm.DisableKeyword("_ALPHABLEND_ON");
                        nm.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        nm.renderQueue = 2450;
                        float cutoff = src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f;
                        nm.SetFloat("_Cutoff", Mathf.Max(0.3f, cutoff));
                        // Empêche le culling pour voir les plans des deux côtés (feuilles)
                        if (nm.HasProperty("_Cull")) nm.SetFloat("_Cull", 0f);
                    }

                    nm.SetFloat("_Glossiness", 0.08f);
                    nm.SetFloat("_Metallic", 0f);

                    fixedMaterialCache[src] = nm;
                }
                mats[i] = nm;
                changed = true;
            }
            if (changed) rend.sharedMaterials = mats;
        }
    }

    private void BuildOutdoorTerrain()
    {
        outdoorRoot = new GameObject("Outdoor").transform;
        outdoorRoot.parent = transform;

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "OutdoorGround";
        ground.transform.parent = outdoorRoot;
        ground.transform.position = new Vector3(0, -0.2f, 0);
        ground.transform.localScale = new Vector3(MapSize / 10f, 1f, MapSize / 10f);
        ground.isStatic = true;

        Shader std = Shader.Find("Standard");
        Material grass = Resources.Load<Material>("Materials/PP_Ground");
        if (grass == null) grass = Resources.Load<Material>("PP_Ground");
        // Si le PP_Ground utilise un shader URP introuvable, on retombe sur un Standard vert
        if (grass == null || (std != null && grass.shader != std))
            grass = GetMaterial("Grass", new Color(0.34f, 0.55f, 0.25f), 0f, 0.05f);
        ground.GetComponent<Renderer>().sharedMaterial = grass;

        // GroundPatches retirés : à grande échelle ils ressemblent à des blocs flottants.
        // On garde un sol uniforme et on laisse la végétation peupler la variété visuelle.
        BuildEntrancePath();
        BuildPerimeterFence();
        PopulateOutdoorMap();
    }

    // Patchs de couleur (terre nue, prairie, mousse foncée) pour casser le grand plan vert
    private void BuildGroundPatches()
    {
        Material[] mats = {
            GetMaterial("PatchDarkGrass", new Color(0.30f, 0.48f, 0.22f), 0f, 0.04f),
            GetMaterial("PatchDryEarth",  new Color(0.46f, 0.36f, 0.22f), 0f, 0.05f),
            GetMaterial("PatchMoss",      new Color(0.28f, 0.42f, 0.18f), 0f, 0.04f),
            GetMaterial("PatchSand",      new Color(0.78f, 0.70f, 0.50f), 0f, 0.05f),
        };

        int patchCount = 18;
        for (int i = 0; i < patchCount; i++)
        {
            Vector3 pos = RandomOutdoorPoint(out bool valid, 8f, MapHalf - 6f);
            if (!valid) continue;

            GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Plane);
            patch.name = "GroundPatch";
            patch.transform.parent = outdoorRoot;
            patch.transform.position = new Vector3(pos.x, -0.18f, pos.z); // au-dessus du sol
            float r = Random.Range(0.6f, 1.6f);
            patch.transform.localScale = new Vector3(r, 1f, r * Random.Range(0.7f, 1.3f));
            patch.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            patch.GetComponent<Renderer>().sharedMaterial = mats[Random.Range(0, mats.Length)];
            Destroy(patch.GetComponent<Collider>());
            patch.isStatic = true;
        }
    }

    // Chemin en dalles depuis la porte (côté +Z) vers le portail extérieur
    private void BuildEntrancePath()
    {
        // PP_Meadow_Path_05 retiré : c'est un gros bloc 3D, il faisait des marches géantes
        GameObject[] tilePrefabs = LoadPrefabs(
            "Decorations/PP_Floor_Tile_05",
            "Decorations/PP_Floor_Tile_06",
            "Decorations/PP_Floor_Tile_15",
            "Decorations/PP_Floor_Tile_16");

        if (tilePrefabs.Length == 0)
        {
            // Fallback : un long ruban en pierre claire
            GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "EntrancePath";
            path.transform.parent = outdoorRoot;
            path.transform.position = new Vector3(0, -0.18f, greenhouseLength / 2f + 12f);
            path.transform.localScale = new Vector3(2.2f, 0.04f, 24f);
            path.GetComponent<Renderer>().sharedMaterial =
                GetMaterial("PathStone", new Color(0.68f, 0.65f, 0.60f), 0.05f, 0.4f);
            Destroy(path.GetComponent<Collider>());
            return;
        }

        float startZ = greenhouseLength / 2f + 1.5f;
        float endZ = startZ + 22f;
        float step = 1.4f;
        int tileIndex = 0;
        for (float z = startZ; z <= endZ; z += step)
        {
            GameObject prefab = tilePrefabs[tileIndex++ % tilePrefabs.Length];
            float x = Mathf.Sin(z * 0.18f) * 0.15f; // micro-courbure
            GameObject tile = Instantiate(prefab,
                new Vector3(x, -0.15f, z),
                Quaternion.Euler(0, Random.Range(-8f, 8f), 0), outdoorRoot);
            tile.name = "PathTile";
            tile.transform.localScale = Vector3.one * Random.Range(0.25f, 0.4f);
            foreach (var col in tile.GetComponentsInChildren<Collider>())
                col.enabled = false;
            FixPrefabMaterials(tile, new Color(0.65f, 0.62f, 0.55f));
        }
    }

    // Clôture autour du terrain pour fermer la map visuellement
    private void BuildPerimeterFence()
    {
        GameObject[] fences = LoadPrefabs(
            "Decorations/PP_Small_Fence_01",
            "Decorations/PP_Small_Fence_04");
        if (fences.Length == 0) return;

        float fenceRadius = 92f;
        int sectionCount = 64;
        // gap autour de la porte (axe +Z)
        const float doorGapAngle = 28f * Mathf.Deg2Rad;
        for (int i = 0; i < sectionCount; i++)
        {
            float a = (i / (float)sectionCount) * Mathf.PI * 2f;
            float angularDist = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 90f)) * Mathf.Deg2Rad;
            if (angularDist < doorGapAngle) continue;

            Vector3 pos = new Vector3(Mathf.Cos(a) * fenceRadius, 0f, Mathf.Sin(a) * fenceRadius);
            // orienté tangent au cercle
            float rotY = -a * Mathf.Rad2Deg + 90f;
            GameObject prefab = fences[Random.Range(0, fences.Length)];
            GameObject f = Instantiate(prefab, pos, Quaternion.Euler(0, rotY, 0), outdoorRoot);
            f.transform.localScale = Vector3.one * Random.Range(0.4f, 0.6f);
            foreach (var col in f.GetComponentsInChildren<Collider>())
                col.enabled = false;
            FixPrefabMaterials(f, new Color(0.45f, 0.30f, 0.18f));
        }
    }

    // Place une grande variété de décorations dans plusieurs anneaux autour de la serre
    private void PopulateOutdoorMap()
    {
        GameObject[] trees = LoadPrefabs(
            "Decorations/PP_Tree_02",
            "Decorations/PP_Tree_10",
            "Decorations/PP_Birch_Tree_05",
            "Decorations/PP_Birch_Tree_06");

        GameObject[] rocks = LoadPrefabs(
            "Decorations/PP_Rock_Moss_Grown_09",
            "Decorations/PP_Rock_Moss_Grown_11",
            "Decorations/PP_Rock_Pile_Forest_Moss_05",
            "Decorations/PP_Rock_Pile_Forest_Moss_10");

        GameObject[] pebbles = LoadPrefabs(
            "Decorations/PP_Cemetery_Pebbles_03",
            "Decorations/PP_Cemetery_Pebbles_09");

        GameObject[] flowers = LoadPrefabs(
            "Decorations/PP_Daffodil_03",
            "Decorations/PP_Hyacinth_04",
            "Decorations/PP_Sunflower_04");

        GameObject[] mushrooms = LoadPrefabs(
            "Decorations/PP_Mushroom_Fantasy_Orange_09",
            "Decorations/PP_Mushroom_Fantasy_Orange_10",
            "Decorations/PP_Mushroom_Fantasy_Purple_05",
            "Decorations/PP_Mushroom_Fantasy_Purple_08");

        GameObject[] grass = LoadPrefabs(
            "Decorations/PP_Grass_11",
            "Decorations/PP_Grass_15");

        GameObject[] meadows = LoadPrefabs(
            "Decorations/PP_Meadow_07",
            "Decorations/PP_Meadow_08");

        GameObject[] moss = LoadPrefabs(
            "Decorations/PP_Forest_Mountain_Moss_01",
            "Decorations/PP_Forest_Mountain_Moss_02");

        float ghHalf = Mathf.Max(greenhouseWidth, greenhouseLength) / 2f;

        // Couleurs fallback (utilisées si le shader URP ne se convertit pas correctement)
        Color treeGreen = new Color(0.18f, 0.42f, 0.18f);
        Color trunkBrown = new Color(0.32f, 0.22f, 0.14f);
        Color rockGray = new Color(0.50f, 0.48f, 0.45f);
        Color mossGreen = new Color(0.28f, 0.40f, 0.20f);
        Color flowerYellow = new Color(0.92f, 0.82f, 0.30f);
        Color mushroomOrange = new Color(0.80f, 0.45f, 0.20f);
        Color grassGreen = new Color(0.34f, 0.55f, 0.25f);

        // ── FORÊT DENSE ──
        // 1) Anneau proche (boisé clair) : arbres + touffes d'herbe
        ScatterPrefabs(trees, 90, ghHalf + 8f, 30f, 0.35f, 0.55f, treeGreen);
        // 2) Anneau moyen (forêt dense)
        ScatterPrefabs(trees, 180, 30f, 60f, 0.4f, 0.65f, treeGreen);
        // 3) Anneau extérieur (forêt très dense — mur d'arbres)
        ScatterPrefabs(trees, 220, 60f, 95f, 0.45f, 0.75f, treeGreen);
        // 4) Bosquets serrés (clusters de 4–6 arbres)
        ScatterTreeClusters(trees, 30, 35f, 90f, treeGreen);

        // ── GROS ROCHERS — TRÈS LOIN ET PETITS, en bordure d'horizon ──
        ScatterPrefabs(rocks, 20, 95f, MapHalf - 5f, 0.25f, 0.5f, rockGray);
        // Petits rochers / cailloux : éparpillés au sol
        ScatterPrefabs(pebbles, 60, ghHalf + 8f, 70f, 0.15f, 0.3f, rockGray);

        // ── SOUS-BOIS ──
        ScatterPrefabs(moss, 90, ghHalf + 4f, 80f, 0.25f, 0.5f, mossGreen);
        ScatterPrefabs(flowers, 140, ghHalf + 3f, 40f, 0.35f, 0.6f, flowerYellow);
        ScatterPrefabs(grass, 280, ghHalf + 2f, 70f, 0.3f, 0.55f, grassGreen);
        ScatterPrefabs(mushrooms, 70, ghHalf + 10f, 75f, 0.3f, 0.5f, mushroomOrange);
        ScatterPrefabs(meadows, 50, ghHalf + 6f, 60f, 0.35f, 0.6f, grassGreen);
    }

    // Crée des bosquets : un point central, puis 4-6 arbres autour à 1-3 m
    private void ScatterTreeClusters(GameObject[] trees, int clusterCount,
                                     float minRadius, float maxRadius, Color fallback)
    {
        if (trees == null || trees.Length == 0) return;
        for (int c = 0; c < clusterCount; c++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(minRadius, maxRadius);
            Vector3 center = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            if (IsInsideGreenhouse(center, 3f)) continue;
            if (IsOnEntrancePath(center, 4f)) continue;

            int treesInCluster = Random.Range(4, 7);
            for (int i = 0; i < treesInCluster; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 3f;
                Vector3 pos = center + new Vector3(offset.x, 0f, offset.y);
                if (IsInsideGreenhouse(pos, 3f)) continue;
                if (IsOnEntrancePath(pos, 4f)) continue;

                GameObject prefab = trees[Random.Range(0, trees.Length)];
                GameObject inst = Instantiate(prefab, pos,
                    Quaternion.Euler(0, Random.Range(0f, 360f), 0), outdoorRoot);
                inst.transform.localScale = Vector3.one * Random.Range(0.4f, 0.7f);
                foreach (var col in inst.GetComponentsInChildren<Collider>())
                    col.enabled = false;
                FixPrefabMaterials(inst, fallback);
            }
        }
    }

    private GameObject[] LoadPrefabs(params string[] paths)
    {
        var list = new List<GameObject>(paths.Length);
        foreach (var p in paths)
        {
            var go = Resources.Load<GameObject>(p);
            if (go != null) list.Add(go);
        }
        return list.ToArray();
    }

    private void ScatterPrefabs(GameObject[] prefabs, int count, float minRadius, float maxRadius,
                                float minScale, float maxScale, Color fallback)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        int placed = 0;
        int safety = count * 6;
        while (placed < count && safety-- > 0)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(minRadius, maxRadius);
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            if (IsInsideGreenhouse(pos, 3f)) continue;
            if (IsOnEntrancePath(pos, 2.5f)) continue;

            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            GameObject inst = Instantiate(prefab, pos,
                Quaternion.Euler(0, Random.Range(0f, 360f), 0), outdoorRoot);
            inst.transform.localScale = Vector3.one * Random.Range(minScale, maxScale);
            foreach (var col in inst.GetComponentsInChildren<Collider>())
                col.enabled = false;
            FixPrefabMaterials(inst, fallback);
            placed++;
        }
    }

    // Cherche un point au sol qui ne tombe pas dans la serre
    private Vector3 RandomOutdoorPoint(out bool valid, float minDistFromCenter, float maxDistFromCenter)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(minDistFromCenter, maxDistFromCenter);
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            if (!IsInsideGreenhouse(pos, 1.5f))
            {
                valid = true;
                return pos;
            }
        }
        valid = false;
        return Vector3.zero;
    }

    private bool IsInsideGreenhouse(Vector3 pos, float margin)
    {
        return Mathf.Abs(pos.x) < greenhouseWidth / 2f + margin
            && Mathf.Abs(pos.z) < greenhouseLength / 2f + margin;
    }

    private bool IsOnEntrancePath(Vector3 pos, float halfWidth)
    {
        float startZ = greenhouseLength / 2f;
        float endZ = startZ + 24f;
        return pos.z > startZ && pos.z < endZ && Mathf.Abs(pos.x) < halfWidth;
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
        dirt.GetComponent<Renderer>().material = GetMaterial("Dirt", new Color(0.36f, 0.25f, 0.18f), 0f, 0.05f);
        dirt.isStatic = true;

        // Allée centrale en carrelage de pierre
        GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
        path.name = "Floor_StonePath";
        path.transform.parent = transform;
        path.transform.position = new Vector3(0, 0.01f, 0);
        path.transform.localScale = new Vector3(3f, 0.05f, greenhouseLength - 0.5f);
        path.GetComponent<Renderer>().material = GetMaterial("Stone", new Color(0.65f, 0.62f, 0.58f), 0.05f, 0.42f);
        path.isStatic = true;

        // NavMesh : on ajoute un NavMeshSurface au runtime via composant statique
        // Note : nécessite le package "AI Navigation" — voir instructions plus bas
    }

    // ─────────────────────────────────────────────
    // MURS : 4 murs transparents simulant le verre
    // ─────────────────────────────────────────────
    private void BuildWalls()
    {
        Material glass = GetTransparentMaterial("Glass", new Color(0.78f, 0.92f, 0.95f, 0.22f), 0.05f, 0.95f);

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
        door.GetComponent<Renderer>().material = GetMaterial("DoorWood", new Color(0.18f, 0.35f, 0.18f), 0f, 0.25f);
    }

    private void BuildWoodenFrame()
    {
        Material wood = GetMaterial("Wood", new Color(0.42f, 0.27f, 0.15f), 0f, 0.22f);
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
        Material glass = GetTransparentMaterial("RoofGlass", new Color(0.78f, 0.92f, 0.95f, 0.22f), 0.05f, 0.95f);
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
        // Tentative : charger un modèle FBX depuis Resources/Furniture/Table
        GameObject tablePrefab = Resources.Load<GameObject>("Furniture/Table");
        if (tablePrefab != null)
        {
            // 3ds Max exporte en Z-up : -90° sur X pour redresser
            centralTable = Instantiate(tablePrefab, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), transform);
            centralTable.name = "CentralTable";

            // Applique les textures à la main (le FBX référence souvent les textures par chemin absolu)
            Texture2D diffuse = Resources.Load<Texture2D>("Furniture/owt_diffuse");
            Texture2D bump = Resources.Load<Texture2D>("Furniture/owt_bump");
            Material woodMat = new Material(Shader.Find("Standard"));
            if (diffuse != null) woodMat.mainTexture = diffuse;
            if (bump != null)
            {
                woodMat.SetTexture("_BumpMap", bump);
                woodMat.EnableKeyword("_NORMALMAP");
            }
            woodMat.SetFloat("_Metallic", 0f);
            woodMat.SetFloat("_Glossiness", 0.25f);

            Renderer[] renderers = centralTable.GetComponentsInChildren<Renderer>();
            foreach (var rend in renderers) rend.sharedMaterial = woodMat;

            // Scale (world Y après rotation) pour faire environ 1 m de haut, puis repose la base à y=0
            if (renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);
                if (combined.size.y > 0.001f)
                {
                    const float targetHeight = 1.0f;
                    centralTable.transform.localScale = Vector3.one * (targetHeight / combined.size.y);
                }
                Bounds afterScale = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) afterScale.Encapsulate(renderers[i].bounds);
                centralTable.transform.position += new Vector3(0, -afterScale.min.y, 0);
            }
            return;
        }

        // Fallback procédural : plateau + 4 pieds
        centralTable = new GameObject("CentralTable");
        centralTable.transform.parent = transform;
        centralTable.transform.position = Vector3.zero;

        Material wood = GetMaterial("Wood", new Color(0.42f, 0.27f, 0.15f), 0f, 0.22f);

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
        Material clay = GetMaterial("Clay", new Color(0.55f, 0.27f, 0.15f), 0f, 0.30f);
        Material soil = GetMaterial("Soil", new Color(0.25f, 0.15f, 0.08f), 0f, 0.06f);

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
        if (potPrefab != null)
        {
            GameObject prefabPot = Instantiate(potPrefab, pos, Quaternion.identity, transform);
            prefabPot.name = name;
            prefabPot.tag = "Pot";
            if (prefabPot.GetComponent<PotInteraction>() == null)
                prefabPot.AddComponent<PotInteraction>();
            if (prefabPot.GetComponent<Collider>() == null)
            {
                CapsuleCollider cc = prefabPot.AddComponent<CapsuleCollider>();
                cc.radius = 0.5f; cc.height = 1f;
            }
            SphereCollider prefabTrigger = prefabPot.AddComponent<SphereCollider>();
            prefabTrigger.isTrigger = true;
            prefabTrigger.radius = 0.8f;
            return prefabPot;
        }

        // Le pot lui-même (primitives)
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

        Vector3 fertPos = new Vector3(-0.8f, tableY + 0.2f, 0);
        Vector3 pestPos = new Vector3(0.8f, tableY + 0.2f, 0);

        // Spray d'engrais
        if (fertilizerSprayPrefab != null)
        {
            fertilizerSpray = Instantiate(fertilizerSprayPrefab, fertPos, Quaternion.identity, transform);
            fertilizerSpray.name = "FertilizerSpray";
            fertilizerSpray.tag = "Pickup";
            EnsurePickup(fertilizerSpray, ItemType.FertilizerSpray);
        }
        else
        {
            fertilizerSpray = BuildSpray("FertilizerSpray", fertPos, new Color(0.2f, 0.5f, 0.9f), ItemType.FertilizerSpray);
        }

        // Spray anti-nuisibles
        if (pestSprayPrefab != null)
        {
            pestSpray = Instantiate(pestSprayPrefab, pestPos, Quaternion.identity, transform);
            pestSpray.name = "PestSpray";
            pestSpray.tag = "Pickup";
            EnsurePickup(pestSpray, ItemType.PestSpray);
        }
        else
        {
            pestSpray = BuildSpray("PestSpray", pestPos, new Color(0.85f, 0.15f, 0.15f), ItemType.PestSpray);
        }

        // Première graine
        Vector3 seedPos = new Vector3(0, tableY + 0.15f, 0.4f);
        if (seedPrefab != null)
        {
            startingSeed = Instantiate(seedPrefab, seedPos, Quaternion.identity, transform);
            startingSeed.name = "Seed_Drosera";
            startingSeed.tag = "Pickup";
            EnsurePickup(startingSeed, ItemType.Seed, 0);
        }
        else
        {
            startingSeed = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            startingSeed.name = "Seed_Drosera";
            startingSeed.tag = "Pickup";
            startingSeed.transform.parent = transform;
            startingSeed.transform.position = seedPos;
            startingSeed.transform.localScale = Vector3.one * 0.15f;
            startingSeed.GetComponent<Renderer>().material = GetMaterial("Seed1", new Color(0.6f, 0.8f, 0.2f), 0f, 0.35f);
            Rigidbody rb2 = startingSeed.AddComponent<Rigidbody>();
            rb2.isKinematic = true;
            ItemPickup pu = startingSeed.AddComponent<ItemPickup>();
            pu.itemType = ItemType.Seed;
            pu.seedIndex = 0;
        }
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
        body.GetComponent<Renderer>().material = GetMaterial(name + "_Body", tint, 0.05f, 0.55f);

        // Embout / gâchette (petit cube)
        GameObject trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trigger.name = "Trigger";
        trigger.transform.parent = root.transform;
        trigger.transform.localPosition = new Vector3(0.1f, 0.2f, 0);
        trigger.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
        trigger.GetComponent<Renderer>().material = GetMaterial("SprayTrigger", new Color(0.9f, 0.9f, 0.9f), 0.1f, 0.45f);

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
        Vector3 pos = new Vector3(-greenhouseWidth / 2f + 1f, 0.3f, greenhouseLength / 2f - 2f);

        if (soilPilePrefab != null)
        {
            soilPile = Instantiate(soilPilePrefab, pos, Quaternion.identity, transform);
            soilPile.name = "SoilPile";
            soilPile.tag = "SoilPile";
            if (soilPile.GetComponent<Collider>() == null)
            {
                SphereCollider sc = soilPile.AddComponent<SphereCollider>();
                sc.isTrigger = true; sc.radius = 0.8f;
            }
            return;
        }

        soilPile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        soilPile.name = "SoilPile";
        soilPile.tag = "SoilPile";
        soilPile.transform.parent = transform;
        soilPile.transform.position = pos;
        soilPile.transform.localScale = new Vector3(1.2f, 0.6f, 1.2f);
        soilPile.GetComponent<Renderer>().material = GetMaterial("Soil", new Color(0.25f, 0.15f, 0.08f), 0f, 0.06f);

        SphereCollider trig = soilPile.AddComponent<SphereCollider>();
        trig.isTrigger = true;
        trig.radius = 0.8f;
    }

    // ─────────────────────────────────────────────
    // ARROSOIR : cylindre + bec en capsule + anse
    // ─────────────────────────────────────────────
    private void BuildWateringCan()
    {
        Vector3 pos = new Vector3(greenhouseWidth / 2f - 1f, 0.4f, greenhouseLength / 2f - 2f);

        if (wateringCanPrefab != null)
        {
            wateringCan = Instantiate(wateringCanPrefab, pos, Quaternion.identity, transform);
            wateringCan.name = "WateringCan";
            wateringCan.tag = "Pickup";
            EnsurePickup(wateringCan, ItemType.WateringCan);
            return;
        }

        wateringCan = new GameObject("WateringCan");
        wateringCan.transform.parent = transform;
        wateringCan.transform.position = pos;
        wateringCan.tag = "Pickup";

        Material metal = GetMaterial("Metal", new Color(0.72f, 0.78f, 0.82f), 0.9f, 0.78f);

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
    // HELPER : s'assure qu'un prefab instancié a bien le bon ItemPickup + Rigidbody
    // ─────────────────────────────────────────────
    private void EnsurePickup(GameObject go, ItemType type, int seedIdx = -1)
    {
        if (go.GetComponent<Collider>() == null)
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = Vector3.one * 0.6f;
        }
        if (go.GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }
        ItemPickup existing = go.GetComponent<ItemPickup>();
        if (existing == null) existing = go.AddComponent<ItemPickup>();
        existing.itemType = type;
        if (seedIdx >= 0) existing.seedIndex = seedIdx;
    }

    // ─────────────────────────────────────────────
    // HELPERS MATÉRIAUX (PBR — shader Standard)
    // ─────────────────────────────────────────────
    private Material GetMaterial(string key, Color c) => GetMaterial(key, c, 0f, 0.2f);

    private Material GetMaterial(string key, Color c, float metallic, float smoothness)
    {
        if (materialCache.TryGetValue(key, out Material m)) return m;
        m = new Material(Shader.Find("Standard"));
        m.color = c;
        m.SetFloat("_Metallic", Mathf.Clamp01(metallic));
        m.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));
        materialCache[key] = m;
        return m;
    }

    private Material GetTransparentMaterial(string key, Color c) => GetTransparentMaterial(key, c, 0f, 0.85f);

    private Material GetTransparentMaterial(string key, Color c, float metallic, float smoothness)
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
        m.SetFloat("_Metallic", Mathf.Clamp01(metallic));
        m.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));
        materialCache[key] = m;
        return m;
    }
}
