// ArrosoirBuilder.cs
// -----------------------------------------------------------------------------
// Ajoute un menu "Tools > Créer Arrosoir" qui construit dans la scène courante
// un arrosoir simple en assemblant des primitives Unity (cylindres, sphère, cubes).
//
// Structure créée :
//   Arrosoir (Empty)
//   ├── Corps        (Cylinder, gros)
//   ├── Bec          (Cylinder fin, incliné)
//   ├── Pomme        (Sphere au bout du bec)
//   ├── Anse_Gauche  (Cube fin vertical)
//   ├── Anse_Droite  (Cube fin vertical)
//   └── Anse_Haut    (Cube fin horizontal)
//
// Tout est en matériau Metal (gris brillant), créé dans Assets/Resources/Materials/
// si pas déjà présent.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class ArrosoirBuilder
{
    [MenuItem("Tools/Créer Arrosoir")]
    public static void CreerArrosoir()
    {
        // 1) Récupère (ou crée) le matériau métal
        Material metal = ChargerOuCreerMateriau("Metal",
            new Color(0.72f, 0.74f, 0.78f), metallique: 0.8f, brillance: 0.6f);

        // 2) Parent vide (sert à tout déplacer ensemble)
        GameObject arrosoir = new GameObject("Arrosoir");
        arrosoir.transform.position = Vector3.zero;

        // 3) Corps : gros cylindre vertical
        CreerPiece("Corps", PrimitiveType.Cylinder,
            pos:   new Vector3(0f, 0.2f, 0f),
            scale: new Vector3(0.4f, 0.2f, 0.4f),
            rot:   Quaternion.identity,
            mat:   metal, parent: arrosoir.transform);

        // 4) Bec : cylindre fin et incliné, qui sort sur le côté droit
        CreerPiece("Bec", PrimitiveType.Cylinder,
            pos:   new Vector3(0.25f, 0.35f, 0f),
            scale: new Vector3(0.05f, 0.2f, 0.05f),
            rot:   Quaternion.Euler(0f, 0f, -60f), // incliné vers le haut-droite
            mat:   metal, parent: arrosoir.transform);

        // 5) Pomme d'arrosoir : sphère au bout du bec
        CreerPiece("Pomme", PrimitiveType.Sphere,
            pos:   new Vector3(0.43f, 0.50f, 0f),
            scale: new Vector3(0.12f, 0.12f, 0.12f),
            rot:   Quaternion.identity,
            mat:   metal, parent: arrosoir.transform);

        // 6) Anse en "n" inversé : 2 supports verticaux + 1 barre horizontale
        CreerPiece("Anse_Gauche", PrimitiveType.Cube,
            pos:   new Vector3(-0.15f, 0.50f, 0f),
            scale: new Vector3(0.04f, 0.20f, 0.04f),
            rot:   Quaternion.identity,
            mat:   metal, parent: arrosoir.transform);

        CreerPiece("Anse_Droite", PrimitiveType.Cube,
            pos:   new Vector3(0.15f, 0.50f, 0f),
            scale: new Vector3(0.04f, 0.20f, 0.04f),
            rot:   Quaternion.identity,
            mat:   metal, parent: arrosoir.transform);

        CreerPiece("Anse_Haut", PrimitiveType.Cube,
            pos:   new Vector3(0f, 0.60f, 0f),
            scale: new Vector3(0.34f, 0.04f, 0.04f),
            rot:   Quaternion.identity,
            mat:   metal, parent: arrosoir.transform);

        // Auto-ajoute le script Pickup (type="eau") pour que l'arrosoir soit ramassable
        Pickup pickup = arrosoir.AddComponent<Pickup>();
        pickup.typeItem = "eau";
        pickup.positionEnMain = new Vector3(0.35f, -0.25f, 0.55f);
        pickup.rotationEnMain = new Vector3(15f, -20f, 0f);

        // Ajoute un BoxCollider sur le parent pour rendre l'arrosoir cliquable
        // (les enfants ont déjà leurs propres colliders, mais ce collider global facilite le clic)
        BoxCollider bc = arrosoir.AddComponent<BoxCollider>();
        bc.center = new Vector3(0.1f, 0.35f, 0f);
        bc.size = new Vector3(0.7f, 0.7f, 0.4f);

        Selection.activeGameObject = arrosoir;
        Debug.Log("✅ Arrosoir créé à l'origine (0,0,0) — ramassable (type=eau). Déplace-le sur la table.");
    }

    // ── Crée une primitive enfant avec ses paramètres ──────────────────────────
    static void CreerPiece(string nom, PrimitiveType type, Vector3 pos, Vector3 scale,
                           Quaternion rot, Material mat, Transform parent)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        piece.name = nom;
        piece.transform.parent = parent;
        piece.transform.localPosition = pos;
        piece.transform.localScale = scale;
        piece.transform.localRotation = rot;
        piece.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // ── Charge un matériau existant ou en crée un nouveau ─────────────────────
    static Material ChargerOuCreerMateriau(string nom, Color couleur,
                                            float metallique = 0f, float brillance = 0.1f)
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
