// BacATerreBuilder.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Créer Bac à Terre" : instancie dans la scène le modèle 3D
// PlanterBox.fbx (Assets/Resources/Tools/PlanterBox.fbx) — modèle réaliste
// importé — et lui attache automatiquement le script Source (type="terre")
// pour permettre au joueur de venir prendre des mottes de terre dessus.
//
// L'objet est créé sous le nom "BacATerre", posé sur Y=0, ~1m de large.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

public static class BacATerreBuilder
{
    const string CHEMIN_FBX = "Assets/Resources/Tools/PlanterBox.fbx";

    [MenuItem("Tools/Créer Bac à Terre")]
    public static void CreerBac()
    {
        // 1) Charge le modèle 3D
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CHEMIN_FBX);
        if (fbx == null)
        {
            EditorUtility.DisplayDialog("Modèle introuvable",
                "PlanterBox.fbx introuvable à " + CHEMIN_FBX, "OK");
            return;
        }

        // 2) Instancie le FBX dans la scène
        GameObject bac = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        bac.name = "BacATerre";
        bac.transform.position = Vector3.zero;

        // 3) Auto-scale : on cible une largeur de ~1m + pose la base à Y=0
        Renderer[] rs = bac.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            float maxXZ = Mathf.Max(b.size.x, b.size.z);
            if (maxXZ > 0.001f)
                bac.transform.localScale = Vector3.one * (1.0f / maxXZ);

            Bounds b2 = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b2.Encapsulate(rs[i].bounds);
            bac.transform.position += new Vector3(0, -b2.min.y, 0);
        }

        // 4) Script Source : cliquer dessus donne une motte de terre dans la main
        Source source = bac.AddComponent<Source>();
        source.typeItem = "terre";
        source.couleurEnMain = new Color(0.28f, 0.18f, 0.10f);

        // 5) Collider global pour rendre le bac cliquable
        BoxCollider bc = bac.AddComponent<BoxCollider>();
        if (rs.Length > 0)
        {
            Bounds bw = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) bw.Encapsulate(rs[i].bounds);
            Vector3 ls = bac.transform.lossyScale;
            bc.center = bac.transform.InverseTransformPoint(bw.center);
            bc.size = new Vector3(bw.size.x / ls.x, bw.size.y / ls.y, bw.size.z / ls.z);
        }

        Selection.activeGameObject = bac;
        Debug.Log("✅ BacATerre créé à l'origine. Cliquer dessus = prendre une motte de terre.");
    }
}
