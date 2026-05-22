// PorteFinaleHelper.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Marquer Sélection comme Porte Finale" :
// ajoute le composant PorteFinale sur le GameObject sélectionné dans la
// Hierarchy (= la porte cachée derrière les buissons).
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

public static class PorteFinaleHelper
{
    [MenuItem("Tools/Marquer Sélection comme Porte Finale")]
    public static void Marquer()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Sélection vide",
                "Sélectionne d'abord la porte dans la Hierarchy.", "OK");
            return;
        }

        PorteFinale existante = go.GetComponent<PorteFinale>();
        if (existante == null) go.AddComponent<PorteFinale>();

        EditorUtility.SetDirty(go);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[PorteFinale] '" + go.name + "' est marquée comme porte finale.");
        EditorUtility.DisplayDialog("Porte Finale",
            "'" + go.name + "' est marquée comme porte finale.\n\n" +
            "Au ramassage de la clé, elle disparaîtra automatiquement.\n" +
            "Pour qu'elle TOURNE au lieu de disparaître : Inspector > " +
            "Porte Finale > Mode = Rotation.", "OK");
    }
}
