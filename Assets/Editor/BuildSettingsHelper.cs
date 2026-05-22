// BuildSettingsHelper.cs
// -----------------------------------------------------------------------------
// Menu "Tools > UI > Configurer Build Settings (auto)" : ajoute MainMenu en
// position 0 et la scène de jeu détectée en position 1.
//
// Détecte la scène de jeu en cherchant la première .unity qui contient un Pot
// (ou qui n'est pas MainMenu).
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BuildSettingsHelper
{
    [MenuItem("Tools/UI/Configurer Build Settings (auto)")]
    public static void Configurer()
    {
        // Trouve toutes les scènes du projet
        string[] guids = AssetDatabase.FindAssets("t:Scene");
        List<EditorBuildSettingsScene> liste = new List<EditorBuildSettingsScene>();

        // MainMenu d'abord (priorité 0)
        string mainMenuPath = null;
        string sceneJeuPath = null;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string nom = System.IO.Path.GetFileNameWithoutExtension(path);

            if (path.Contains("Pure Poly") || path.Contains("BOXOPHOBIC")) continue; // skip démos
            if (nom == "MainMenu") mainMenuPath = path;
            else if (sceneJeuPath == null) sceneJeuPath = path;
        }

        if (mainMenuPath != null)
            liste.Add(new EditorBuildSettingsScene(mainMenuPath, true));
        if (sceneJeuPath != null)
            liste.Add(new EditorBuildSettingsScene(sceneJeuPath, true));

        EditorBuildSettings.scenes = liste.ToArray();

        string msg = "Build Settings configurés :\n";
        for (int i = 0; i < liste.Count; i++)
            msg += "  [" + i + "] " + System.IO.Path.GetFileNameWithoutExtension(liste[i].path) + "\n";

        if (sceneJeuPath != null)
        {
            string nomJeu = System.IO.Path.GetFileNameWithoutExtension(sceneJeuPath);
            msg += "\n→ Pense à mettre '" + nomJeu + "' dans le champ 'Nom Scene Jeu' " +
                   "du UIManager (composant MainMenuController).";
        }

        Debug.Log("[BuildSettings] " + msg);
        EditorUtility.DisplayDialog("Build Settings", msg, "OK");
    }
}
