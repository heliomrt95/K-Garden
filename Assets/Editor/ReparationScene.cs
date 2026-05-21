// ReparationScene.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Réparer scène" : scanne tous les GameObjects de la scène et
// ajoute automatiquement les scripts Pickup / Source / Pot manquants, ainsi
// qu'un Collider si nécessaire, en se basant sur le NOM des objets :
//
//   - "Arrosoir*"           → Pickup (typeItem = "eau")
//   - "Spray*" / "SprayRouge" → Pickup (typeItem = "spray")
//   - "Terre" (enfant)      → Source (typeItem = "terre")
//
// Pratique si tes objets ont été créés AVANT la mise à jour des builders.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

public static class ReparationScene
{
    [MenuItem("Tools/Réparer scène (ajouter scripts manquants)")]
    public static void Reparer()
    {
        int ajoutsScript = 0;
        int ajoutsCollider = 0;

        // Trouve tous les GameObjects (y compris désactivés)
        GameObject[] tous = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (var go in tous)
        {
            // Ignore les assets (prefabs dans le Project, pas dans la scène)
            if (!go.scene.IsValid()) continue;

            string nom = go.name.ToLower();

            // === Arrosoir ===
            if (nom.StartsWith("arrosoir") || nom.Contains("watering"))
            {
                if (go.GetComponent<Pickup>() == null)
                {
                    var p = go.AddComponent<Pickup>();
                    p.typeItem = "eau";
                    p.positionEnMain = new Vector3(0.35f, -0.25f, 0.55f);
                    p.rotationEnMain = new Vector3(15f, -20f, 0f);
                    ajoutsScript++;
                    Debug.Log("[Repair] + Pickup(eau) sur " + go.name);
                }
                if (AssureCollider(go)) ajoutsCollider++;
            }
            // === Spray ===
            else if (nom.StartsWith("spray"))
            {
                if (go.GetComponent<Pickup>() == null)
                {
                    var p = go.AddComponent<Pickup>();
                    p.typeItem = "spray";
                    p.positionEnMain = new Vector3(0.30f, -0.22f, 0.50f);
                    p.rotationEnMain = new Vector3(0f, 180f, 0f);
                    ajoutsScript++;
                    Debug.Log("[Repair] + Pickup(spray) sur " + go.name);
                }
                if (AssureCollider(go)) ajoutsCollider++;
            }
            // === Terre (enfant d'un bac) ===
            else if (nom == "terre" && go.transform.parent != null)
            {
                if (go.GetComponent<Source>() == null)
                {
                    var s = go.AddComponent<Source>();
                    s.typeItem = "terre";
                    s.couleurEnMain = new Color(0.28f, 0.18f, 0.10f);
                    ajoutsScript++;
                    Debug.Log("[Repair] + Source(terre) sur " + go.name);
                }
            }
        }

        // Marque la scène comme modifiée pour qu'Unity te propose de sauvegarder
        if (ajoutsScript > 0 || ajoutsCollider > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        string resume = ajoutsScript + " script(s) ajouté(s), "
                      + ajoutsCollider + " collider(s) ajouté(s).";
        Debug.Log("=== Réparation : " + resume + " ===");

        if (ajoutsScript == 0 && ajoutsCollider == 0)
        {
            EditorUtility.DisplayDialog("Réparation",
                "Aucune modification. Vérifie que tes objets s'appellent bien Arrosoir, SprayRouge, ou Terre (sensible aux préfixes).",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Réparation", resume + "\nN'oublie pas de sauvegarder (Cmd+S).", "OK");
        }
    }

    // Ajoute un BoxCollider si le GameObject n'en a aucun (lui ni ses enfants),
    // calculé sur les bounds combinés des renderers. Retourne true si ajouté.
    static bool AssureCollider(GameObject go)
    {
        // S'il y a déjà un collider quelque part dans la hiérarchie, on ne fait rien
        if (go.GetComponentInChildren<Collider>() != null) return false;

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            // Pas de renderer : on met un collider arbitraire
            var b = go.AddComponent<BoxCollider>();
            b.size = Vector3.one * 0.3f;
            return true;
        }

        // Bounds combinés en monde
        Bounds wb = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) wb.Encapsulate(renderers[i].bounds);

        // Conversion en repère local du GameObject (compense scale/rotation/position)
        Vector3 ls = go.transform.lossyScale;
        var bc = go.AddComponent<BoxCollider>();
        bc.center = go.transform.InverseTransformPoint(wb.center);
        bc.size = new Vector3(
            Mathf.Abs(wb.size.x / Mathf.Max(0.001f, ls.x)),
            Mathf.Abs(wb.size.y / Mathf.Max(0.001f, ls.y)),
            Mathf.Abs(wb.size.z / Mathf.Max(0.001f, ls.z)));
        Debug.Log("[Repair] + BoxCollider sur " + go.name);
        return true;
    }
}
