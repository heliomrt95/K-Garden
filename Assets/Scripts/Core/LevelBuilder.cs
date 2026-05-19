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
    public float greenhouseWidth = 8f;
    public float greenhouseLength = 12f;
    public float greenhouseHeight = 3.5f;
    public float wallThickness = 0.2f;

    [Header("Pots")]
    public int potsPerSide = 3;
    public float potSpacing = 2.5f;

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
    [HideInInspector] public List<GameObject> planterBoxes = new List<GameObject>();
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

        // Si un modèle de serre custom (FBX/DAE/OBJ exporté depuis SketchUp Free,
        // Blender, etc.) est présent dans Resources/Greenhouse/, on l'utilise à la
        // place des murs/toit/cadre procéduraux. Le sol reste procédural.
        // Unity traite .fbx/.dae/.obj comme des GameObject importés par leur nom
        // sans extension : on essaie les noms usuels.
        GameObject greenhouseFbx =
            Resources.Load<GameObject>("Greenhouse/Greenhouse")
            ?? Resources.Load<GameObject>("Greenhouse/greenhouse")
            ?? Resources.Load<GameObject>("Greenhouse/Serre")
            ?? Resources.Load<GameObject>("Greenhouse/serre");
        // Sol intérieur retiré : on garde le terrain extérieur (herbe) visible
        // sous la serre. La dalle béton de fondation est posée par BuildArchedGreenhouse.
        if (greenhouseFbx != null)
            InstantiateCustomGreenhouse(greenhouseFbx);
        else
            BuildArchedGreenhouse();

        BuildCentralTable();
        BuildPlanterBoxes();
        BuildIndoorPath();
        BuildPots();
        BuildStartingItems();
        BuildSoilPile();
        BuildWateringCan();
        BuildDecorPlants();
        SetupLighting();

        // Patch Green Lawn 3D abandonné — les billboards Grass1-4 donnent
        // déjà l'effet "champ" voulu, plus léger en perf.
        GameObject[] grassBlades = LoadPrefabs(
            "Tools/Grass1", "Tools/Grass2", "Tools/Grass3", "Tools/Grass4");
        ScatterIndoorGrass(grassBlades, 15000);
    }

    // Pose des patches de pelouse 3D détaillée à l'intérieur de la serre
    private void BuildIndoorLawn(GameObject patchPrefab)
    {
        // Debug : log les bounds du mesh tel qu'Unity le voit
        MeshFilter mf = patchPrefab.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Debug.Log($"[Lawn] mesh.bounds={mf.sharedMesh.bounds.size}, mesh.name={mf.sharedMesh.name}");
        }

        const float patchSize = 4f;
        // Hardcode : le mesh source mesure ~214m sur sa plus grande dimension.
        // Scale 0.02 donne ~4.3m. Vector3 non-uniforme pour ajuster la hauteur.
        Vector3 lawnScale = new Vector3(0.02f, 0.04f, 0.02f);

        int patchesX = Mathf.CeilToInt(greenhouseWidth / patchSize);
        int patchesZ = Mathf.CeilToInt(greenhouseLength / patchSize);
        float stepX = greenhouseWidth / patchesX;
        float stepZ = greenhouseLength / patchesZ;

        Material lawnMat = GetLawnPatchMaterial();

        for (int ix = 0; ix < patchesX; ix++)
        {
            for (int iz = 0; iz < patchesZ; iz++)
            {
                float x = -greenhouseWidth / 2f + stepX / 2f + ix * stepX;
                float z = -greenhouseLength / 2f + stepZ / 2f + iz * stepZ;

                GameObject patch = Instantiate(patchPrefab,
                    new Vector3(x, GroundY, z),
                    Quaternion.Euler(-90f, Random.Range(0f, 360f), 0f),
                    transform);
                patch.name = $"LawnPatch_{ix}_{iz}";
                patch.transform.localScale = lawnScale;

                foreach (var r in patch.GetComponentsInChildren<Renderer>())
                {
                    if (lawnMat != null) r.sharedMaterial = lawnMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                foreach (var col in patch.GetComponentsInChildren<Collider>())
                    col.enabled = false;
            }
        }
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
    private const float GroundY = -0.2f;      // niveau du sol extérieur (Plane)

    private Transform outdoorRoot;

    // Cache des matériaux convertis URP → Standard (pour éviter de recréer N fois le même)
    private Dictionary<Material, Material> fixedMaterialCache = new Dictionary<Material, Material>();

    // Génère un matériau de sol vert procédural avec une texture noise Perlin
    // pour donner des variations naturelles de teinte (pas un sol plat uniforme).
    private Material TryBuildPBRGroundMaterial()
    {
        Shader std = Shader.Find("Standard");
        if (std == null) return null;

        Texture2D tex = GenerateGrassNoiseTexture(512);
        Material mat = new Material(std);
        mat.name = "GrassProcedural";
        mat.mainTexture = tex;
        mat.color = Color.white;
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.05f);

        // Tiling : 8 répétitions sur 200 m → chaque tuile = 25 m,
        // assez grand pour que la répétition soit invisible mais détails restent nets
        const float tiles = 8f;
        mat.mainTextureScale = new Vector2(tiles, tiles);

        return mat;
    }

    // Crée une texture procédurale d'herbe : multi-octave Perlin noise + variation de teinte
    private Texture2D GenerateGrassNoiseTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 16;

        Color[] pixels = new Color[size * size];

        // 3 nuances pour mélanger : vert clair, vert moyen, vert foncé / brun
        Color cBright = new Color(0.48f, 0.62f, 0.28f);
        Color cMid    = new Color(0.30f, 0.50f, 0.22f);
        Color cDark   = new Color(0.18f, 0.32f, 0.12f);
        Color cEarth  = new Color(0.32f, 0.26f, 0.16f);

        float offX = Random.Range(0f, 1000f);
        float offY = Random.Range(0f, 1000f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Multi-octave Perlin pour variation organique
                float n1 = Mathf.PerlinNoise((x * 0.012f) + offX, (y * 0.012f) + offY);
                float n2 = Mathf.PerlinNoise((x * 0.08f) + offX, (y * 0.08f) + offY) * 0.5f;
                float n3 = Mathf.PerlinNoise((x * 0.35f) + offX, (y * 0.35f) + offY) * 0.25f;
                float n = (n1 + n2 + n3) / 1.75f; // [0, 1]

                // Touches de terre éparses (5 % de la surface)
                float earthMask = Mathf.PerlinNoise((x * 0.005f) + offX + 500f, (y * 0.005f) + offY);
                bool isEarth = earthMask > 0.78f && n1 < 0.4f;

                Color c;
                if (isEarth)
                {
                    c = Color.Lerp(cEarth, cDark, n2);
                }
                else if (n < 0.35f)
                {
                    c = Color.Lerp(cDark, cMid, n / 0.35f);
                }
                else
                {
                    c = Color.Lerp(cMid, cBright, (n - 0.35f) / 0.65f);
                }

                // Petite variation pixel par pixel pour casser l'aspect lissé
                float jitter = Random.Range(-0.04f, 0.04f);
                c.r = Mathf.Clamp01(c.r + jitter);
                c.g = Mathf.Clamp01(c.g + jitter);
                c.b = Mathf.Clamp01(c.b + jitter);

                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true); // génère mipmaps
        return tex;
    }

    // Matériau partagé pour le patch de pelouse 3D (Standard opaque, double-face)
    private Material lawnPatchMaterial;
    private Material GetLawnPatchMaterial()
    {
        if (lawnPatchMaterial != null) return lawnPatchMaterial;
        Shader std = Shader.Find("Standard");
        if (std == null) return null;
        lawnPatchMaterial = new Material(std);
        lawnPatchMaterial.name = "LawnPatchMat";
        // Couleur diffuse depuis le MTL d'origine (vert-jaune naturel)
        lawnPatchMaterial.color = new Color(0.44f, 0.53f, 0.20f);
        lawnPatchMaterial.SetFloat("_Metallic", 0f);
        lawnPatchMaterial.SetFloat("_Glossiness", 0.05f);
        // Désactive le culling : normales potentiellement inversées par 3DS Max
        if (lawnPatchMaterial.HasProperty("_Cull"))
            lawnPatchMaterial.SetFloat("_Cull", 0f);
        lawnPatchMaterial.enableInstancing = true;
        return lawnPatchMaterial;
    }

    // Matériau partagé pour les brins d'herbe (Standard Cutout + double-face + instancing)
    private Material grassBladeMaterial;
    private Material GetGrassBladeMaterial()
    {
        if (grassBladeMaterial != null) return grassBladeMaterial;
        Shader std = Shader.Find("Standard");
        if (std == null) return null;

        Texture2D tex = Resources.Load<Texture2D>("Textures/Grass1")
                        ?? Resources.Load<Texture2D>("Textures/Grass2");

        grassBladeMaterial = new Material(std);
        grassBladeMaterial.name = "GrassBladeMat";
        if (tex != null) grassBladeMaterial.mainTexture = tex;
        grassBladeMaterial.color = new Color(0.7f, 1.0f, 0.55f); // léger vert
        // Mode Cutout (alpha testé)
        grassBladeMaterial.SetFloat("_Mode", 1);
        grassBladeMaterial.SetOverrideTag("RenderType", "TransparentCutout");
        grassBladeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        grassBladeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        grassBladeMaterial.SetInt("_ZWrite", 1);
        grassBladeMaterial.EnableKeyword("_ALPHATEST_ON");
        grassBladeMaterial.DisableKeyword("_ALPHABLEND_ON");
        grassBladeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        grassBladeMaterial.renderQueue = 2450;
        grassBladeMaterial.SetFloat("_Cutoff", 0.4f);
        // Désactive le culling pour voir les deux côtés des quads
        if (grassBladeMaterial.HasProperty("_Cull"))
            grassBladeMaterial.SetFloat("_Cull", 0f);
        grassBladeMaterial.SetFloat("_Metallic", 0f);
        grassBladeMaterial.SetFloat("_Glossiness", 0.05f);
        // GPU instancing pour rendre des milliers de brins efficacement
        grassBladeMaterial.enableInstancing = true;
        return grassBladeMaterial;
    }

    // Applique le matériau d'herbe partagé sur tous les renderers du brin
    private void ApplyGrassBladeMaterial(GameObject blade)
    {
        Material mat = GetGrassBladeMaterial();
        if (mat == null) return;
        foreach (var rend in blade.GetComponentsInChildren<Renderer>())
            rend.sharedMaterial = mat;
    }

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

        // Quad au lieu de Plane (Plane = 10×10 quads → 121 vertex gizmos visibles).
        // Quad = 4 vertices uniquement, rotation -90°X pour orienter face vers le haut.
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ground.name = "OutdoorGround";
        ground.transform.parent = outdoorRoot;
        ground.transform.position = new Vector3(0, GroundY, 0);
        ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        ground.transform.localScale = new Vector3(MapSize, MapSize, 1f);
        ground.isStatic = true;

        // Sol : si un set PBR "AddWater" est présent (Resources/Textures/Ground/),
        // on l'utilise. Sinon on retombe sur un vert procédural uniforme.
        Material grass = TryBuildPBRGroundMaterial();
        if (grass == null)
            grass = GetMaterial("Grass", new Color(0.32f, 0.52f, 0.23f), 0f, 0.02f);
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
            path.transform.position = new Vector3(0, GroundY + 0.02f, greenhouseLength / 2f + 12f);
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
                new Vector3(x, GroundY + 0.02f, z),
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

            Vector3 pos = new Vector3(Mathf.Cos(a) * fenceRadius, GroundY, Mathf.Sin(a) * fenceRadius);
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

        // Touffes Pure Poly (petites) + brins d'herbe billboards du grass.rar
        GameObject[] grass = LoadPrefabs(
            "Decorations/PP_Grass_11",
            "Decorations/PP_Grass_15");
        GameObject[] grassBlades = LoadPrefabs(
            "Tools/Grass1",
            "Tools/Grass2",
            "Tools/Grass3",
            "Tools/Grass4");

        // PP_Meadow_07/08 et PP_Forest_Mountain_Moss_* sont en réalité de grosses
        // collines/montagnes (malgré leur nom). On les utilise uniquement comme
        // toile de fond très lointaine, pas comme décor de sol.
        GameObject[] mountains = LoadPrefabs(
            "Decorations/PP_Forest_Mountain_Moss_01",
            "Decorations/PP_Forest_Mountain_Moss_02",
            "Decorations/PP_Meadow_07",
            "Decorations/PP_Meadow_08");

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

        // ── GROS ROCHERS — TRÈS LOIN, à l'horizon ──
        ScatterPrefabs(rocks, 20, 110f, MapHalf - 5f, 0.25f, 0.5f, rockGray);
        // Petits rochers / cailloux : éparpillés au sol
        ScatterPrefabs(pebbles, 60, ghHalf + 8f, 70f, 0.12f, 0.25f, rockGray);

        // ── MONTAGNES LOINTAINES — placées contre le bord de la map ──
        ScatterPrefabs(mountains, 18, 130f, MapHalf - 5f, 0.6f, 1.1f, mossGreen);

        // ── SOUS-BOIS — uniquement de petits éléments de sol près de la serre ──
        ScatterPrefabs(flowers, 140, ghHalf + 3f, 40f, 0.35f, 0.6f, flowerYellow);
        ScatterPrefabs(grass, 280, ghHalf + 2f, 70f, 0.3f, 0.55f, grassGreen);
        ScatterPrefabs(mushrooms, 70, ghHalf + 10f, 75f, 0.3f, 0.5f, mushroomOrange);

        // ── BRINS D'HERBE 3D (CombineMeshes : 25k+ brins en quelques draw calls) ──
        ScatterGrassBlades(grassBlades, 12000, ghHalf + 1f, 25f, 0.08f, 0.20f, allowInside: false);
        ScatterGrassBlades(grassBlades, 10000, 25f, 60f, 0.10f, 0.25f, allowInside: false);
        ScatterGrassBlades(grassBlades, 6000, 60f, 90f, 0.12f, 0.30f, allowInside: false);
        // (L'herbe intérieure est ajoutée à la fin de BuildLevel, une fois les
        // pots/table/arrosoir instanciés pour pouvoir les éviter.)
    }

    // Récupère les meshes sources des prefabs Grass (pour CombineMeshes)
    private Mesh[] GetGrassSourceMeshes(GameObject[] prefabs)
    {
        var list = new List<Mesh>(prefabs.Length);
        foreach (var p in prefabs)
        {
            if (p == null) continue;
            var mf = p.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) list.Add(mf.sharedMesh);
        }
        return list.ToArray();
    }

    // Combine plusieurs CombineInstance en chunks de meshes (chaque chunk = 1 GameObject + 1 draw call)
    private void BuildCombinedGrassMeshes(List<CombineInstance> instances, Transform parent, string baseName)
    {
        if (instances.Count == 0) return;
        Material mat = GetGrassBladeMaterial();
        const int chunkSize = 5000;
        for (int start = 0; start < instances.Count; start += chunkSize)
        {
            int end = Mathf.Min(start + chunkSize, instances.Count);
            CombineInstance[] chunk = new CombineInstance[end - start];
            for (int i = 0; i < chunk.Length; i++) chunk[i] = instances[start + i];

            Mesh combined = new Mesh();
            combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combined.CombineMeshes(chunk, true, true);

            GameObject go = new GameObject($"{baseName}_{start / chunkSize}");
            go.transform.parent = parent;
            go.AddComponent<MeshFilter>().sharedMesh = combined;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }

    // Scatter optimisé : génère N positions valides puis combine en quelques meshes
    private void ScatterGrassBlades(GameObject[] prefabs, int count, float minRadius, float maxRadius,
                                    float minScale, float maxScale, bool allowInside)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        Mesh[] sources = GetGrassSourceMeshes(prefabs);
        if (sources.Length == 0) return;

        var instances = new List<CombineInstance>(count);
        int placed = 0;
        int safety = count * 3;
        while (placed < count && safety-- > 0)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(minRadius, maxRadius);
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, GroundY, Mathf.Sin(angle) * radius);
            if (!allowInside && IsInsideGreenhouse(pos, 3f)) continue;
            if (IsOnEntrancePath(pos, 2.5f)) continue;

            Matrix4x4 m = Matrix4x4.TRS(
                pos,
                Quaternion.Euler(0, Random.Range(0f, 360f), 0),
                Vector3.one * Random.Range(minScale, maxScale));
            instances.Add(new CombineInstance { mesh = sources[Random.Range(0, sources.Length)], transform = m });
            placed++;
        }
        BuildCombinedGrassMeshes(instances, outdoorRoot, "GrassOutdoor");
    }

    // Place des brins d'herbe à l'intérieur de la serre, en évitant les meubles/pots
    // Utilise CombineMeshes pour rester performant même à très haute densité
    private void ScatterIndoorGrass(GameObject[] grassBlades, int count)
    {
        if (grassBlades == null || grassBlades.Length == 0) return;
        Mesh[] sources = GetGrassSourceMeshes(grassBlades);
        if (sources.Length == 0) return;

        float marginX = greenhouseWidth / 2f - 0.3f;
        float marginZ = greenhouseLength / 2f - 0.3f;
        int placed = 0;
        int safety = count * 4;
        var instances = new List<CombineInstance>(count);

        while (placed < count && safety-- > 0)
        {
            Vector3 pos = new Vector3(
                Random.Range(-marginX, marginX),
                GroundY,
                Random.Range(-marginZ, marginZ));

            // Évite uniquement le dessous de la table (pieds) — rectangle 1.6 × 0.9
            if (Mathf.Abs(pos.x) < 1.6f && Mathf.Abs(pos.z) < 0.9f) continue;

            // Évite les pots de très près (35 cm)
            bool tooClose = false;
            foreach (var pot in pots)
            {
                if (pot == null) continue;
                float dx = pos.x - pot.transform.position.x;
                float dz = pos.z - pot.transform.position.z;
                if (dx * dx + dz * dz < 0.35f * 0.35f) { tooClose = true; break; }
            }
            if (tooClose) continue;

            if (wateringCan != null)
            {
                Vector3 wp = wateringCan.transform.position;
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(wp.x, wp.z)) < 0.35f) continue;
            }
            if (soilPile != null)
            {
                Vector3 sp = soilPile.transform.position;
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(sp.x, sp.z)) < 0.55f) continue;
            }

            // Évite les bacs à plantations (rectangle 2m × 1m autour de chaque bac)
            bool nearBox = false;
            foreach (var box in planterBoxes)
            {
                if (box == null) continue;
                Vector3 bp = box.transform.position;
                if (Mathf.Abs(pos.x - bp.x) < 0.55f && Mathf.Abs(pos.z - bp.z) < 1.05f) { nearBox = true; break; }
            }
            if (nearBox) continue;

            Matrix4x4 m = Matrix4x4.TRS(
                pos,
                Quaternion.Euler(0, Random.Range(0f, 360f), 0),
                Vector3.one * Random.Range(0.08f, 0.16f));
            instances.Add(new CombineInstance { mesh = sources[Random.Range(0, sources.Length)], transform = m });
            placed++;
        }

        BuildCombinedGrassMeshes(instances, transform, "GrassIndoor");
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
            Vector3 center = new Vector3(Mathf.Cos(angle) * radius, GroundY, Mathf.Sin(angle) * radius);
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
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, GroundY, Mathf.Sin(angle) * radius);
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
    // SERRE TUNNEL : structure en arches metalliques + verre courbe
    // ─────────────────────────────────────────────
    private void BuildArchedGreenhouse()
    {
        float Rx = greenhouseWidth / 2f;     // demi-largeur
        float Ry = greenhouseHeight;          // hauteur de la voûte
        float L = greenhouseLength;

        Material steel = GetMaterial("Steel", new Color(0.78f, 0.80f, 0.82f), 0.85f, 0.65f);
        // Bâche translucide laiteuse pour la voûte (un seul "plan" de panneaux courbes)
        Material glass = GetTransparentMaterial("ArchTarp", new Color(0.94f, 0.96f, 0.91f, 0.94f), 0f, 0.08f);
        // Pignon : matériau OPAQUE pour éviter les artefacts de superposition d'alpha
        // (les tranches verticales se superposent sinon et créent une teinte jaune)
        Material gableMat = GetMaterial("GableTarp", new Color(0.94f, 0.96f, 0.91f), 0f, 0.08f);
        Material concrete = GetMaterial("Concrete", new Color(0.66f, 0.66f, 0.63f), 0f, 0.15f);
        Material doorMat = GetMaterial("DoorMetal", new Color(0.45f, 0.48f, 0.50f), 0.7f, 0.5f);

        GameObject ghRoot = new GameObject("ArchedGreenhouse");
        ghRoot.transform.parent = transform;
        // Maintenant que les enfants utilisent localPosition, ghRoot.localPosition
        // décale réellement toute la structure. Base des arches pile sur l'herbe.
        ghRoot.transform.localPosition = new Vector3(0, GroundY, 0);

        // Dalle béton retirée : on garde de l'herbe sous la serre.
        // (Variable concrete conservée au cas où on rajouterait un cadre de fondation)
        _ = concrete;

        // ── Arches transversales ──
        const int archCount = 8;
        const int segmentsPerArch = 14;
        float archSpacing = L / (archCount - 1);
        float archThickness = 0.12f;
        for (int a = 0; a < archCount; a++)
        {
            float z = -L / 2f + a * archSpacing;
            BuildArchRib(ghRoot.transform, Rx, Ry, z, segmentsPerArch, archThickness, steel);
        }

        // ── Longerons : poutres longitudinales le long de l'arche ──
        const int longeronCount = 6;
        float beamThickness = 0.08f;
        for (int l = 0; l <= longeronCount; l++)
        {
            float theta = Mathf.PI * l / longeronCount; // 0 .. π
            float x = -Mathf.Cos(theta) * Rx;
            float y = Mathf.Sin(theta) * Ry;

            GameObject longeron = GameObject.CreatePrimitive(PrimitiveType.Cube);
            longeron.name = $"Longeron_{l}";
            longeron.transform.parent = ghRoot.transform;
            longeron.transform.localPosition = new Vector3(x, y, 0);
            longeron.transform.localScale = new Vector3(beamThickness, beamThickness, L);
            longeron.GetComponent<Renderer>().sharedMaterial = steel;
        }

        // ── Panneaux de verre entre les arches (voûte semi-cylindrique) ──
        for (int a = 0; a < archCount - 1; a++)
        {
            float zMid = -L / 2f + a * archSpacing + archSpacing / 2f;
            BuildArchGlassRow(ghRoot.transform, Rx, Ry, zMid, archSpacing, segmentsPerArch, glass);
        }

        // ── Pignons : porte sur +Z, fermé sur -Z ──
        // On utilise gableMat (opaque) pour les tranches du pignon afin d'éviter
        // les artefacts d'alpha qui rendaient le pignon visiblement plus jaune
        BuildGable(ghRoot.transform, Rx, Ry, +L / 2f + 0.02f, gableMat, steel, doorMat, true);
        BuildGable(ghRoot.transform, Rx, Ry, -L / 2f - 0.02f, gableMat, steel, doorMat, false);
    }

    // Un arc en demi-ellipse : N petits cubes orientés tangents à la courbe
    private void BuildArchRib(Transform parent, float Rx, float Ry, float z, int segments,
                              float thickness, Material steel)
    {
        for (int s = 0; s < segments; s++)
        {
            float t0 = (float)s / segments;
            float t1 = (float)(s + 1) / segments;
            float theta0 = Mathf.PI * t0;
            float theta1 = Mathf.PI * t1;

            Vector3 p0 = new Vector3(-Mathf.Cos(theta0) * Rx, Mathf.Sin(theta0) * Ry, z);
            Vector3 p1 = new Vector3(-Mathf.Cos(theta1) * Rx, Mathf.Sin(theta1) * Ry, z);
            Vector3 mid = (p0 + p1) / 2f;
            Vector3 dir = p1 - p0;
            float len = dir.magnitude;

            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = $"ArchSeg_{s}";
            seg.transform.parent = parent;
            seg.transform.localPosition = mid;
            seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            seg.transform.localScale = new Vector3(thickness, len * 1.02f, thickness);
            seg.GetComponent<Renderer>().sharedMaterial = steel;
        }
    }

    // Une rangée de N panneaux de verre courbes entre deux arches (sur largeur archSpacing)
    private void BuildArchGlassRow(Transform parent, float Rx, float Ry, float zMid, float length,
                                   int segments, Material glass)
    {
        for (int s = 0; s < segments; s++)
        {
            float t0 = (float)s / segments;
            float t1 = (float)(s + 1) / segments;
            float theta0 = Mathf.PI * t0;
            float theta1 = Mathf.PI * t1;

            Vector3 p0 = new Vector3(-Mathf.Cos(theta0) * Rx, Mathf.Sin(theta0) * Ry, 0);
            Vector3 p1 = new Vector3(-Mathf.Cos(theta1) * Rx, Mathf.Sin(theta1) * Ry, 0);
            Vector3 dir = p1 - p0;
            float arcLen = dir.magnitude;
            Vector3 mid = new Vector3((p0.x + p1.x) / 2f, (p0.y + p1.y) / 2f, zMid);

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = $"GlassPanel_{s}";
            panel.transform.parent = parent;
            panel.transform.localPosition = mid;
            panel.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            panel.transform.localScale = new Vector3(0.025f, arcLen * 0.99f, length * 0.96f);
            panel.GetComponent<Renderer>().sharedMaterial = glass;
        }
    }

    // Pignon en demi-ellipse : tranches verticales remplissant la forme,
    // découpées pour laisser passer la porte si hasDoor=true. + arc structurel + X braces.
    private void BuildGable(Transform parent, float Rx, float Ry, float z,
                            Material glass, Material steel, Material doorMat, bool hasDoor)
    {
        const int slices = 22;
        float sliceWidth = (Rx * 2f) / slices;
        const float doorWidth = 2f;
        const float doorHeight = 2.4f;

        for (int i = 0; i < slices; i++)
        {
            float x0 = -Rx + i * sliceWidth;
            float xMid = x0 + sliceWidth / 2f;

            float t = xMid / Rx;
            if (Mathf.Abs(t) >= 1f) continue;
            float h = Ry * Mathf.Sqrt(1f - t * t);
            if (h < 0.05f) continue;

            bool inDoor = hasDoor && Mathf.Abs(xMid) < doorWidth / 2f;
            if (inDoor)
            {
                if (h > doorHeight)
                {
                    float panelH = h - doorHeight;
                    BuildGablePanel(parent, xMid, doorHeight + panelH / 2f, z,
                                    sliceWidth * 1.02f, panelH, glass);
                }
            }
            else
            {
                BuildGablePanel(parent, xMid, h / 2f, z, sliceWidth * 1.02f, h, glass);
            }
        }

        // Arc structurel du pignon (plus épais)
        BuildArchRib(parent, Rx, Ry, z, 18, 0.16f, steel);

        // Linteau horizontal au-dessus de la porte
        if (hasDoor)
        {
            GameObject lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "Lintel";
            lintel.transform.parent = parent;
            lintel.transform.localPosition = new Vector3(0, doorHeight, z);
            lintel.transform.localScale = new Vector3(doorWidth + 0.2f, 0.1f, 0.1f);
            lintel.GetComponent<Renderer>().sharedMaterial = steel;

            // Porte
            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Door";
            door.tag = "Door";
            door.transform.parent = parent;
            door.transform.localPosition = new Vector3(0, doorHeight / 2f, z);
            door.transform.localScale = new Vector3(doorWidth, doorHeight, 0.06f);
            door.GetComponent<Renderer>().sharedMaterial = doorMat;
        }

        // X braces retirés — ils créaient des lignes sombres visibles à l'intérieur
        // et accentuaient la différence d'aspect entre pignon et voûte.
    }

    private void BuildGablePanel(Transform parent, float x, float y, float z,
                                 float width, float height, Material glass)
    {
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "GableSlice";
        panel.transform.parent = parent;
        panel.transform.localPosition = new Vector3(x, y, z);
        panel.transform.localScale = new Vector3(width, height, 0.04f);
        panel.GetComponent<Renderer>().sharedMaterial = glass;
    }

    private void BuildXBraces(Transform parent, float Rx, float Ry, float z, Material steel)
    {
        const float t = 0.06f;
        float topY = Ry * 0.55f;

        // X gauche
        BuildBrace(parent, new Vector3(-Rx * 0.85f, 0.1f, z), new Vector3(-Rx * 0.35f, topY, z), t, steel);
        BuildBrace(parent, new Vector3(-Rx * 0.85f, topY, z), new Vector3(-Rx * 0.35f, 0.1f, z), t, steel);
        // X droit
        BuildBrace(parent, new Vector3(Rx * 0.35f, 0.1f, z), new Vector3(Rx * 0.85f, topY, z), t, steel);
        BuildBrace(parent, new Vector3(Rx * 0.35f, topY, z), new Vector3(Rx * 0.85f, 0.1f, z), t, steel);
    }

    private void BuildBrace(Transform parent, Vector3 p0, Vector3 p1, float thickness, Material mat)
    {
        Vector3 mid = (p0 + p1) / 2f;
        Vector3 dir = p1 - p0;
        float len = dir.magnitude;

        GameObject brace = GameObject.CreatePrimitive(PrimitiveType.Cube);
        brace.name = "Brace";
        brace.transform.parent = parent;
        brace.transform.localPosition = mid;
        brace.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        brace.transform.localScale = new Vector3(thickness, len, thickness);
        brace.GetComponent<Renderer>().sharedMaterial = mat;
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
                centralTable.transform.position += new Vector3(0, -afterScale.min.y + GroundY, 0);

                // Collider englobant pour empêcher le joueur de traverser la table
                Bounds finalBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) finalBounds.Encapsulate(renderers[i].bounds);
                BoxCollider bc = centralTable.AddComponent<BoxCollider>();
                bc.center = centralTable.transform.InverseTransformPoint(finalBounds.center);
                Vector3 worldSize = finalBounds.size;
                Vector3 ls = centralTable.transform.lossyScale;
                bc.size = new Vector3(worldSize.x / ls.x, worldSize.y / ls.y, worldSize.z / ls.z);
            }
            return;
        }

        // Fallback procédural : plateau + 4 pieds
        centralTable = new GameObject("CentralTable");
        centralTable.transform.parent = transform;
        centralTable.transform.position = new Vector3(0, GroundY, 0);

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

        // Collider englobant pour empêcher le joueur de traverser la table
        BoxCollider tableCol = centralTable.AddComponent<BoxCollider>();
        tableCol.center = new Vector3(0, legH / 2f, 0);
        tableCol.size = new Vector3(topW, legH + topH, topL);
    }

    // ─────────────────────────────────────────────
    // BACS À PLANTATIONS : 4 par côté, symétriques le long de la serre
    // ─────────────────────────────────────────────
    private void BuildPlanterBoxes()
    {
        GameObject prefab = Resources.Load<GameObject>("Tools/PlanterBox");
        if (prefab == null) return;

        const int boxesPerSide = 4;
        const float boxTargetLength = 2f;  // longueur cible le long de Z
        const float wallClearance = 0.2f;  // espace entre bac et mur

        // Calcule la largeur du bac après scale (le PlanterBox FBX a sa "longueur" sur X)
        // Après auto-scale à 2m sur dim max, la largeur (Z originale = 0.3m) devient ~1m
        float boxHalfWidth = 0.5f; // approximation, ajusté avec wallClearance
        float xLeft = -greenhouseWidth / 2f + boxHalfWidth + wallClearance;
        float xRight = greenhouseWidth / 2f - boxHalfWidth - wallClearance;

        float marginZ = 0.6f;
        float zStart = -greenhouseLength / 2f + marginZ + boxTargetLength / 2f;
        float zEnd = greenhouseLength / 2f - marginZ - boxTargetLength / 2f;

        for (int i = 0; i < boxesPerSide; i++)
        {
            float t = (boxesPerSide == 1) ? 0.5f : (float)i / (boxesPerSide - 1);
            float z = Mathf.Lerp(zStart, zEnd, t);
            SpawnPlanterBox(prefab, new Vector3(xLeft, GroundY, z), boxTargetLength);
            SpawnPlanterBox(prefab, new Vector3(xRight, GroundY, z), boxTargetLength);
        }
    }

    private void SpawnPlanterBox(GameObject prefab, Vector3 pos, float targetLength)
    {
        // Rotation 90° Y pour que la longueur du bac soit alignée sur l'axe Z (longueur de la serre)
        GameObject box = Instantiate(prefab, pos, Quaternion.Euler(0, 90f, 0), transform);
        box.name = "PlanterBox";
        box.tag = "Pot"; // Pour que le raycast d'interaction le détecte comme un pot

        // Auto-scale : cible "longueur" sur la plus grande dimension
        Renderer[] rs = box.GetComponentsInChildren<Renderer>();
        Bounds boxBounds = new Bounds();
        bool boundsInit = false;
        if (rs.Length > 0)
        {
            boxBounds = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) boxBounds.Encapsulate(rs[i].bounds);
            boundsInit = true;
            float maxDim = Mathf.Max(boxBounds.size.x, boxBounds.size.y, boxBounds.size.z);
            if (maxDim > 0.001f)
                box.transform.localScale = Vector3.one * (targetLength / maxDim);
        }

        FixPrefabMaterials(box, new Color(0.42f, 0.27f, 0.15f));

        // Recalcule les bounds après scale
        Bounds finalBounds = new Bounds();
        bool finalInit = false;
        foreach (var r in box.GetComponentsInChildren<Renderer>())
        {
            if (!finalInit) { finalBounds = r.bounds; finalInit = true; }
            else finalBounds.Encapsulate(r.bounds);
        }

        if (finalInit)
        {
            // BoxCollider solide pour bloquer le joueur (sur les planches du bac)
            BoxCollider bc = box.AddComponent<BoxCollider>();
            Vector3 ls = box.transform.lossyScale;
            bc.size = new Vector3(finalBounds.size.x / ls.x, finalBounds.size.y / ls.y, finalBounds.size.z / ls.z);
            bc.center = box.transform.InverseTransformPoint(finalBounds.center);

            // Trigger d'interaction (zone autour du bac) sur un enfant
            // pour ne pas interférer avec le collider solide
            GameObject trig = new GameObject("InteractionTrigger");
            trig.transform.parent = box.transform;
            trig.transform.position = finalBounds.center;
            trig.transform.localScale = Vector3.one;
            BoxCollider triggerBC = trig.AddComponent<BoxCollider>();
            triggerBC.isTrigger = true;
            triggerBC.size = new Vector3(finalBounds.size.x / ls.x * 1.2f, finalBounds.size.y / ls.y * 1.4f, finalBounds.size.z / ls.z * 1.2f);
        }

        // Composant de gameplay : le bac réagit comme un grand pot (1 plante par bac)
        if (box.GetComponent<PotInteraction>() == null)
            box.AddComponent<PotInteraction>();

        // SoilVisual : un cube fin posé sur le dessus du bac, masqué jusqu'à ajout de terre
        if (finalInit)
        {
            GameObject soilVis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            soilVis.name = "SoilVisual";
            soilVis.transform.parent = box.transform;
            Vector3 soilWorld = new Vector3(finalBounds.center.x, finalBounds.max.y - 0.04f, finalBounds.center.z);
            soilVis.transform.position = soilWorld;
            Vector3 ls2 = box.transform.lossyScale;
            soilVis.transform.localScale = new Vector3(
                (finalBounds.size.x * 0.85f) / ls2.x,
                0.08f / ls2.y,
                (finalBounds.size.z * 0.85f) / ls2.z);
            soilVis.GetComponent<Renderer>().sharedMaterial = GetMaterial("BoxSoil", new Color(0.25f, 0.15f, 0.08f), 0f, 0.06f);
            Destroy(soilVis.GetComponent<Collider>());
            soilVis.SetActive(false);
        }

        planterBoxes.Add(box);
        pots.Add(box); // inclu dans la liste des pots pour le gameplay (waves, raycast, etc.)
    }

    // ─────────────────────────────────────────────
    // CHEMIN CENTRAL : allée de dalles entre les bacs gauche/droit
    // ─────────────────────────────────────────────
    private void BuildIndoorPath()
    {
        Material stone = GetMaterial("PathStone", new Color(0.62f, 0.58f, 0.52f), 0.05f, 0.35f);
        Material gravel = GetMaterial("PathGravel", new Color(0.55f, 0.50f, 0.43f), 0f, 0.20f);

        // Largeur du chemin (entre les bacs : ~2 m libre au centre)
        const float pathWidth = 1.4f;
        const float tileLength = 0.8f;
        const float tileGap = 0.05f;

        float zStart = -greenhouseLength / 2f + 0.3f;
        float zEnd = greenhouseLength / 2f - 0.3f;

        // Fond du chemin : grand cube fin de gravier sous les dalles
        GameObject bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bed.name = "PathBed";
        bed.transform.parent = transform;
        bed.transform.position = new Vector3(0, GroundY + 0.005f, 0);
        bed.transform.localScale = new Vector3(pathWidth + 0.2f, 0.01f, zEnd - zStart);
        bed.GetComponent<Renderer>().sharedMaterial = gravel;
        Destroy(bed.GetComponent<Collider>());

        // Dalles individuelles posées en ligne, avec léger décalage X aléatoire
        int tileCount = Mathf.FloorToInt((zEnd - zStart) / (tileLength + tileGap));
        float totalUsed = tileCount * tileLength + (tileCount - 1) * tileGap;
        float startOffset = zStart + (zEnd - zStart - totalUsed) / 2f + tileLength / 2f;

        for (int i = 0; i < tileCount; i++)
        {
            float z = startOffset + i * (tileLength + tileGap);
            // Évite de poser une dalle sous la table (zone -1 à +1 sur Z)
            if (Mathf.Abs(z) < 1.1f) continue;

            float xJitter = Random.Range(-0.05f, 0.05f);
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"PathTile_{i}";
            tile.transform.parent = transform;
            tile.transform.position = new Vector3(xJitter, GroundY + 0.02f, z);
            tile.transform.localScale = new Vector3(pathWidth - 0.1f, 0.025f, tileLength);
            tile.transform.rotation = Quaternion.Euler(0, Random.Range(-2f, 2f), 0);
            tile.GetComponent<Renderer>().sharedMaterial = stone;
            Destroy(tile.GetComponent<Collider>());
        }
    }

    // ─────────────────────────────────────────────
    // POTS : dispersés aléatoirement dans la serre
    // ─────────────────────────────────────────────
    private void BuildPots()
    {
        Material clay = GetMaterial("Clay", new Color(0.55f, 0.27f, 0.15f), 0f, 0.30f);
        Material soil = GetMaterial("Soil", new Color(0.25f, 0.15f, 0.08f), 0f, 0.06f);

        // Pots dispersés aléatoirement dans la zone CENTRALE de la serre,
        // entre les bacs latéraux (qui sont à |x|≈3.3) et la table (|x|<1.8, |z|<0.9)
        int totalPots = potsPerSide * 2;
        // Zone X disponible : entre les bacs (intérieur) — environ |x| < width/2 - 1.5
        float xRange = greenhouseWidth / 2f - 1.5f;
        float zRange = greenhouseLength / 2f - 0.8f;

        int placed = 0;
        int safety = totalPots * 30;
        const float minPotDist = 0.9f; // distance minimale entre 2 pots
        while (placed < totalPots && safety-- > 0)
        {
            float x = Random.Range(-xRange, xRange);
            float z = Random.Range(-zRange, zRange);

            // Évite la table (rectangle 1.8 × 1.0 autour de l'origine)
            if (Mathf.Abs(x) < 2.0f && Mathf.Abs(z) < 1.1f) continue;

            // Évite les autres pots
            bool overlap = false;
            foreach (var p in pots)
            {
                if (p == null) continue;
                float dx = x - p.transform.position.x;
                float dz = z - p.transform.position.z;
                if (dx * dx + dz * dz < minPotDist * minPotDist) { overlap = true; break; }
            }
            if (overlap) continue;

            pots.Add(CreatePot(new Vector3(x, GroundY + 0.5f, z), $"Pot_{placed}", clay, soil));
            placed++;
        }
    }

    private GameObject CreatePot(Vector3 pos, string name, Material clay, Material soil)
    {
        // Fallback FBX généré : pivot à la base
        GameObject fbxPot = potPrefab != null ? potPrefab : Resources.Load<GameObject>("Tools/Pots");

        if (fbxPot != null)
        {
            // Pose le pot sur l'herbe (le pivot du FBX est à la base)
            Vector3 fbxPos = new Vector3(pos.x, GroundY, pos.z);
            GameObject prefabPot = Instantiate(fbxPot, fbxPos, Quaternion.identity, transform);
            prefabPot.name = name;
            prefabPot.tag = "Pot";

            // Auto-scale : cible 55 cm de hauteur quel que soit le facteur d'import FBX
            Renderer[] potRs = prefabPot.GetComponentsInChildren<Renderer>();
            if (potRs.Length > 0)
            {
                Bounds pb = potRs[0].bounds;
                for (int i = 1; i < potRs.Length; i++) pb.Encapsulate(potRs[i].bounds);
                if (pb.size.y > 0.001f)
                    prefabPot.transform.localScale = Vector3.one * (0.55f / pb.size.y);
            }

            // Texture/couleur argile via FixPrefabMaterials avec fallback orange terracotta
            FixPrefabMaterials(prefabPot, new Color(0.62f, 0.32f, 0.18f));

            if (prefabPot.GetComponent<PotInteraction>() == null)
                prefabPot.AddComponent<PotInteraction>();

            // Colliders calibrés sur les bounds APRÈS scale (sinon décalage si Unity FBX import varie)
            Renderer[] postRs = prefabPot.GetComponentsInChildren<Renderer>();
            Bounds wb = new Bounds();
            bool wbInit = false;
            foreach (var r in postRs)
            {
                if (!wbInit) { wb = r.bounds; wbInit = true; }
                else wb.Encapsulate(r.bounds);
            }
            if (wbInit)
            {
                Vector3 ls = prefabPot.transform.lossyScale;
                // Capsule collider qui englobe le pot (radius = mi-largeur, height = hauteur totale)
                CapsuleCollider cc = prefabPot.AddComponent<CapsuleCollider>();
                cc.direction = 1; // axe Y
                cc.radius = (Mathf.Max(wb.size.x, wb.size.z) * 0.45f) / Mathf.Max(ls.x, ls.z);
                cc.height = wb.size.y / ls.y;
                cc.center = prefabPot.transform.InverseTransformPoint(wb.center);

                // Trigger sphère pour pickup/interaction, légèrement plus large que le pot
                SphereCollider prefabTrigger = prefabPot.AddComponent<SphereCollider>();
                prefabTrigger.isTrigger = true;
                float maxRadiusWorld = Mathf.Max(wb.size.x, wb.size.z) * 0.55f;
                prefabTrigger.radius = maxRadiusWorld / Mathf.Max(ls.x, ls.z);
                prefabTrigger.center = prefabPot.transform.InverseTransformPoint(wb.center);
            }
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
        float tableY = GroundY + 1.0f; // hauteur du plateau, table reposant sur l'herbe

        // Le FBX Spray a son pivot à la base, on le pose sur le plateau (tableY)
        Vector3 fertPos = new Vector3(-0.8f, tableY + 0.02f, 0);
        Vector3 pestPos = new Vector3(0.8f, tableY + 0.02f, 0);

        // Préfère le nouveau modèle texturé "Sprayer" si présent, sinon le Spray généré.
        GameObject sprayModel =
            Resources.Load<GameObject>("Tools/Sprayer")
            ?? Resources.Load<GameObject>("Tools/Spray");

        // Spray d'engrais (bleu)
        if (fertilizerSprayPrefab != null)
        {
            fertilizerSpray = Instantiate(fertilizerSprayPrefab, fertPos, Quaternion.identity, transform);
            fertilizerSpray.name = "FertilizerSpray";
            fertilizerSpray.tag = "Pickup";
            EnsurePickup(fertilizerSpray, ItemType.FertilizerSpray);
        }
        else if (sprayModel != null)
        {
            fertilizerSpray = InstantiateSprayFromFBX(sprayModel, "FertilizerSpray", fertPos,
                new Color(0.25f, 0.55f, 0.90f), ItemType.FertilizerSpray);
        }
        else
        {
            fertilizerSpray = BuildSpray("FertilizerSpray", fertPos, new Color(0.2f, 0.5f, 0.9f), ItemType.FertilizerSpray);
        }

        // Spray anti-nuisibles (rouge)
        if (pestSprayPrefab != null)
        {
            pestSpray = Instantiate(pestSprayPrefab, pestPos, Quaternion.identity, transform);
            pestSpray.name = "PestSpray";
            pestSpray.tag = "Pickup";
            EnsurePickup(pestSpray, ItemType.PestSpray);
        }
        else if (sprayModel != null)
        {
            pestSpray = InstantiateSprayFromFBX(sprayModel, "PestSpray", pestPos,
                new Color(0.85f, 0.18f, 0.18f), ItemType.PestSpray);
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

    // Instancie le FBX Spray, le redimensionne et applique une teinte sur les textures
    private GameObject InstantiateSprayFromFBX(GameObject prefab, string name, Vector3 pos,
                                                Color tint, ItemType type)
    {
        GameObject go = Instantiate(prefab, pos, Quaternion.identity, transform);
        go.name = name;
        go.tag = "Pickup";

        // Auto-scale : taille max ≈ 25 cm (hauteur d'un spray réel)
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (maxDim > 0.001f)
                go.transform.localScale = Vector3.one * (0.25f / maxDim);
        }

        // Pour chaque renderer : reconvertit le matériau URP→Standard, garde la texture,
        // et applique la teinte. Si pas de texture (modèle généré), on remplace par
        // un Standard uniforme teinté.
        Shader std = Shader.Find("Standard");
        foreach (var rend in rs)
        {
            Material[] mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material src = mats[i];
                Material nm = new Material(std);
                Texture tex = null;
                if (src != null)
                {
                    if (src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
                    if (tex == null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
                }
                if (tex != null)
                {
                    nm.mainTexture = tex;
                    nm.color = tint; // multiplie la texture par la teinte
                }
                else
                {
                    nm.color = tint;
                }
                nm.SetFloat("_Metallic", 0.05f);
                nm.SetFloat("_Glossiness", 0.35f);
                mats[i] = nm;
            }
            rend.sharedMaterials = mats;
        }

        EnsurePickup(go, type);
        return go;
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
        Vector3 pos = new Vector3(-greenhouseWidth / 2f + 1f, GroundY + 0.3f, greenhouseLength / 2f - 2f);

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
        // FBX : pivot à la base → poser à GroundY pour reposer sur l'herbe
        Vector3 fbxPos = new Vector3(greenhouseWidth / 2f - 1f, GroundY, greenhouseLength / 2f - 2f);
        // Procédural : cylindre centré, demi-hauteur 0.4 → center à GroundY + 0.4
        Vector3 procPos = new Vector3(greenhouseWidth / 2f - 1f, GroundY + 0.4f, greenhouseLength / 2f - 2f);

        // Fallback : charge depuis Resources/Tools/WateringCan (FBX/OBJ) si l'Inspector ne le donne pas
        GameObject canModel = wateringCanPrefab
            ?? Resources.Load<GameObject>("Tools/WateringCan")
            ?? Resources.Load<GameObject>("Tools/Watering_can_regadera");

        if (canModel != null)
        {
            wateringCan = Instantiate(canModel, fbxPos, Quaternion.identity, transform);
            wateringCan.name = "WateringCan";
            wateringCan.tag = "Pickup";

            // Auto-scale : ramène la dimension max à ~0.6 m (taille d'un arrosoir réel)
            Renderer[] rs = wateringCan.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (maxDim > 0.001f)
                    wateringCan.transform.localScale = Vector3.one * (0.6f / maxDim);
            }

            FixPrefabMaterials(wateringCan, new Color(0.72f, 0.78f, 0.82f));
            EnsurePickup(wateringCan, ItemType.WateringCan);
            return;
        }

        wateringCan = new GameObject("WateringCan");
        wateringCan.transform.parent = transform;
        wateringCan.transform.position = procPos;
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
    // PLANTES DÉCO : 4 au sol (coins), 3 suspendues au plafond, 3 en hauteur
    // ─────────────────────────────────────────────
    private void BuildDecorPlants()
    {
        GameObject root = new GameObject("DecorPlants");
        root.transform.parent = transform;

        // Sol : 4 coins de la serre (en dehors de la zone des PlanterBox/pots)
        SpawnDecorPlant("Decorations/Plants/Plant_01/plant",
            new Vector3(-3.4f, GroundY, -5.4f), 0.85f, root.transform, anchorTop: false);
        SpawnDecorPlant("Decorations/Plants/Plant_02/plant",
            new Vector3( 3.4f, GroundY, -5.4f), 0.95f, root.transform, anchorTop: false);
        SpawnDecorPlant("Decorations/Plants/Plant_03/plant",
            new Vector3(-3.4f, GroundY,  5.4f), 0.75f, root.transform, anchorTop: false);
        SpawnDecorPlant("Decorations/Plants/Plant_04/plant",
            new Vector3( 3.4f, GroundY,  5.4f), 0.70f, root.transform, anchorTop: false);

        // Suspendues : sur l'axe central (x=0), espacées le long de la serre, en dessous du sommet de l'arche
        float ceilingY = GroundY + greenhouseHeight - 0.4f; // ~ 2.9m monde
        SpawnDecorPlant("Decorations/Plants/Plant_05/marijuanna",
            new Vector3(0f, ceilingY - 0.9f, -3.5f), 0.55f, root.transform, anchorTop: true, ceilingY);
        SpawnDecorPlant("Decorations/Plants/Plant_07/eb_house_plant_02",
            new Vector3(0f, ceilingY - 0.7f,  0f), 0.45f, root.transform, anchorTop: true, ceilingY);
        SpawnDecorPlant("Decorations/Plants/Plant_08/eb_house_plant_03",
            new Vector3(0f, ceilingY - 0.7f,  3.5f), 0.45f, root.transform, anchorTop: true, ceilingY);

        // En hauteur : sur les bacs/planters (côté E et W) et sur la table centrale
        SpawnDecorPlant("Decorations/Plants/Plant_06/eb_house_plant_01",
            new Vector3(1.1f, GroundY + 1.05f, 0f), 0.45f, root.transform, anchorTop: false);
        SpawnDecorPlant("Decorations/Plants/Plant_09/indoor_plant_02",
            new Vector3(-3.3f, GroundY + 0.55f, -4.0f), 0.50f, root.transform, anchorTop: false);
        SpawnDecorPlant("Decorations/Plants/Plant_10/lowpoly_plant",
            new Vector3( 3.3f, GroundY + 0.55f,  4.0f), 0.50f, root.transform, anchorTop: false);
    }

    // Instancie une plante déco, l'auto-scale, applique materials, et ajoute une corde si suspendue
    private void SpawnDecorPlant(string resPath, Vector3 worldPos, float targetHeight,
                                 Transform parent, bool anchorTop, float ceilingY = 0f)
    {
        GameObject prefab = Resources.Load<GameObject>(resPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[Deco] Prefab introuvable : {resPath}");
            return;
        }

        GameObject inst = Instantiate(prefab, worldPos,
            Quaternion.Euler(0, Random.Range(0f, 360f), 0), parent);
        inst.name = resPath.Substring(resPath.LastIndexOf('/') + 1);

        Renderer[] rs = inst.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            if (b.size.y > 0.001f)
                inst.transform.localScale = Vector3.one * (targetHeight / b.size.y);

            // Réaligne : base posée sur worldPos.y (sol/étagère) ou sommet à worldPos.y (suspension)
            Bounds b2 = inst.GetComponentInChildren<Renderer>().bounds;
            for (int i = 1; i < rs.Length; i++) b2.Encapsulate(rs[i].bounds);
            float yOffset = anchorTop ? worldPos.y - b2.max.y : worldPos.y - b2.min.y;
            inst.transform.position += new Vector3(0, yOffset, 0);
        }

        FixPrefabMaterials(inst, new Color(0.30f, 0.55f, 0.20f));

        foreach (var col in inst.GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (anchorTop && ceilingY > worldPos.y)
        {
            GameObject rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rope.name = "DecoRope";
            Destroy(rope.GetComponent<Collider>());
            rope.transform.parent = parent;
            float topY = worldPos.y; // sommet de la plante = point d'accroche bas
            float ropeMidY = (ceilingY + topY) * 0.5f;
            float ropeLen = ceilingY - topY;
            rope.transform.position = new Vector3(worldPos.x, ropeMidY, worldPos.z);
            rope.transform.localScale = new Vector3(0.02f, ropeLen * 0.5f, 0.02f);
            var rr = rope.GetComponent<Renderer>();
            if (rr != null)
            {
                var ropeMat = new Material(Shader.Find("Standard"));
                ropeMat.color = new Color(0.32f, 0.20f, 0.10f);
                ropeMat.SetFloat("_Glossiness", 0.05f);
                rr.sharedMaterial = ropeMat;
            }
        }
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
            // BoxCollider trigger calibré sur les bounds visuelles (au moins 20 cm pour le raycast)
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                Vector3 ls = go.transform.lossyScale;
                bc.size = new Vector3(
                    Mathf.Max(b.size.x, 0.2f) / Mathf.Max(ls.x, 0.001f),
                    Mathf.Max(b.size.y, 0.2f) / Mathf.Max(ls.y, 0.001f),
                    Mathf.Max(b.size.z, 0.2f) / Mathf.Max(ls.z, 0.001f));
                bc.center = go.transform.InverseTransformPoint(b.center);
            }
            else
            {
                bc.size = Vector3.one * 0.6f;
            }
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
