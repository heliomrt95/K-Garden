// GraineBuilder.cs
// -----------------------------------------------------------------------------
// 3 menus pour créer des sachets de graines, un par niveau de progression :
//   - Tools > Sachets de Graines > Niveau 1  (typeItem = "graine_1")
//   - Tools > Sachets de Graines > Niveau 2  (typeItem = "graine_2")
//   - Tools > Sachets de Graines > Niveau 3  (typeItem = "graine_3")
//
// Chaque sachet est posé à l'origine sous le nom SachetGraines_N1/N2/N3, avec
// une couleur différente. Au clic en jeu, le joueur prend une graine de ce
// niveau qu'il peut planter dans un pot.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class GraineBuilder
{
    [MenuItem("Tools/Sachets de Graines/Niveau 1")]
    public static void Creer1() { CreerSachet(1); }

    [MenuItem("Tools/Sachets de Graines/Niveau 2")]
    public static void Creer2() { CreerSachet(2); }

    [MenuItem("Tools/Sachets de Graines/Niveau 3")]
    public static void Creer3() { CreerSachet(3); }

    static void CreerSachet(int niveau)
    {
        // Couleurs selon le niveau (récipient + tas de graines)
        Color cPot, cGraines;
        switch (niveau)
        {
            case 2: cPot = new Color(0.40f, 0.30f, 0.55f); cGraines = new Color(0.60f, 0.45f, 0.85f); break;
            case 3: cPot = new Color(0.20f, 0.45f, 0.55f); cGraines = new Color(0.30f, 0.85f, 0.75f); break;
            default: cPot = new Color(0.55f, 0.35f, 0.20f); cGraines = new Color(0.65f, 0.48f, 0.22f); break;
        }

        // 1) Matériaux
        Material matPot = ChargerOuCreerMateriau("SachetPot_N" + niveau, cPot, 0.05f, 0.30f);
        Material matGraines = ChargerOuCreerMateriau("SachetGraines_N" + niveau, cGraines, 0f, 0.15f);

        // 2) Parent vide
        GameObject sachet = new GameObject("SachetGraines_N" + niveau);
        sachet.transform.position = Vector3.zero;

        // 3) Récipient : petit cylindre
        GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pot.name = "Pot";
        pot.transform.parent = sachet.transform;
        pot.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        pot.transform.localScale = new Vector3(0.14f, 0.05f, 0.14f);
        pot.GetComponent<Renderer>().sharedMaterial = matPot;

        // 4) Tas de graines : sphère écrasée
        GameObject tas = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tas.name = "Tas";
        tas.transform.parent = sachet.transform;
        tas.transform.localPosition = new Vector3(0f, 0.11f, 0f);
        tas.transform.localScale = new Vector3(0.22f, 0.06f, 0.22f);
        tas.GetComponent<Renderer>().sharedMaterial = matGraines;
        Object.DestroyImmediate(tas.GetComponent<Collider>());

        // 5) Source : clic = prendre une graine de ce niveau
        Source source = sachet.AddComponent<Source>();
        source.typeItem = "graine_" + niveau;
        source.couleurEnMain = cGraines;
        source.tailleEnMain = 0.06f;

        // 6) Collider global
        BoxCollider bc = sachet.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 0.07f, 0f);
        bc.size = new Vector3(0.24f, 0.16f, 0.24f);

        Selection.activeGameObject = sachet;
        Debug.Log("✅ SachetGraines_N" + niveau + " créé (typeItem=graine_" + niveau +
                  "). À glisser dans SeedUnlockManager pour les niveaux 2 et 3.");
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
