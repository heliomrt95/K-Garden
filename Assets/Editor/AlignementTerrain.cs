// AlignementTerrain.cs
// -----------------------------------------------------------------------------
// 2 menus pour replacer les décorations sur la surface du sol :
//
//   - "Tools > Aligner Sélection sur Sol"
//        Aligne uniquement les GameObjects sélectionnés dans la Hierarchy.
//
//   - "Tools > Aligner Toutes Décorations sur Sol"
//        Scanne tous les objets racines et aligne ceux qui ne sont pas dans
//        la liste d'exclusion (Player, Map, Serre, Pots, outils, managers…).
//
// L'alignement utilise un raycast vers le BAS depuis chaque déco. Ça marche
// donc avec n'importe quel sol :
//   - un Plane primitive (notre cas actuel)
//   - un Terrain Unity
//   - n'importe quel mesh avec un Collider
//
// Le pivot de l'objet est compensé : on positionne sa BASE sur le sol, pas
// son centre — un champignon ne sera donc pas à moitié enterré.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AlignementTerrain
{
    // Noms d'objets racines à NE PAS toucher en mode "Aligner Toutes"
    static readonly HashSet<string> NomsExclus = new HashSet<string>
    {
        "player", "camera", "main camera", "directional light",
        "map", "plane", "sol", "ground", "floor",
        "serre", "bacaterre", "table",
        "pot", "pot 1", "pot 2", "pot 3",
        "sachetgraines", "sachetgraines_n1", "sachetgraines_n2", "sachetgraines_n3",
        "arrosoir", "sprayeau", "spraypesticide", "spray", "pelle",
        "gamemanager",
    };

    [MenuItem("Tools/Aligner Sélection sur Sol")]
    public static void AlignerSelection()
    {
        int aligned = 0;
        foreach (var go in Selection.gameObjects)
        {
            if (AlignerSurSol(go)) aligned++;
        }
        if (aligned > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[Alignement] " + aligned + " objet(s) sélectionné(s) alignés sur le sol.");
    }

    [MenuItem("Tools/Aligner Toutes Décorations sur Sol")]
    public static void AlignerToutesDecorations()
    {
        int aligned = 0;
        int rates = 0;
        GameObject[] tous = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in tous)
        {
            if (!go.scene.IsValid()) continue;
            if (go.transform.parent != null) continue;          // racines seulement
            if (NomsExclus.Contains(go.name.ToLower())) continue;
            // Ignore les composants techniques (renderers vides, lumières, etc.)
            if (go.GetComponentInChildren<Renderer>() == null) continue;

            bool ok = AlignerSurSol(go);
            if (ok) aligned++;
            else rates++;
        }
        if (aligned > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        EditorUtility.DisplayDialog("Alignement sur Sol",
            aligned + " décoration(s) alignée(s)." +
            (rates > 0 ? "\n" + rates + " sans sol trouvé en dessous (ignorée(s))." : ""), "OK");
    }

    // ── Cœur : raycast vers le bas pour trouver le sol ───────────────────────
    static bool AlignerSurSol(GameObject go)
    {
        if (go == null) return false;

        Vector3 pos = go.transform.position;

        // 1) Désactive les colliders de la déco pour ne pas que le raycast
        //    s'arrête dessus
        Collider[] colliders = go.GetComponentsInChildren<Collider>();
        bool[] etats = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            etats[i] = colliders[i].enabled;
            colliders[i].enabled = false;
        }

        // 2) Raycast depuis 50 m au-dessus, vers le bas sur 200 m
        Vector3 origine = new Vector3(pos.x, pos.y + 50f, pos.z);
        bool touche = Physics.Raycast(origine, Vector3.down, out RaycastHit hit, 200f);

        // 3) Restaure les colliders
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = etats[i];

        if (!touche)
        {
            Debug.LogWarning("[Alignement] '" + go.name + "' : aucun sol trouvé sous l'objet.");
            return false;
        }

        // 4) Compense l'offset du pivot : la base de l'objet (bounds.min.y)
        //    doit toucher le sol, pas son pivot
        float pivotOffset = 0f;
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            pivotOffset = pos.y - b.min.y; // > 0 si pivot au-dessus de la base
        }

        Vector3 nouvellePos = new Vector3(pos.x, hit.point.y + pivotOffset, pos.z);
        if ((nouvellePos - pos).sqrMagnitude < 0.0001f) return false;

        Undo.RecordObject(go.transform, "Aligner sur Sol");
        go.transform.position = nouvellePos;
        return true;
    }
}
