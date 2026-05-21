// PotBuilder.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Créer Pot vide" : construit un petit pot terracotta vide
// (cylindre creux visuel) avec 2 enfants cachés (Terre, Eau) qui s'activeront
// quand le joueur remplira le pot.
//
// Structure créée :
//   Pot (Cylindre creux + script Pot)
//   ├── Terre  (Cube — désactivé au start, activé quand terre versée)
//   └── Eau    (Cube fin bleu — désactivé au start, activé quand pot arrosé)
//
// Posé à l'origine, dimensions ~30 cm de haut. À déplacer dans la scène.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class PotBuilder
{
    [MenuItem("Tools/Créer Pot vide")]
    public static void CreerPot()
    {
        // 1) Matériaux
        Material terracotta = ChargerOuCreerMateriau("Terracotta",
            new Color(0.62f, 0.32f, 0.18f), metallique: 0f, brillance: 0.10f);
        Material terre = ChargerOuCreerMateriau("Earth",
            new Color(0.28f, 0.18f, 0.10f), metallique: 0f, brillance: 0.05f);
        Material eau = ChargerOuCreerMateriau("Water",
            new Color(0.30f, 0.55f, 0.85f), metallique: 0.1f, brillance: 0.85f);

        // 2) Corps du pot — un cylindre épais
        GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pot.name = "Pot";
        pot.transform.position = Vector3.zero;
        pot.transform.localScale = new Vector3(0.3f, 0.15f, 0.3f); // ~30cm large, 30cm haut
        pot.GetComponent<Renderer>().sharedMaterial = terracotta;

        // Le cylindre a son pivot au centre. On le remonte pour que la base soit à Y=0.
        pot.transform.position = new Vector3(0f, 0.15f, 0f);

        // 3) Enfant "Terre" — un cube qui remplit l'intérieur du pot
        // Position légèrement plus basse que le rebord du pot
        GameObject visuelTerre = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visuelTerre.name = "Terre";
        visuelTerre.transform.parent = pot.transform;
        // Compense le scale du parent (0.3, 0.15, 0.3) pour avoir une taille fixe
        visuelTerre.transform.localScale = new Vector3(0.85f, 0.7f, 0.85f);
        visuelTerre.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        visuelTerre.GetComponent<Renderer>().sharedMaterial = terre;
        Object.DestroyImmediate(visuelTerre.GetComponent<Collider>());

        // 4) Enfant "Eau" — un disque fin posé sur la terre
        GameObject visuelEau = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visuelEau.name = "Eau";
        visuelEau.transform.parent = pot.transform;
        visuelEau.transform.localScale = new Vector3(0.78f, 0.05f, 0.78f);
        visuelEau.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        visuelEau.GetComponent<Renderer>().sharedMaterial = eau;
        Object.DestroyImmediate(visuelEau.GetComponent<Collider>());

        // 5) Script Pot + références aux enfants
        Pot scriptPot = pot.AddComponent<Pot>();
        scriptPot.visuelTerre = visuelTerre;
        scriptPot.visuelEau = visuelEau;

        Selection.activeGameObject = pot;
        Debug.Log("✅ Pot vide créé à l'origine. Déplace-le où tu veux. " +
                  "Clic avec terre en main → rempli. Puis clic avec arrosoir → arrosé.");
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
