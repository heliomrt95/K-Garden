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
        "Assets/Resources/Textures/Grass1.jpg",
        "Assets/Resources/Textures/Grass1.png",
        "Assets/Resources/Textures/Grass2.jpg",
        "Assets/Resources/Textures/Grass2.png",
    };

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

            // 5) Calcule un tiling raisonnable : ~2m par tile (pour un Plane de
            //    10×10 unités avec scale (sx, _, sz), on veut sx/2 × sz/2 tiles).
            Vector3 ls = go.transform.lossyScale;
            // Plane primitive Unity = 10×10 unités à scale 1
            float taillesXMonde = 10f * ls.x;
            float taillesZMonde = 10f * ls.z;
            mat.mainTextureScale = new Vector2(taillesXMonde / 2f, taillesZMonde / 2f);

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
