// BacATerreBuilder.cs
// -----------------------------------------------------------------------------
// Ajoute un menu "Tools > Créer Bac à Terre" qui construit dans la scène
// courante un bac en bois avec de la terre à l'intérieur.
//
// Structure créée :
//   BacATerre (Empty)
//   ├── Bord_Avant   (Cube)  → matériau Wood
//   ├── Bord_Arrière (Cube)  → matériau Wood
//   ├── Bord_Gauche  (Cube)  → matériau Wood
//   ├── Bord_Droit   (Cube)  → matériau Wood
//   └── Terre        (Cube)  → matériau Earth
//
// Les matériaux Wood et Earth sont créés dans Assets/Resources/Materials/
// s'ils n'existent pas déjà.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class BacATerreBuilder
{
    [MenuItem("Tools/Créer Bac à Terre")]
    public static void CreerBac()
    {
        // 1) Récupère (ou crée) les 2 matériaux
        Material wood  = ChargerOuCreerMateriau("Wood",  new Color(0.45f, 0.28f, 0.15f));
        Material earth = ChargerOuCreerMateriau("Earth", new Color(0.28f, 0.18f, 0.10f));

        // 2) Crée le parent vide
        GameObject bac = new GameObject("BacATerre");
        bac.transform.position = Vector3.zero;

        // 3) Crée les 4 bords (en bois)
        CreerPiece("Bord_Avant",   new Vector3( 0f,    0.20f,  0.5f),  new Vector3(1f,    0.4f, 0.05f), wood, bac.transform);
        CreerPiece("Bord_Arriere", new Vector3( 0f,    0.20f, -0.5f),  new Vector3(1f,    0.4f, 0.05f), wood, bac.transform);
        CreerPiece("Bord_Gauche",  new Vector3(-0.5f,  0.20f,  0f),    new Vector3(0.05f, 0.4f, 1f),    wood, bac.transform);
        CreerPiece("Bord_Droit",   new Vector3( 0.5f,  0.20f,  0f),    new Vector3(0.05f, 0.4f, 1f),    wood, bac.transform);

        // 4) Crée la terre à l'intérieur (un cube plat un peu plus petit que le bac)
        CreerPiece("Terre", new Vector3(0f, 0.15f, 0f), new Vector3(0.95f, 0.3f, 0.95f), earth, bac.transform);

        // 5) Sélectionne le bac dans la Hierarchy pour que l'utilisateur le voie
        Selection.activeGameObject = bac;
        Debug.Log("✅ BacATerre créé à l'origine (0,0,0). Déplace-le où tu veux dans la scène.");
    }

    // ── Crée un Cube enfant avec position/scale/matériau donnés ─────────────────
    static void CreerPiece(string nom, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = nom;
        piece.transform.parent = parent;
        piece.transform.localPosition = pos;
        piece.transform.localScale = scale;
        piece.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // ── Charge un matériau existant, ou en crée un nouveau s'il n'existe pas ──
    static Material ChargerOuCreerMateriau(string nom, Color couleur)
    {
        string dossier = "Assets/Resources/Materials";
        string chemin = dossier + "/" + nom + ".mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(chemin);
        if (mat != null) return mat;

        if (!Directory.Exists(dossier)) Directory.CreateDirectory(dossier);
        mat = new Material(Shader.Find("Standard"));
        mat.color = couleur;
        AssetDatabase.CreateAsset(mat, chemin);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
