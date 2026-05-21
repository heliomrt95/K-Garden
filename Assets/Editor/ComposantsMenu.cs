// ComposantsMenu.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Composants" : prend le GameObject actuellement sélectionné
// dans la Hierarchy et lui ajoute le script gameplay correspondant + un
// Collider si manquant. Permet d'utiliser tes propres modèles 3D (téléchargés
// d'internet) au lieu des primitives générées par les builders.
//
// Workflow type :
//   1) Glisse ton modèle FBX/OBJ depuis Project dans la Scene
//   2) Sélectionne le GameObject dans la Hierarchy
//   3) Tools > Composants > Marquer [Pot / Pickup eau / Pickup spray / etc.]
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class ComposantsMenu
{
    // ── Pickup ────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Composants/Marquer comme Arrosoir (Pickup eau)")]
    public static void MarquerArrosoir()
    {
        AjouterPickup("eau",
            posMain:    new Vector3(0.35f, -0.25f, 0.55f),
            rotMain:    new Vector3(15f, -20f, 0f));
    }

    [MenuItem("Tools/Composants/Marquer comme Spray (Pickup spray)")]
    public static void MarquerSpray()
    {
        AjouterPickup("spray",
            posMain:    new Vector3(0.30f, -0.22f, 0.50f),
            rotMain:    new Vector3(0f, 180f, 0f));
    }

    // ── Source ────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Composants/Marquer comme Source de Terre")]
    public static void MarquerSourceTerre()
    {
        AjouterSource("terre", new Color(0.22f, 0.13f, 0.07f));
    }

    [MenuItem("Tools/Composants/Marquer comme Source de Graines")]
    public static void MarquerSourceGraines()
    {
        AjouterSource("graine", new Color(0.55f, 0.40f, 0.18f));
    }

    // ── Pot ───────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Composants/Marquer comme Pot")]
    public static void MarquerPot()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null) { Avertir("Sélectionne d'abord un GameObject."); return; }

        // 1) Ajoute le script Pot si manquant
        Pot pot = go.GetComponent<Pot>();
        if (pot == null) pot = go.AddComponent<Pot>();

        // 2) Collider obligatoire pour être cliquable
        AssureCollider(go);

        // 3) Crée les enfants Terre et Eau (cachés au start) s'ils n'existent pas
        Bounds wb = CalculerBounds(go);
        pot.visuelTerre = TrouverOuCreerEnfantCube(go, "Terre", wb,
            offsetYRatio: 0.20f, taillXZ: 0.78f, taillY: 0.40f,
            couleur: new Color(0.22f, 0.13f, 0.07f), nomMateriau: "Earth");
        pot.visuelGraine = TrouverOuCreerEnfantCube(go, "Graine", wb,
            offsetYRatio: 0.55f, taillXZ: 0.06f, taillY: 0.60f,
            couleur: new Color(0.30f, 0.65f, 0.30f), nomMateriau: "Sprout");
        pot.visuelEau = TrouverOuCreerEnfantCube(go, "Eau", wb,
            offsetYRatio: 0.40f, taillXZ: 0.72f, taillY: 0.05f,
            couleur: new Color(0.25f, 0.55f, 0.85f), nomMateriau: "Water");

        EditorUtility.SetDirty(go);
        Debug.Log("[Composants] '" + go.name + "' est maintenant un Pot (Terre + Eau enfants cachés).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers internes
    // ─────────────────────────────────────────────────────────────────────────

    static void AjouterPickup(string typeItem, Vector3 posMain, Vector3 rotMain)
    {
        GameObject go = Selection.activeGameObject;
        if (go == null) { Avertir("Sélectionne d'abord un GameObject."); return; }

        Pickup p = go.GetComponent<Pickup>();
        if (p == null) p = go.AddComponent<Pickup>();
        p.typeItem = typeItem;
        p.positionEnMain = posMain;
        p.rotationEnMain = rotMain;

        AssureCollider(go);
        EditorUtility.SetDirty(go);
        Debug.Log("[Composants] '" + go.name + "' est maintenant un Pickup (type=" + typeItem + ").");
    }

    static void AjouterSource(string typeItem, Color couleurEnMain)
    {
        GameObject go = Selection.activeGameObject;
        if (go == null) { Avertir("Sélectionne d'abord un GameObject."); return; }

        Source s = go.GetComponent<Source>();
        if (s == null) s = go.AddComponent<Source>();
        s.typeItem = typeItem;
        s.couleurEnMain = couleurEnMain;

        AssureCollider(go);
        EditorUtility.SetDirty(go);
        Debug.Log("[Composants] '" + go.name + "' est maintenant une Source (type=" + typeItem + ").");
    }

    // ── Crée (ou retrouve) un enfant Cylindre (forme ronde) avec position/scale
    //    calculés sur les bounds parent. Un cylindre ne dépasse pas des bords ronds d'un pot.
    static GameObject TrouverOuCreerEnfantCube(GameObject parent, string nom, Bounds wb,
                                                float offsetYRatio, float taillXZ, float taillY,
                                                Color couleur, string nomMateriau)
    {
        Transform existing = parent.transform.Find(nom);
        if (existing != null) return existing.gameObject;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = nom;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent.transform);

        // Position monde : au centre des bounds, décalée vers le haut
        Vector3 worldPos = new Vector3(wb.center.x,
                                       wb.center.y + wb.size.y * offsetYRatio,
                                       wb.center.z);
        go.transform.position = worldPos;

        // Scale local en compensant le scale du parent. Le cylindre Unity a une
        // hauteur native de 2 unités (de -1 à +1 sur Y), d'où le facteur 0.5 sur Y.
        Vector3 ls = parent.transform.lossyScale;
        go.transform.localScale = new Vector3(
            (wb.size.x * taillXZ) / Mathf.Max(0.001f, ls.x),
            (wb.size.y * taillY * 0.5f) / Mathf.Max(0.001f, ls.y),
            (wb.size.z * taillXZ) / Mathf.Max(0.001f, ls.z));

        go.GetComponent<Renderer>().sharedMaterial = ChargerOuCreerMateriau(nomMateriau, couleur);
        return go;
    }

    static Bounds CalculerBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.3f);
        Bounds wb = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) wb.Encapsulate(rs[i].bounds);
        return wb;
    }

    static void AssureCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null) return;

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            var b = go.AddComponent<BoxCollider>();
            b.size = Vector3.one * 0.3f;
            return;
        }
        Bounds wb = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) wb.Encapsulate(renderers[i].bounds);

        Vector3 ls = go.transform.lossyScale;
        var bc = go.AddComponent<BoxCollider>();
        bc.center = go.transform.InverseTransformPoint(wb.center);
        bc.size = new Vector3(
            Mathf.Abs(wb.size.x / Mathf.Max(0.001f, ls.x)),
            Mathf.Abs(wb.size.y / Mathf.Max(0.001f, ls.y)),
            Mathf.Abs(wb.size.z / Mathf.Max(0.001f, ls.z)));
    }

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

    static void Avertir(string msg)
    {
        EditorUtility.DisplayDialog("Composants", msg, "OK");
    }
}
