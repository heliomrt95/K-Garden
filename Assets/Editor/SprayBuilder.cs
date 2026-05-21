// SprayBuilder.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Créer Spray Rouge" : instancie dans la scène le modèle 3D
// Sprayer.fbx (Assets/Resources/Tools/Sprayer.fbx) — celui issu de l'archive
// 15-sprayer.zip — avec ses 2 textures :
//   - Spray_Body.png → corps de la bouteille (teinté rouge)
//   - Spray_Top.png  → pompe/embout (couleur neutre conservée)
//
// L'objet est créé sous le nom "SprayRouge", posé sur Y=0, ~25 cm de haut.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class SprayBuilder
{
    const string CHEMIN_FBX      = "Assets/Resources/Tools/Sprayer.fbx";
    const string CHEMIN_TEX_BODY = "Assets/Resources/Tools/Spray_Body.png";
    const string CHEMIN_TEX_TOP  = "Assets/Resources/Tools/Spray_Top.png";

    [MenuItem("Tools/Créer Spray Rouge")]
    public static void CreerSprayRouge()
    {
        // 1) Charge le modèle 3D
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CHEMIN_FBX);
        if (fbx == null)
        {
            EditorUtility.DisplayDialog("Modèle introuvable",
                "Sprayer.fbx introuvable à " + CHEMIN_FBX, "OK");
            return;
        }

        // 2) Instancie le FBX dans la scène
        GameObject spray = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        spray.name = "SprayRouge";
        spray.transform.position = Vector3.zero;

        // 3) Crée (ou charge) les 2 matériaux
        Material matBody = ChargerOuCreerMateriau("Spray_Body_Rouge",
            CHEMIN_TEX_BODY, tint: new Color(0.85f, 0.15f, 0.15f), brillance: 0.5f);
        Material matTop  = ChargerOuCreerMateriau("Spray_Top",
            CHEMIN_TEX_TOP,  tint: Color.white,                   brillance: 0.6f);

        // 4) Le FBX a typiquement 2 slots de matériau : on assigne Body au 1er,
        //    Top au 2e. Si plus de slots, on remplit avec Body.
        foreach (Renderer rend in spray.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = (i == 1) ? matTop : matBody;
            rend.sharedMaterials = mats;
        }

        // 5) Auto-scale : on cible une hauteur de ~25 cm
        Renderer[] rs = spray.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            if (b.size.y > 0.001f)
                spray.transform.localScale = Vector3.one * (0.25f / b.size.y);

            // Recalcule après scale et pose la base à Y=0
            Bounds b2 = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b2.Encapsulate(rs[i].bounds);
            spray.transform.position += new Vector3(0, -b2.min.y, 0);
        }

        // Auto-ajoute le script Pickup (type="spray") pour rendre le spray ramassable
        Pickup pickup = spray.AddComponent<Pickup>();
        pickup.typeItem = "spray";
        pickup.positionEnMain = new Vector3(0.30f, -0.22f, 0.50f);
        pickup.rotationEnMain = new Vector3(0f, 90f, 0f);

        // Collider global sur le parent pour faciliter le clic
        BoxCollider bc = spray.AddComponent<BoxCollider>();
        // Recalcule la taille du collider à partir des bounds après scale
        if (rs.Length > 0)
        {
            Bounds bw = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) bw.Encapsulate(rs[i].bounds);
            Vector3 ls = spray.transform.lossyScale;
            bc.center = spray.transform.InverseTransformPoint(bw.center);
            bc.size = new Vector3(bw.size.x / ls.x, bw.size.y / ls.y, bw.size.z / ls.z);
        }

        Selection.activeGameObject = spray;
        Debug.Log("✅ SprayRouge créé à l'origine — ramassable (type=spray). Déplace-le sur la table.");
    }

    // ── Charge un matériau existant, ou en crée un nouveau ────────────────────
    static Material ChargerOuCreerMateriau(string nom, string cheminTexture,
                                            Color tint, float brillance)
    {
        string dossier = "Assets/Resources/Materials";
        string chemin = dossier + "/" + nom + ".mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(chemin);
        if (mat != null) return mat;

        if (!Directory.Exists(dossier)) Directory.CreateDirectory(dossier);
        mat = new Material(Shader.Find("Standard"));
        mat.color = tint;
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(cheminTexture);
        if (tex != null) mat.mainTexture = tex;
        mat.SetFloat("_Metallic", 0.1f);
        mat.SetFloat("_Glossiness", brillance);
        AssetDatabase.CreateAsset(mat, chemin);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
