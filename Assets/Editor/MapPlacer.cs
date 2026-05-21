// MapPlacer.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Placer la Map autour de la Serre" : pose un sol low-poly du
// pack SimpleNaturePack (JustCreate) sous la Serre, puis génère un décor
// "forêt" tout autour (arbres, rochers, buissons, fleurs, herbes, champignons).
//
// - Le sol choisi est le Ground_XX le plus PLAT des 3 du pack (auto).
// - Une zone de sécurité (calculée depuis les bounds de la Serre + marge)
//   reste libre de décor pour ne pas empiéter sur la serre.
// - Plus on s'éloigne, plus la densité d'arbres et la taille des rochers
//   augmentent → impression de relief / forêt dense en lisière.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

public static class MapPlacer
{
    const string PACK = "Assets/SimpleNaturePack/Prefabs/";
    const float TAILLE_SOL    = 70f;   // côté du sol (m)
    const float MARGE_SERRE   = 2.5f;  // marge libre autour de la serre (m)
    const float RAYON_MAX     = 32f;   // rayon utile pour disposer les décors

    [MenuItem("Tools/Placer la Map autour de la Serre")]
    public static void PlacerMap()
    {
        // 1) Trouver le Ground le plus plat parmi Ground_01..03
        GameObject groundPrefab = TrouverGroundLePlusPlat();
        if (groundPrefab == null)
        {
            EditorUtility.DisplayDialog("Erreur",
                "Aucun Ground_XX trouvé dans " + PACK, "OK");
            return;
        }

        // 2) Trouver la Serre et calculer ses bounds (zone de sécurité)
        GameObject serre = GameObject.Find("Serre");
        Vector3 centreSerre = serre != null ? serre.transform.position : Vector3.zero;
        float baseSerreY = centreSerre.y;
        float rayonSerre = 4f;
        if (serre != null)
        {
            Renderer[] rs = serre.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                baseSerreY = b.min.y;
                centreSerre = new Vector3(b.center.x, baseSerreY, b.center.z);
                rayonSerre  = Mathf.Max(b.extents.x, b.extents.z);
            }
        }
        float rayonSecu = rayonSerre + MARGE_SERRE;

        // 3) Remplacer une Map existante
        GameObject ancien = GameObject.Find("Map");
        if (ancien != null)
        {
            if (!EditorUtility.DisplayDialog("Map déjà présente",
                    "Un objet 'Map' existe déjà. Le remplacer ?", "Oui", "Annuler"))
                return;
            Object.DestroyImmediate(ancien);
        }

        // 4) Conteneur racine
        GameObject mapRoot = new GameObject("Map");

        // 5) Sol
        GameObject sol = (GameObject)PrefabUtility.InstantiatePrefab(groundPrefab);
        sol.name = "Sol";
        sol.transform.SetParent(mapRoot.transform);
        ScaleEtPoserSol(sol, centreSerre, baseSerreY);

        // 6) Décors
        Random.InitState(42); // reproductible
        GenererDecors(mapRoot.transform, centreSerre, baseSerreY, rayonSecu);

        // 7) Désactiver l'ancien Plane
        GameObject plane = GameObject.Find("Plane");
        if (plane != null)
        {
            plane.SetActive(false);
            Debug.Log("Ancien 'Plane' désactivé.");
        }

        // 8) Static flags
        AppliquerStaticFlagsRecursif(mapRoot);

