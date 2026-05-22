// PauseMenuWiring.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Câbler boutons Pause" : détecte automatiquement les Button
// enfants du Canvas qui porte le script PauseMenu et câble leurs OnClick vers
// la bonne méthode, selon leur nom :
//
//   reprendre / resume                 → PauseMenu.Reprendre()
//   reglage / settings / parametres    → PauseMenu.OuvrirReglages()
//   retour / menu / main               → PauseMenu.RetourMenuPrincipal()
//   quitter / exit / quit              → PauseMenu.Quitter()
//
// Sécurité : on supprime d'abord les OnClick existants qui pointent déjà sur
// le même PauseMenu (évite les doublons quand on relance le menu plusieurs fois).
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class PauseMenuWiring
{
    [MenuItem("Tools/Câbler boutons Pause")]
    public static void Cabler()
    {
        // 1) Trouve le composant PauseMenu dans la scène
        PauseMenu pm = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
        if (pm == null)
        {
            EditorUtility.DisplayDialog("PauseMenu introuvable",
                "Aucun GameObject avec le composant PauseMenu trouvé dans la scène ouverte.\n" +
                "Ouvre la scène de jeu (scene.unity) puis relance ce menu.", "OK");
            return;
        }

        // 2) Récupère tous les Button enfants (y compris désactivés)
        Button[] boutons = pm.GetComponentsInChildren<Button>(true);
        if (boutons.Length == 0)
        {
            EditorUtility.DisplayDialog("Aucun bouton",
                "PauseMenu est trouvé mais aucun Button n'est dans sa hiérarchie. " +
                "Vérifie que les boutons sont bien enfants du Canvas du PauseMenu.", "OK");
            return;
        }

        int cables = 0;
        var rapport = new System.Text.StringBuilder();

        foreach (Button b in boutons)
        {
            string n = b.gameObject.name.ToLower();

            string action = null;
            UnityAction methode = null;

            if (Contient(n, "reprendre", "resume"))              { action = "Reprendre"; methode = pm.Reprendre; }
            else if (Contient(n, "reglage", "settings", "parametr")) { action = "OuvrirReglages"; methode = pm.OuvrirReglages; }
            else if (Contient(n, "retour", "menu", "main"))      { action = "RetourMenuPrincipal"; methode = pm.RetourMenuPrincipal; }
            else if (Contient(n, "quitter", "exit", "quit"))     { action = "Quitter"; methode = pm.Quitter; }

            if (methode == null)
            {
                rapport.AppendLine("• " + b.name + " : non reconnu (renommer avec 'quitter', 'retour', 'reglages' ou 'reprendre').");
                continue;
            }

            // 3) Nettoie les listeners existants qui ciblent ce PauseMenu, sinon
            //    on accumule des doublons à chaque appel.
            for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                if (b.onClick.GetPersistentTarget(i) == pm)
                    UnityEventTools.RemovePersistentListener(b.onClick, i);
            }

            // 4) Ajoute le bon listener (persistant = sérialisé dans la scène)
            UnityEventTools.AddPersistentListener(b.onClick, methode);
            EditorUtility.SetDirty(b);
            rapport.AppendLine("✅ " + b.name + " → PauseMenu." + action + "()");
            cables++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pm.gameObject.scene);

        Debug.Log("[PauseMenuWiring] Câblage terminé.\n" + rapport.ToString());
        EditorUtility.DisplayDialog("Câblage Pause",
            cables + " bouton(s) câblé(s).\n\n" + rapport.ToString() +
            "\nPense à sauvegarder la scène (Cmd+S).", "OK");
    }

    static bool Contient(string s, params string[] motsCles)
    {
        foreach (var m in motsCles) if (s.Contains(m)) return true;
        return false;
    }
}
