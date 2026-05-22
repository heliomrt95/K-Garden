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
        SceneManager.LoadScene(nomSceneJeu);
    }

    public void OuvrirBoutique()
    {
        Debug.Log("Boutique pas encore implémentée.");
    }

    public void OuvrirReglages()
    {
        if (panelMenu != null)     panelMenu.SetActive(false);
        if (panelSettings != null) panelSettings.SetActive(true);
    }

    public void FermerReglages()
    {
        if (panelSettings != null) panelSettings.SetActive(false);
        if (panelMenu != null)     panelMenu.SetActive(true);
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
