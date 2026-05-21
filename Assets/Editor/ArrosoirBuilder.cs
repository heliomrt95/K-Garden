// ArrosoirBuilder.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Créer Arrosoir" : instancie dans la scène le modèle 3D
// WateringCan.fbx (Assets/Resources/Tools/WateringCan.fbx), applique un
// matériau vert sur tout le corps, puis ajoute un disque d'eau bleu visible
// par l'orifice du haut. Attache automatiquement le script Pickup (type="eau").
//
// L'objet est créé sous le nom "Arrosoir", posé sur Y=0, ~25 cm de haut.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class ArrosoirBuilder
{
    const string CHEMIN_FBX = "Assets/Resources/Tools/WateringCan.fbx";

    [MenuItem("Tools/Créer Arrosoir")]
    public static void CreerArrosoir()
    {
        // 1) Charge le modèle 3D
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CHEMIN_FBX);
        if (fbx == null)
        {
            EditorUtility.DisplayDialog("Modèle introuvable",
                "WateringCan.fbx introuvable à " + CHEMIN_FBX, "OK");
            return;
        }

        // 2) Instancie le FBX dans la scène
        GameObject arrosoir = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        arrosoir.name = "Arrosoir";
        arrosoir.transform.position = Vector3.zero;

        // 3) Auto-scale : on cible une hauteur de ~25 cm + pose la base à Y=0
        Renderer[] rs = arrosoir.GetComponentsInChildren<Renderer>();
        Bounds bw = new Bounds();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            if (b.size.y > 0.001f)
                arrosoir.transform.localScale = Vector3.one * (0.25f / b.size.y);

            bw = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) bw.Encapsulate(rs[i].bounds);
            arrosoir.transform.position += new Vector3(0, -bw.min.y, 0);

            // Recalcule après reposition pour les enfants Eau
            bw = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) bw.Encapsulate(rs[i].bounds);
        }

        // 4) Applique le matériau vert (ArrosoirCorps) à tous les Renderer du modèle
        Material vert = ChargerOuCreerMateriau("ArrosoirCorps",
            new Color(0.20f, 0.55f, 0.30f), metallique: 0.3f, brillance: 0.55f);
        foreach (Renderer rend in arrosoir.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = vert;
            rend.sharedMaterials = mats;
        }

        // 5) Ajoute un disque d'eau visible par l'orifice du haut (le réservoir)
        //    On utilise le matériau Water.mat (eau transparente brillante)
        Material eau = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Water.mat");
        if (eau == null)
            eau = ChargerOuCreerMateriau("Water", new Color(0.25f, 0.55f, 0.85f), metallique: 0.2f, brillance: 0.9f);

        GameObject eauDisque = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        eauDisque.name = "Eau";
        Object.DestroyImmediate(eauDisque.GetComponent<Collider>());
        eauDisque.transform.SetParent(arrosoir.transform);

        // Position : au sommet de l'arrosoir, légèrement enfoncé
        Vector3 ls = arrosoir.transform.lossyScale;
        Vector3 worldPosEau = new Vector3(
            bw.center.x,
            bw.max.y - bw.size.y * 0.06f,
            bw.center.z);
        eauDisque.transform.position = worldPosEau;

        // Taille : un peu plus petit que le diamètre du corps, très plat
        float diametreLocal = Mathf.Min(bw.size.x, bw.size.z) * 0.55f;
        eauDisque.transform.localScale = new Vector3(
            diametreLocal / Mathf.Max(0.001f, ls.x),
            (bw.size.y * 0.02f) / Mathf.Max(0.001f, ls.y),
            diametreLocal / Mathf.Max(0.001f, ls.z));
        eauDisque.GetComponent<Renderer>().sharedMaterial = eau;

        // 6) Script Pickup (type="eau") pour rendre l'arrosoir ramassable
        Pickup pickup = arrosoir.AddComponent<Pickup>();
        pickup.typeItem = "eau";
        pickup.positionEnMain = new Vector3(0.35f, -0.25f, 0.55f);
        pickup.rotationEnMain = new Vector3(15f, -45f, 0f);

        // 7) Collider global pour faciliter le clic
        BoxCollider bc = arrosoir.AddComponent<BoxCollider>();
        bc.center = arrosoir.transform.InverseTransformPoint(bw.center);
        bc.size = new Vector3(bw.size.x / ls.x, bw.size.y / ls.y, bw.size.z / ls.z);

        Selection.activeGameObject = arrosoir;
        Debug.Log("✅ Arrosoir vert créé à l'origine — avec disque d'eau visible. Ramassable (type=eau).");
    }

    static Material ChargerOuCreerMateriau(string nom, Color couleur,
                                            float metallique = 0f, float brillance = 0.2f)
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