        Selection.activeGameObject = mapRoot;
        EditorGUIUtility.PingObject(mapRoot);
        Debug.Log("✅ Map placée — sol: " + groundPrefab.name +
                  ", zone serre libre: r=" + rayonSecu.ToString("F1") + "m" +
                  ". Pense à re-baker le NavMesh.");
    }

    // ── Sol le plus plat = celui dont le mesh a la plus petite extension Y ────
    static GameObject TrouverGroundLePlusPlat()
    {
        GameObject best = null;
        float minH = float.MaxValue;
        for (int i = 1; i <= 3; i++)
        {
            string chemin = PACK + "Ground_0" + i + ".prefab";
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(chemin);
            if (p == null) continue;
            MeshFilter mf = p.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            float h = mf.sharedMesh.bounds.size.y;
            if (h < minH) { minH = h; best = p; }
        }
        return best;
    }

    static void ScaleEtPoserSol(GameObject sol, Vector3 centre, float baseY)
    {
        Renderer[] rs = sol.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            float taille = Mathf.Max(b.size.x, b.size.z);
            if (taille > 0.001f)
                sol.transform.localScale *= TAILLE_SOL / taille;
        }
        sol.transform.position = new Vector3(centre.x, baseY - 0.01f, centre.z);
    }

    // ── Génération des décors en anneaux concentriques ────────────────────────
    static void GenererDecors(Transform parent, Vector3 centre, float y, float rSecu)
    {
        string[] arbres   = { "Tree_01", "Tree_02", "Tree_03", "Tree_04", "Tree_05" };
        string[] rochers  = { "Rock_01", "Rock_02", "Rock_03", "Rock_04", "Rock_05" };
        string[] buissons = { "Bush_01", "Bush_02", "Bush_03" };
        string[] herbes   = { "Grass_01", "Grass_02" };
        string[] fleurs   = { "Flowers_01", "Flowers_02" };
        string[] champis  = { "Mushroom_01", "Mushroom_02" };
        string[] divers   = { "Branch_01", "Stump_01" };

        // Anneau proche (au ras de la zone serre) : herbes, fleurs, petits éléments
        float rIn = rSecu;
        float rMid = Mathf.Min(rSecu + 10f, RAYON_MAX);
        PlacerEnAnneau(parent, centre, y, rIn, rMid, 40, herbes,  0.8f, 1.2f);
        PlacerEnAnneau(parent, centre, y, rIn, rMid, 20, fleurs,  0.8f, 1.2f);
        PlacerEnAnneau(parent, centre, y, rIn, rMid, 12, champis, 0.8f, 1.2f);
        PlacerEnAnneau(parent, centre, y, rIn, rMid, 10, buissons,0.8f, 1.2f);

        // Anneau forêt : arbres, rochers, buissons
        float rFar = RAYON_MAX;
        PlacerEnAnneau(parent, centre, y, rMid, rFar, 35, arbres,   0.9f, 1.4f);
        PlacerEnAnneau(parent, centre, y, rMid, rFar, 18, rochers,  0.9f, 1.6f);
        PlacerEnAnneau(parent, centre, y, rMid, rFar, 12, buissons, 0.8f, 1.3f);
        PlacerEnAnneau(parent, centre, y, rMid, rFar, 8,  divers,   0.9f, 1.2f);

        // Anneau bordure (lisière épaisse) : gros rochers + arbres → "relief"
        // Les rochers reçoivent un petit décalage Y aléatoire pour simuler du relief.
        PlacerEnAnneauReliefRochers(parent, centre, y, rFar - 6f, rFar, 25);
        PlacerEnAnneau(parent, centre, y, rFar - 6f, rFar, 25, arbres, 1.0f, 1.5f);
    }

    static void PlacerEnAnneau(Transform parent, Vector3 centre, float y,
        float rMin, float rMax, int n, string[] noms, float scaleMin, float scaleMax)
    {
        for (int i = 0; i < n; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r     = Mathf.Sqrt(Random.Range(rMin * rMin, rMax * rMax)); // dist. uniforme
            Vector3 pos = centre + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
            pos.y = y;

            string nom = noms[Random.Range(0, noms.Length)];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PACK + nom + ".prefab");
            if (prefab == null) continue;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(parent);
            inst.transform.position = pos;
            inst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float s = Random.Range(scaleMin, scaleMax);
            inst.transform.localScale = new Vector3(s, s, s);
        }
    }

    // Pour la lisière : gros rochers, certains soulevés/enfoncés en Y → relief
    static void PlacerEnAnneauReliefRochers(Transform parent, Vector3 centre, float y,
        float rMin, float rMax, int n)
    {
        string[] rochers = { "Rock_01", "Rock_02", "Rock_03", "Rock_04", "Rock_05" };
        for (int i = 0; i < n; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r     = Mathf.Sqrt(Random.Range(rMin * rMin, rMax * rMax));
            Vector3 pos = centre + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
            // Relief : ±0.6 m (mais surtout positif pour suggérer une butte)
            pos.y = y + Random.Range(-0.2f, 0.8f);

            string nom = rochers[Random.Range(0, rochers.Length)];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PACK + nom + ".prefab");
            if (prefab == null) continue;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(parent);
            inst.transform.position = pos;
            inst.transform.rotation = Quaternion.Euler(
                Random.Range(-10f, 10f),
                Random.Range(0f, 360f),
                Random.Range(-10f, 10f));
            float s = Random.Range(1.2f, 2.2f);
            inst.transform.localScale = new Vector3(s, s, s);
        }
    }

    static void AppliquerStaticFlagsRecursif(GameObject root)
    {
        var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.NavigationStatic | StaticEditorFlags.ReflectionProbeStatic;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
    }
}
