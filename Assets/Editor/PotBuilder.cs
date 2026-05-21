// PotBuilder.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Créer Pot vide" : instancie dans la scène le modèle 3D
// ClayPot.fbx (Assets/Resources/Tools/ClayPot.fbx) — modèle réaliste importé —
// et y ajoute :
//   - 2 enfants Terre / Eau (cubes désactivés au start, activés quand le joueur
//     remplit/arrose le pot)
//   - le script Pot avec ses références
//
// L'objet est créé sous le nom "Pot", posé sur Y=0, dimensionné à ~30 cm de haut.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class PotBuilder
{
    const string CHEMIN_FBX = "Assets/Resources/Tools/ClayPot.fbx";

    [MenuItem("Tools/Créer Pot vide")]
    public static void CreerPot()
    {
        // 1) Charge le modèle 3D
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CHEMIN_FBX);
        if (fbx == null)
        {
            EditorUtility.DisplayDialog("Modèle introuvable",
                "ClayPot.fbx introuvable à " + CHEMIN_FBX, "OK");
            return;
        }

        // 2) Instancie le FBX dans la scène
        GameObject pot = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        pot.name = "Pot";
        pot.transform.position = Vector3.zero;

        // 3) Auto-scale : on cible une hauteur de ~30 cm + pose la base à Y=0
        Renderer[] rs = pot.GetComponentsInChildren<Renderer>();
        Bounds bw = new Bounds();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            if (b.size.y > 0.001f)
                pot.transform.localScale = Vector3.one * (0.30f / b.size.y);

            bw = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) bw.Encapsulate(rs[i].bounds);
            pot.transform.position += new Vector3(0, -bw.min.y, 0);

            // Recalcule les bounds après reposition pour les enfants Terre/Eau
            bw = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) bw.Encapsulate(rs[i].bounds);
        }
        else
        {
            bw = new Bounds(pot.transform.position + Vector3.up * 0.15f, new Vector3(0.3f, 0.3f, 0.3f));
        }

        // 4) Matériaux pour Terre, Graine, Eau
        Material matTerre = ChargerOuCreerMateriau("Earth",
            new Color(0.22f, 0.13f, 0.07f), metallique: 0f, brillance: 0.04f);
        Material matGraine = ChargerOuCreerMateriau("Sprout",
            new Color(0.30f, 0.65f, 0.30f), metallique: 0f, brillance: 0.20f);
        Material matEau = ChargerOuCreerMateriau("Water",
            new Color(0.25f, 0.55f, 0.85f), metallique: 0.2f, brillance: 0.95f);

        // 5) Enfant "Terre" — cylindre (rond) qui remplit l'intérieur du pot
        //    Un cylindre épouse la forme intérieure du pot et ne dépasse pas des bords.
        GameObject visuelTerre = CreerEnfantPrimitive(pot, "Terre", PrimitiveType.Cylinder, bw,
            offsetYRatio: 0.20f, taillXZ: 0.78f, taillY: 0.18f, mat: matTerre);

        // 6) Enfant "Graine" — petite pousse verte qui sort de la terre
        GameObject visuelGraine = CreerEnfantPrimitive(pot, "Graine", PrimitiveType.Cylinder, bw,
            offsetYRatio: 0.55f, taillXZ: 0.06f, taillY: 0.30f, mat: matGraine);

        // 7) Enfant "Eau" — disque fin (cylindre plat) posé sur la terre
        GameObject visuelEau = CreerEnfantPrimitive(pot, "Eau", PrimitiveType.Cylinder, bw,
            offsetYRatio: 0.40f, taillXZ: 0.72f, taillY: 0.02f, mat: matEau);

        // 8) Script Pot + références aux enfants
        Pot scriptPot = pot.AddComponent<Pot>();
        scriptPot.visuelTerre = visuelTerre;
        scriptPot.visuelGraine = visuelGraine;
        scriptPot.visuelEau = visuelEau;

        // 9) Collider global pour rendre le pot cliquable
        BoxCollider bc = pot.AddComponent<BoxCollider>();
        Vector3 ls = pot.transform.lossyScale;
        bc.center = pot.transform.InverseTransformPoint(bw.center);
        bc.size = new Vector3(bw.size.x / ls.x, bw.size.y / ls.y, bw.size.z / ls.z);

        Selection.activeGameObject = pot;
        Debug.Log("✅ Pot vide créé à l'origine. Déplace-le où tu veux. " +
                  "Clic avec terre en main → rempli. Puis clic avec arrosoir → arrosé.");
    }

    static GameObject CreerEnfantPrimitive(GameObject parent, string nom, PrimitiveType type, Bounds wb,
                                            float offsetYRatio, float taillXZ, float taillY, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = nom;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent.transform);

        Vector3 worldPos = new Vector3(wb.center.x,
                                       wb.center.y + wb.size.y * offsetYRatio,
                                       wb.center.z);
        go.transform.position = worldPos;

        // NB : un Cylinder Unity a une hauteur "native" de 2 (de -1 à +1 sur Y),
        // donc taillY est divisé par 2 si type est Cylinder pour matcher l'intention.
        float facteurY = (type == PrimitiveType.Cylinder) ? 0.5f : 1f;
        Vector3 ls = parent.transform.lossyScale;
        go.transform.localScale = new Vector3(
            (wb.size.x * taillXZ) / Mathf.Max(0.001f, ls.x),
            (wb.size.y * taillY * facteurY) / Mathf.Max(0.001f, ls.y),
            (wb.size.z * taillXZ) / Mathf.Max(0.001f, ls.z));

        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
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
