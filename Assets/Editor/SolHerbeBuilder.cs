// SolHerbeBuilder.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Appliquer Texture Herbe au Sol" : crée (ou met à jour) un
// matériau "GrassSol.mat" dans Resources/Materials, avec une vraie texture
// d'herbe (Grass_Diffuse.jpg / Grass1.jpg), et l'assigne à l'objet "Plane"
// (ou tout autre objet racine nommé Sol / Ground / Floor) avec un tiling
// adapté à la taille du sol.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class SolHerbeBuilder
{
    // Liste des textures candidates (par ordre de préférence)
    static readonly string[] TexturesCandidates =
    {
        "Assets/Resources/Textures/Grass_Diffuse.jpg",
        "Assets/Resources/Textures/Grass_Diffuse.jpg.png",
        "Assets/Scenes/Grass_Diffuse.jpg",
        "Assets/Resources/Textures/Grass1.jpg",
        "Assets/Resources/Textures/Grass1.png",
        "Assets/Resources/Textures/Grass2.jpg",
        "Assets/Resources/Textures/Grass2.png",
    };

    [MenuItem("Tools/Créer Sol")]
    public static void CreerSol()
    {
        // Si un sol existe déjà (Plane / Sol / Ground / Floor à la racine), demander.
        GameObject existant = null;
        GameObject[] tous = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in tous)
        {
            if (!go.scene.IsValid()) continue;
            if (go.transform.parent != null) continue;
            string n = go.name.ToLower();
            if (n == "plane" || n == "sol" || n == "ground" || n == "floor")
            {
                existant = go; break;
            }
        }

        if (existant != null)
        {
            if (!EditorUtility.DisplayDialog("Sol déjà présent",
                    "Un sol nommé '" + existant.name + "' existe déjà. Le remplacer ?",
                    "Oui", "Annuler"))
                return;
            Object.DestroyImmediate(existant);
        }

        // Crée un Plane primitif (10×10 unités à scale 1) — on le scale ×5 pour 50×50 m
        GameObject sol = GameObject.CreatePrimitive(PrimitiveType.Plane);
        sol.name = "Plane";
        sol.transform.position = Vector3.zero;
        sol.transform.localScale = new Vector3(5f, 1f, 5f);

        // Static flags pour NavMesh / lightmaps
        GameObjectUtility.SetStaticEditorFlags(sol,
            StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.NavigationStatic | StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);

        // Applique la texture herbe (réutilise la fonction existante)
        Appliquer();

        Selection.activeGameObject = sol;
        EditorGUIUtility.PingObject(sol);
        Debug.Log("✅ Sol 'Plane' créé (50×50 m, Y=0) avec texture herbe.");
    }

    [MenuItem("Tools/Appliquer Texture Herbe au Sol")]
    public static void Appliquer()
    {
        // 1) Trouve la première texture qui existe
        Texture2D tex = null;
        string texPath = null;
        foreach (var p in TexturesCandidates)
        {
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (tex != null) { texPath = p; break; }
        }
        if (tex == null)
        {
            EditorUtility.DisplayDialog("Texture introuvable",
                "Aucune texture d'herbe trouvée dans Assets/Resources/Textures/.", "OK");
            return;
        }

        // 1b) Force les bons settings d'import pour éviter les bords noirs :
        //     - wrap Repeat (sinon Unity affiche les pixels du bord = bordure sombre)
        //     - filter Trilinear (lissage entre mipmaps)
        //     - aniso 8 (texture nette à angle rasant)
        TextureImporter ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (ti != null)
        {
            bool change = false;
            if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; change = true; }
            if (ti.filterMode != FilterMode.Trilinear) { ti.filterMode = FilterMode.Trilinear; change = true; }
            if (ti.anisoLevel < 8) { ti.anisoLevel = 8; change = true; }
            if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; change = true; }
            if (change) ti.SaveAndReimport();
        }

        // 2) Crée ou met à jour le matériau GrassSol.mat
        const string dossierMat = "Assets/Resources/Materials";
        const string cheminMat  = dossierMat + "/GrassSol.mat";
        if (!Directory.Exists(dossierMat)) Directory.CreateDirectory(dossierMat);

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(cheminMat);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, cheminMat);
        }
        mat.mainTexture = tex;
        mat.color = Color.white; // pas de tint, on veut la couleur native de la texture
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.08f);

        // 3) Cherche les objets "sol" dans la scène
        int appliques = 0;
        GameObject[] tous = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in tous)
        {
            if (!go.scene.IsValid()) continue;
            if (go.transform.parent != null) continue;

            string nom = go.name.ToLower();
            bool estSol = nom == "plane" || nom == "sol" || nom == "ground" || nom == "floor";
            if (!estSol) continue;

            Renderer rend = go.GetComponent<Renderer>();
            if (rend == null) continue;

            // 4) Assigne le matériau
            rend.sharedMaterial = mat;
            appliques++;

            // 5) Tiling : ~1 tile par 5m monde — moins de répétitions = moins
            //    de coutures visibles entre les tiles d'herbe.
            Vector3 ls = go.transform.lossyScale;
            float taillesXMonde = 10f * ls.x; // Plane primitive = 10×10 unités à scale 1
            float taillesZMonde = 10f * ls.z;
            mat.mainTextureScale = new Vector2(taillesXMonde / 5f, taillesZMonde / 5f);
            mat.mainTextureOffset = Vector2.zero;

            Debug.Log("[Herbe] Texture appliquée sur '" + go.name + "' (tiling " +
                      mat.mainTextureScale.x + "×" + mat.mainTextureScale.y + ").");
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        if (appliques > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        EditorUtility.DisplayDialog("Texture Herbe",
            "Texture : " + Path.GetFileName(texPath) + "\n" +
            "Matériau : GrassSol.mat\n" +
            "Sols modifiés : " + appliques, "OK");
    }
}
