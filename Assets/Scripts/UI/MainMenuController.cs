// MainMenuController.cs
// -----------------------------------------------------------------------------
// Contrôleur de la page d'accueil. À mettre sur un GameObject "UIManager" du
// Canvas. Les boutons JOUER / BOUTIQUE / REGLAGE pointent vers ses méthodes.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels à switcher")]
    public GameObject panelMenu;
    public GameObject panelSettings;

    [Header("Scène à charger pour Jouer")]
    public string nomSceneJeu = "MainGame";

    public void Jouer()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play(AudioManager.Instance.menuClick1);
        if (string.IsNullOrEmpty(nomSceneJeu))
        {
            Debug.LogError("[MainMenu] Aucune scène configurée. Renseigne 'Nom Scene Jeu' " +
                           "dans l'Inspector de UIManager → MainMenuController.");
            return;
        }

        // Vérifie que la scène est bien dans Build Settings
        if (!SceneExisteDansBuild(nomSceneJeu))
        {
            Debug.LogError("[MainMenu] Scène '" + nomSceneJeu + "' introuvable dans les Build " +
                           "Settings. Va dans File > Build Settings et ajoute la scène. " +
                           "Vérifie aussi l'orthographe exacte (sensible à la casse).");
            return;
        }

        SceneManager.LoadScene(nomSceneJeu);
    }

    static bool SceneExisteDansBuild(string nom)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            string n = System.IO.Path.GetFileNameWithoutExtension(path);
            if (n == nom) return true;
        }
        return false;
    }

    public void OuvrirBoutique()
    {
        ClicSecondaire();
        Debug.Log("Boutique pas encore implémentée.");
    }

    public void OuvrirReglages()
    {
        ClicSecondaire();
        if (panelMenu != null)     panelMenu.SetActive(false);
        if (panelSettings != null) panelSettings.SetActive(true);
    }

    public void FermerReglages()
    {
        ClicSecondaire();
        if (panelSettings != null) panelSettings.SetActive(false);
        if (panelMenu != null)     panelMenu.SetActive(true);
    }

    static void ClicSecondaire()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play(AudioManager.Instance.menuClick2);
    }

    public void Quitter()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
