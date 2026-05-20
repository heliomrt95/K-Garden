// SerreBuilder.cs
// -----------------------------------------------------------------------------
// Ajoute un menu "Tools > Créer Serre" qui construit une serre simple :
//   - un sol en béton
//   - 3 murs en verre transparent (le 4e côté est laissé ouvert = entrée)
//   - un toit en verre
//
// Tout est posé à l'origine (0,0,0), serre de 6m × 8m × 3m.
// Les matériaux Glass et Concrete sont créés dans Assets/Resources/Materials/
// s'ils n'existent pas déjà.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class SerreBuilder
{
    [MenuItem("Tools/Créer Serre")]
    public static void CreerSerre()
    {
        // 1) Matériaux : béton opaque + verre transparent
        Material beton = ChargerOuCreerMateriau("Concrete",
            new Color(0.78f, 0.78f, 0.75f, 1f), metallique: 0f, brillance: 0.15f, transparent: false);

        Material verre = ChargerOuCreerMateriau("Glass",
            new Color(0.85f, 0.92f, 0.95f, 0.25f), metallique: 0f, brillance: 0.8f, transparent: true);

        // 2) Parent vide
        GameObject serre = new GameObject("Serre");
        serre.transform.position = Vector3.zero;

        // 3) Sol en béton (6m × 8m, posé à y ≈ 0)
        CreerPiece("Sol_Beton",
            pos:   new Vector3(0f, 0.05f, 0f),
            scale: new Vector3(6f, 0.1f, 8f),
            mat:   beton, parent: serre.transform);

        // 4) Murs en verre — 3 côtés, côté Sud (Z-) laissé ouvert comme entrée
        CreerPiece("Mur_Nord",   // face arrière (Z+)
            pos:   new Vector3(0f, 1.5f, 4f),
            scale: new Vector3(6f, 3f, 0.1f),
            mat:   verre, parent: serre.transform);

        CreerPiece("Mur_Est",    // côté droit (X+)
            pos:   new Vector3(3f, 1.5f, 0f),
            scale: new Vector3(0.1f, 3f, 8f),
            mat:   verre, parent: serre.transform);

        CreerPiece("Mur_Ouest",  // côté gauche (X-)
            pos:   new Vector3(-3f, 1.5f, 0f),
            scale: new Vector3(0.1f, 3f, 8f),
            mat:   verre, parent: serre.transform);

        // 5) Toit en verre plat
        CreerPiece("Toit",
            pos:   new Vector3(0f, 3.05f, 0f),
            scale: new Vector3(6.2f, 0.1f, 8.2f),
            mat:   verre, parent: serre.transform);

        Selection.activeGameObject = serre;
        Debug.Log("✅ Serre créée à l'origine (0,0,0) — 6m × 8m × 3m. Entrée côté Z négatif.");
    }

    // ── Crée un Cube enfant ────────────────────────────────────────────────────
    static void CreerPiece(string nom, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = nom;
        piece.transform.parent = parent;
        piece.transform.localPosition = pos;
        piece.transform.localScale = scale;
        piece.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // ── Charge un matériau ou en crée un nouveau (gère opaque + transparent) ──
    static Material ChargerOuCreerMateriau(string nom, Color couleur,
                                            float metallique, float brillance, bool transparent)
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

        if (transparent)
        {
            // Configuration "Transparent" du shader Standard (pour le verre)
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

        AssetDatabase.CreateAsset(mat, chemin);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
