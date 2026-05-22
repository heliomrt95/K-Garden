// AlignementTerrain.cs
// -----------------------------------------------------------------------------
// Outils pour gérer les DÉCORATIONS EXTÉRIEURES (arbres, herbe, rochers,
// clôtures, plantes décoratives…). Tout passe par un parent unique dans la
// Hierarchy : "EnvironmentDecorations". Les objets du gameplay (pots, outils,
// graines, sachets, plantes interactives) ne sont JAMAIS touchés.
//
// Menus disponibles (Tools > Décorations > …) :
//   - Créer Parent EnvironmentDecorations    : crée le parent vide
//   - Mettre Sélection sous EnvironmentDecorations
//                                            : reparent la sélection sous le parent
//                                              (crée le parent s'il n'existe pas)
//   - Aligner Décorations sur Sol            : aligne TOUS les enfants du parent
//                                              sur la surface du sol (raycast)
//
// L'alignement utilise un raycast vers le bas → marche avec n'importe quel sol
// (Plane, Terrain, mesh custom). Le pivot est compensé pour ne pas enterrer
// les objets à moitié.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

public static class AlignementTerrain
{
    public const string NomParent = "EnvironmentDecorations";

    // ── Création du parent ───────────────────────────────────────────────────
    [MenuItem("Tools/Décorations/Créer Parent EnvironmentDecorations")]
    public static GameObject CreerParent()
    {
        GameObject parent = GameObject.Find(NomParent);
        if (parent != null)
        {
            Debug.Log("[Décorations] Parent déjà présent.");
            Selection.activeGameObject = parent;
            return parent;
        }
        parent = new GameObject(NomParent);
        parent.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(parent, "Créer EnvironmentDecorations");
        Selection.activeGameObject = parent;
        Debug.Log("[Décorations] Parent '" + NomParent + "' créé à l'origine.");
        return parent;
    }

    // ── Reparente la sélection sous EnvironmentDecorations ───────────────────
    [MenuItem("Tools/Décorations/Mettre Sélection sous EnvironmentDecorations")]
    public static void ReparentSelection()
    {
        if (Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Sélection vide",
                "Sélectionne d'abord les décorations à reparenter.", "OK");
            return;
        }

        GameObject parent = GameObject.Find(NomParent) ?? CreerParent();

        int n = 0;
        foreach (var go in Selection.gameObjects)
        {
            if (go == parent) continue;
            // Préserve la position monde
            Undo.SetTransformParent(go.transform, parent.transform, "Reparent");
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        EditorUtility.DisplayDialog("Décorations",
            n + " objet(s) reparenté(s) sous '" + NomParent + "'.", "OK");
    }

    // ── Alignement de TOUS les enfants de EnvironmentDecorations ─────────────
    [MenuItem("Tools/Décorations/Aligner Décorations sur Sol")]
    public static void AlignerDecorations()
    {
        GameObject parent = GameObject.Find(NomParent);
        if (parent == null)
        {
            EditorUtility.DisplayDialog("Parent introuvable",
                "Aucun '" + NomParent + "' dans la scène.\n\n" +
                "1) Tools > Décorations > Créer Parent EnvironmentDecorations\n" +
                "2) Sélectionne tes décorations et fais 'Mettre Sélection sous…'\n" +
                "3) Relance cet alignement.", "OK");
            return;
        }

        int aligned = 0, rates = 0;
        // On parcourt UNIQUEMENT les enfants du parent dédié — aucun risque
        // de toucher au gameplay (pots, outils, sachets, plantes interactives).
        foreach (Transform t in parent.transform)
        {
            if (AlignerSurSol(t.gameObject)) aligned++;
            else rates++;
        }
        if (aligned > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        EditorUtility.DisplayDialog("Alignement Décorations",
            aligned + " décoration(s) alignée(s) sur le sol." +
            (rates > 0 ? "\n" + rates + " sans sol trouvé en dessous (ignorée(s))." : ""), "OK");
    }

    // ── Aligne une sélection ponctuelle (sans contrainte de parent) ──────────
    // Utile pour ajuster un objet précis avant de le ranger dans le parent.
    [MenuItem("Tools/Décorations/Aligner Sélection sur Sol (manuel)")]
    public static void AlignerSelection()
    {
        int aligned = 0;
        foreach (var go in Selection.gameObjects)
            if (AlignerSurSol(go)) aligned++;
        if (aligned > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[Décorations] " + aligned + " objet(s) sélectionné(s) aligné(s).");
    }

    // ── Cœur : raycast vers le bas, compensation du pivot ────────────────────
    static bool AlignerSurSol(GameObject go)
    {
        if (go == null) return false;

        Vector3 pos = go.transform.position;

        // Désactive temporairement les colliders de la déco
        Collider[] colliders = go.GetComponentsInChildren<Collider>();
        bool[] etats = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            etats[i] = colliders[i].enabled;
            colliders[i].enabled = false;
        }

        Vector3 origine = new Vector3(pos.x, pos.y + 50f, pos.z);
        bool touche = Physics.Raycast(origine, Vector3.down, out RaycastHit hit, 200f);

        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = etats[i];

        if (!touche)
        {
            Debug.LogWarning("[Décorations] '" + go.name + "' : aucun sol trouvé sous l'objet.");
            return false;
        }

        // Compense le pivot pour que la base touche le sol
        float pivotOffset = 0f;
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            pivotOffset = pos.y - b.min.y;
        }

        Vector3 nouvellePos = new Vector3(pos.x, hit.point.y + pivotOffset, pos.z);
        if ((nouvellePos - pos).sqrMagnitude < 0.0001f) return false;

        Undo.RecordObject(go.transform, "Aligner sur Sol");
        go.transform.position = nouvellePos;
        return true;
    }
}
