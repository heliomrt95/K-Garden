// GraineBuilder.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Créer Sachet de Graines" : construit dans la scène un petit
// sachet/pot de graines à poser sur la table. Cliquer dessus = prendre une
// graine dans la main (Source typeItem="graine").
//
// Structure créée :
//   SachetGraines (Empty)
//   ├── Pot      (Cylindre marron — le récipient)
//   └── Tas      (Sphère écrasée beige/brun — les graines visibles)
//
// Le script Source est sur le parent (cliquer sur n'importe quelle partie
// récupère une graine).
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class GraineBuilder
{
    [MenuItem("Tools/Créer Sachet de Graines")]
    public static void CreerSachet()
    {
        // 1) Matériaux
        Material matPot = ChargerOuCreerMateriau("BoisClair",
            new Color(0.55f, 0.35f, 0.20f), metallique: 0.05f, brillance: 0.30f);
        Material matGraines = ChargerOuCreerMateriau("Graines",
            new Color(0.65f, 0.48f, 0.22f), metallique: 0f, brillance: 0.15f);

        // 2) Parent vide
        GameObject sachet = new GameObject("SachetGraines");
        sachet.transform.position = Vector3.zero;

        // 3) Récipient : petit cylindre marron
        GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pot.name = "Pot";
        pot.transform.parent = sachet.transform;
        pot.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        pot.transform.localScale = new Vector3(0.14f, 0.05f, 0.14f); // 14cm Ø, 10cm haut
        pot.GetComponent<Renderer>().sharedMaterial = matPot;

        // 4) Tas de graines : sphère écrasée beige
        GameObject tas = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tas.name = "Tas";
        tas.transform.parent = sachet.transform;
        tas.transform.localPosition = new Vector3(0f, 0.11f, 0f);
        tas.transform.localScale = new Vector3(0.22f, 0.06f, 0.22f);
        tas.GetComponent<Renderer>().sharedMaterial = matGraines;
        Object.DestroyImmediate(tas.GetComponent<Collider>());

        // 5) Script Source : cliquer = prendre une graine
        Source source = sachet.AddComponent<Source>();
        source.typeItem = "graine";
        source.couleurEnMain = new Color(0.65f, 0.48f, 0.22f);
        source.tailleEnMain = 0.06f; // petite graine en main

        // 6) Collider global sur le parent pour faciliter le clic
        BoxCollider bc = sachet.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 0.07f, 0f);
        bc.size = new Vector3(0.24f, 0.16f, 0.24f);

        Selection.activeGameObject = sachet;
        Debug.Log("✅ SachetGraines créé à l'origine. Pose-le sur la table. " +
                  "Cliquer dessus = prendre une graine.");
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
