// ShovelBuilder.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Créer Pelle" : instancie Shovel.fbx dans la scène, lui applique
// son matériau PBR (Albedo, Normal, Metallic, Roughness, AO, Emissive), et y
// attache un Pickup (typeItem = "pelle"). Utilisée pour enlever une plante
// mature ou morte d'un pot (cf. Pot.cs : action "pelle" → reset du pot).
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class ShovelBuilder
{
    // Chemins candidats pour le FBX (l'utilisateur l'a posé à la racine d'Assets)
    static readonly string[] FbxCandidates =
    {
        "Assets/Resources/Tools/Shovel.fbx",
        "Assets/Shovel.fbx",
    };

    static readonly string[] TexturesRoots = { "Assets/Resources/Tools", "Assets" };

    [MenuItem("Tools/Créer Pelle")]
    public static void CreerPelle()
    {
        // 1) Trouve le FBX
        GameObject fbx = null;
        string fbxPath = null;
        foreach (var p in FbxCandidates)
        {
            fbx = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (fbx != null) { fbxPath = p; break; }
        }
        if (fbx == null)
        {
            EditorUtility.DisplayDialog("Modèle introuvable",
                "Shovel.fbx introuvable. Essayé : " + string.Join(", ", FbxCandidates), "OK");
            return;
        }

        // 2) Instancie le FBX
        GameObject pelle = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        pelle.name = "Pelle";
        pelle.transform.position = Vector3.zero;

        // 3) Auto-scale : ~60 cm de long + pose à Y=0
        Renderer[] rs = pelle.GetComponentsInChildren<Renderer>();
        Bounds bw = new Bounds();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (maxDim > 0.001f) pelle.transform.localScale = Vector3.one * (0.60f / maxDim);

            bw = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) bw.Encapsulate(rs[i].bounds);
            pelle.transform.position += new Vector3(0, -bw.min.y, 0);

            bw = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) bw.Encapsulate(rs[i].bounds);
        }

        // 4) Crée/charge le matériau PBR avec les textures Shovel_*
        Material mat = ChargerOuCreerMateriau();
        foreach (Renderer rend in pelle.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            rend.sharedMaterials = mats;
        }

        // 5) Pickup (typeItem = "pelle")
        Pickup pickup = pelle.AddComponent<Pickup>();
        pickup.typeItem = "pelle";
        pickup.positionEnMain = new Vector3(0.35f, -0.30f, 0.55f);
        pickup.rotationEnMain = new Vector3(20f, -30f, 60f);

        // 6) Collider global pour le clic
        BoxCollider bc = pelle.AddComponent<BoxCollider>();
        if (rs.Length > 0)
        {
            Vector3 ls = pelle.transform.lossyScale;
            bc.center = pelle.transform.InverseTransformPoint(bw.center);
            bc.size = new Vector3(bw.size.x / ls.x, bw.size.y / ls.y, bw.size.z / ls.z);
        }

        Selection.activeGameObject = pelle;
        Debug.Log("✅ Pelle créée. Ramassable (typeItem=pelle). Clic-maintenu sur un pot avec plante mature pour la déraciner.");
    }

    // ── Création du matériau PBR à partir des textures Shovel_* ──────────────
    static Material ChargerOuCreerMateriau()
    {
        const string dossier = "Assets/Resources/Materials";
        const string chemin  = dossier + "/Shovel.mat";
        if (!Directory.Exists(dossier)) Directory.CreateDirectory(dossier);

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(chemin);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, chemin);
        }

        // Charge chaque texture si trouvée (par nom dans plusieurs dossiers)
        Texture2D albedo    = TrouverTexture("Shovel_Albedo");
        Texture2D normal    = TrouverTexture("Shovel_Normal");
        Texture2D metallic  = TrouverTexture("Shovel_Metallic");
        Texture2D ao        = TrouverTexture("Shovel_AO");
        Texture2D emissive  = TrouverTexture("Shovel_Emissive");

        if (albedo != null) mat.mainTexture = albedo;

        if (normal != null)
        {
            // S'assure que la texture est importée en NormalMap
            string path = AssetDatabase.GetAssetPath(normal);
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }

        if (metallic != null)
        {
            mat.SetTexture("_MetallicGlossMap", metallic);
            mat.EnableKeyword("_METALLICGLOSSMAP");
        }

        if (ao != null)
        {
            mat.SetTexture("_OcclusionMap", ao);
        }

        if (emissive != null)
        {
            mat.SetTexture("_EmissionMap", emissive);
            mat.SetColor("_EmissionColor", Color.white * 0.3f);
            mat.EnableKeyword("_EMISSION");
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static Texture2D TrouverTexture(string nomSansExt)
    {
        foreach (var dir in TexturesRoots)
        {
            foreach (var ext in new[] { ".png", ".jpg", ".tga" })
            {
                string path = dir + "/" + nomSansExt + ext;
                Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (t != null) return t;
            }
        }
        return null;
    }
}
