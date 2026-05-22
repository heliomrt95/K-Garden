// AudioSetup.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Configurer Audio" : crée (ou met à jour) un GameObject
// "AudioManager" dans la scène, y attache le script AudioManager, et assigne
// automatiquement chaque AudioClip depuis Assets/Audio/ par nom de fichier.
//
// Doit être lancé une seule fois par scène où on veut du son (typiquement la
// scène de jeu et la scène menu — ou bien la scène de jeu seule si l'OST
// doit traverser les scènes via DontDestroyOnLoad).
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

public static class AudioSetup
{
    const string DOSSIER = "Assets/Audio/";

    [MenuItem("Tools/Configurer Audio")]
    public static void Configurer()
    {
        // 1) Crée ou récupère le GameObject AudioManager
        GameObject go = GameObject.Find("AudioManager");
        if (go == null) go = new GameObject("AudioManager");
        AudioManager am = go.GetComponent<AudioManager>();
        if (am == null) am = go.AddComponent<AudioManager>();

        // 2) Charge les clips par chemin (les noms ont des espaces/caractères
        //    spéciaux, on les écrit textuellement ici)
        am.musicOST       = Load("OST/OST-K-GARDEN.wav");
        am.footstep       = Load("Bruit de pas.wav");
        am.pickupTool     = Load("prendre l'arosoir et le spray.wav");
        am.pickupSeed     = Load("prendre la graine ).wav");
        am.pickupDirt     = Load("Prendre de la terre.wav");
        am.plantSeed      = Load("planter une graine .wav");
        am.wateringCan    = Load("arosoir .wav");
        am.spray          = Load("différents spray.wav");
        am.plantGrow      = Load("Plante normale qui pousse.wav");
        am.carnivoreGrow  = Load("plante carnivore pousse 1.wav");
        am.carnivoreRoar  = Load("rugissement plante carnivore 2.wav");
        am.menuClick1     = Load("boutons menu 1.wav");
        am.menuClick2     = Load("boutons menu2.wav");
        am.keySpawn       = Load("Clée qui spawn.wav");
        am.insectFly      = Load("Insecte qui vole.wav");

        EditorUtility.SetDirty(am);
        EditorUtility.SetDirty(go);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        // Rapport des clips manquants
        int total = 15, ok = 0;
        if (am.musicOST       != null) ok++;
        if (am.footstep       != null) ok++;
        if (am.pickupTool     != null) ok++;
        if (am.pickupSeed     != null) ok++;
        if (am.pickupDirt     != null) ok++;
        if (am.plantSeed      != null) ok++;
        if (am.wateringCan    != null) ok++;
        if (am.spray          != null) ok++;
        if (am.plantGrow      != null) ok++;
        if (am.carnivoreGrow  != null) ok++;
        if (am.carnivoreRoar  != null) ok++;
        if (am.menuClick1     != null) ok++;
        if (am.menuClick2     != null) ok++;
        if (am.keySpawn       != null) ok++;
        if (am.insectFly      != null) ok++;

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);

        Debug.Log("✅ AudioManager configuré : " + ok + "/" + total + " clips assignés.");
        EditorUtility.DisplayDialog("Audio",
            "AudioManager créé/mis à jour dans la scène.\n" +
            ok + "/" + total + " clips assignés depuis " + DOSSIER + "\n\n" +
            "Pense à sauvegarder la scène (Cmd+S).", "OK");
    }

    static AudioClip Load(string sousChemin)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(DOSSIER + sousChemin);
    }
}
