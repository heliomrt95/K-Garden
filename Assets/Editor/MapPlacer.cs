// MapPlacer.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Placer la Map autour de la Serre" : ajoute dans la scène un
// GameObject Terrain qui utilise le TerrainData du pack "Pure Poly Free Low
// Poly Nature Pack", et le centre sur la position de l'objet "Serre".
//
// Le Terrain reçoit :
//   - un composant Terrain (avec le TerrainData)
//   - un TerrainCollider (pour que le joueur ne traverse pas le sol)
//
// Si un objet "Plane" existe déjà dans la scène (l'ancien sol plat), il est
// désactivé pour éviter le z-fighting avec le nouveau terrain.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

public static class MapPlacer
{
    const string CHEMIN_TERRAIN_DATA =
        "Assets/Pure Poly/Free Low Poly Nature Pack/Terrain/Terrain.asset";

    [MenuItem("Tools/Placer la Map autour de la Serre")]
    public static void PlacerMap()
    {
        // 1) Charger le TerrainData
        TerrainData td = AssetDatabase.LoadAssetAtPath<TerrainData>(CHEMIN_TERRAIN_DATA);
        if (td == null)
        {
            EditorUtility.DisplayDialog("Terrain introuvable",
                "Terrain.asset introuvable à " + CHEMIN_TERRAIN_DATA, "OK");
            return;
        }

        // 2) Trouver la Serre (par nom) — fallback à l'origine si absente
        GameObject serre = GameObject.Find("Serre");
        Vector3 centreSerre = serre != null ? serre.transform.position : Vector3.zero;

        // 3) Supprimer un terrain existant nommé "Map" pour éviter les doublons
        GameObject ancien = GameObject.Find("Map");
        if (ancien != null)
        {
            if (!EditorUtility.DisplayDialog("Map déjà présente",
                    "Un objet 'Map' existe déjà dans la scène. Le remplacer ?", "Oui", "Annuler"))
                return;
            Object.DestroyImmediate(ancien);
        }

        // 4) Créer le GameObject Terrain
        GameObject mapGO = Terrain.CreateTerrainGameObject(td);
        mapGO.name = "Map";

        // 5) Centrer le terrain sur la Serre
        //    Un Terrain Unity a son pivot au coin (0,0,0) — il faut donc décaler
        //    de -size/2 pour que son centre tombe sur la serre.
        Vector3 size = td.size;

        // Base de la serre = Y le plus bas de ses renderers (sinon transform.position.y)
        float baseSerreY = centreSerre.y;
        if (serre != null)
        {
            Renderer[] rs = serre.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                baseSerreY = b.min.y;
            }
        }

        // Hauteur du heightmap à l'emplacement de la serre (centre du terrain → 0.5, 0.5).
        // On positionne le terrain en Y pour que sa surface sous la serre coïncide
        // avec la base de la serre (-1 cm pour éviter le z-fighting).
        float hauteurCentreTerrain = td.GetInterpolatedHeight(0.5f, 0.5f);
        Vector3 origine = new Vector3(
            centreSerre.x - size.x * 0.5f,
            baseSerreY - hauteurCentreTerrain - 0.01f,
            centreSerre.z - size.z * 0.5f);
        mapGO.transform.position = origine;

        // 6) Désactiver l'ancien sol plat "Plane" s'il existe
        GameObject plane = GameObject.Find("Plane");
        if (plane != null)
        {
            plane.SetActive(false);
            Debug.Log("Ancien 'Plane' désactivé (présent sous le nouveau terrain).");
        }

        // 7) S'assurer que le NavMesh puisse se baker dessus (static editor flags)
        GameObjectUtility.SetStaticEditorFlags(mapGO,
            StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.NavigationStatic | StaticEditorFlags.ReflectionProbeStatic);

        Selection.activeGameObject = mapGO;
        EditorGUIUtility.PingObject(mapGO);
        Debug.Log("✅ Map placée — terrain " + size.x + "×" + size.z + "m centré sur la Serre " +
                  "(origine " + origine + "). Pense à re-baker le NavMesh.");
    }
}
