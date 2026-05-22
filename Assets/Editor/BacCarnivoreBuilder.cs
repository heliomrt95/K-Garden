// BacCarnivoreBuilder.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Créer Bac Carnivore" : instancie PlanterBox.fbx (bac CARRÉ,
// même modèle que BacATerre) configuré comme bac spécial pour la graine
// carnivore.
//
// Différences avec un pot normal :
//   - Modèle CARRÉ (PlanterBox.fbx) au lieu du pot rond
//   - Matériau sombre / sang
//   - accepteUniquementCarnivore = true   → refuse les autres graines
//   - terreRequise = 2                   → 2 actions de mise de terre
//   - eauRequise = 2                     → 2 cycles d'arrosage
//
// Réutilise toute la logique Pot.cs.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class BacCarnivoreBuilder
{
    const string CHEMIN_FBX = "Assets/Resources/Tools/PlanterBox.fbx";

    [MenuItem("Tools/Créer Bac Carnivore")]
    public static void CreerBac()
    {
        // 1) Charge le modèle (bac carré)
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CHEMIN_FBX);
        if (fbx == null)
        {
            EditorUtility.DisplayDialog("Modèle introuvable",
                "PlanterBox.fbx introuvable à " + CHEMIN_FBX, "OK");
            return;
        }

        // 2) Instancie
        GameObject bac = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        bac.name = "BacCarnivore";
        bac.transform.position = Vector3.zero;

        // 3) Auto-scale : largeur ~80 cm + pose la base à Y=0
        Renderer[] rs = bac.GetComponentsInChildren<Renderer>();
        Bounds bw = new Bounds();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            float maxXZ = Mathf.Max(b.size.x, b.size.z);
            if (maxXZ > 0.001f) bac.transform.localScale = Vector3.one * (0.80f / maxXZ);

            bw = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) bw.Encapsulate(rs[i].bounds);
            bac.transform.position += new Vector3(0, -bw.min.y, 0);

            bw = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) bw.Encapsulate(rs[i].bounds);
        }
        else bw = new Bounds(bac.transform.position + Vector3.up * 0.15f, new Vector3(0.8f, 0.3f, 0.8f));

        // 4) Matériau noir/sang sur les planches du bac
        Material matBac = ChargerOuCreerMateriau("BoisCarnivore",
            new Color(0.18f, 0.04f, 0.04f), 0.10f, 0.20f);
        foreach (Renderer rend in rs)
        {
            Material[] mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = matBac;
            rend.sharedMaterials = mats;
        }

        // 5) Matériaux des enfants
        Material matTerre = ChargerOuCreerMateriau("Earth",
            new Color(0.22f, 0.13f, 0.07f), 0f, 0.04f);
        Material matGraine = ChargerOuCreerMateriau("SproutCarnivore",
            new Color(0.85f, 0.15f, 0.15f), 0f, 0.30f);
        Material matEau = ChargerOuCreerMateriau("Water",
            new Color(0.25f, 0.55f, 0.85f), 0.2f, 0.95f);

        // 6) Enfants Terre/Graine/Eau — pour un bac carré, on utilise des cubes
        //    pour la terre (qui remplit le rectangle) et des cylindres pour la
        //    graine + l'eau (au centre).
        GameObject visuelTerre  = CreerEnfantPrimitive(bac, "Terre",  PrimitiveType.Cube,     bw, 0.20f, 0.92f, 0.40f, matTerre);
        GameObject visuelGraine = CreerEnfantPrimitive(bac, "Graine", PrimitiveType.Cylinder, bw, 0.55f, 0.10f, 0.50f, matGraine);
        GameObject visuelEau    = CreerEnfantPrimitive(bac, "Eau",    PrimitiveType.Cube,     bw, 0.40f, 0.85f, 0.05f, matEau);

        // 7) Script Pot configuré pour le bac carnivore
        Pot scriptPot = bac.AddComponent<Pot>();
        scriptPot.visuelTerre = visuelTerre;
        scriptPot.visuelGraine = visuelGraine;
        scriptPot.visuelEau = visuelEau;
        scriptPot.accepteUniquementCarnivore = true;
        scriptPot.terreRequise = 2;
        scriptPot.eauRequise = 2;

        // 8) Collider global pour le clic
        BoxCollider bc = bac.AddComponent<BoxCollider>();
        Vector3 ls = bac.transform.lossyScale;
        bc.center = bac.transform.InverseTransformPoint(bw.center);
        bc.size = new Vector3(bw.size.x / ls.x, bw.size.y / ls.y, bw.size.z / ls.z);

        Selection.activeGameObject = bac;
        Debug.Log("✅ BacCarnivore (carré) créé. Accepte uniquement la graine_4. Exigence : 2 terre + 2 eau.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    static GameObject CreerEnfantPrimitive(GameObject parent, string nom, PrimitiveType type, Bounds wb,
                                            float offsetYRatio, float taillXZ, float taillY, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = nom;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent.transform);

        Vector3 worldPos = new Vector3(wb.center.x,
                                       wb.center.y + wb.size.y * offsetYRatio,
                                       wb.center.z);
        go.transform.position = worldPos;

        float facteurY = (type == PrimitiveType.Cylinder) ? 0.5f : 1f;
        Vector3 ls = parent.transform.lossyScale;
        go.transform.localScale = new Vector3(
            (wb.size.x * taillXZ) / Mathf.Max(0.001f, ls.x),
            (wb.size.y * taillY * facteurY) / Mathf.Max(0.001f, ls.y),
            (wb.size.z * taillXZ) / Mathf.Max(0.001f, ls.z));

        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static Material ChargerOuCreerMateriau(string nom, Color couleur, float metallique, float brillance)
    {
        string dossier = "Assets/Resources/Materials";
        string chemin = dossier + "/" + nom + ".mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(chemin);
        if (mat != null) return mat;

        if (!Directory.Exists(dossier)) Directory.CreateDirectory(dossier);
        mat = new Material(Shader.Find("Standard"));
        mat.color = couleur;
        mat.SetFloat("_Metallic", metallique);
        mat.SetFloat("_Glossiness", brillance);
        AssetDatabase.CreateAsset(mat, chemin);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
