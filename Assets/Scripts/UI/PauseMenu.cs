// PauseMenu.cs
// -----------------------------------------------------------------------------
// À mettre sur le Canvas du menu Pause dans la scène de jeu.
// Touche Échap = ouvrir/fermer.
// Time.timeScale = 0 fige le gameplay mais le Canvas UI reste réactif.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject panelPause;
    public GameObject panelSettings;

    [Header("Scène menu principal")]
    public string nomSceneMenu = "MainMenu";

    private bool enPause = false;

    void Start()
    {
        if (panelPause != null) panelPause.SetActive(false);
        if (panelSettings != null) panelSettings.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Basculer();
    }

    void Basculer()
    {
        enPause = !enPause;
        if (panelPause != null) panelPause.SetActive(enPause);
        Time.timeScale = enPause ? 0f : 1f;
        Cursor.lockState = enPause ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = enPause;
    }

    public void Reprendre()
    {
        ClicSecondaire();
        enPause = false;
        if (panelPause != null) panelPause.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OuvrirReglages()
    {
        ClicSecondaire();
        if (panelPause != null) panelPause.SetActive(false);
        if (panelSettings != null) panelSettings.SetActive(true);
    }

    public void FermerReglages()
    {
        ClicSecondaire();
        if (panelSettings != null) panelSettings.SetActive(false);
        if (panelPause != null) panelPause.SetActive(true);
    }

    public void RetourMenuPrincipal()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play(AudioManager.Instance.menuClick1);
        Time.timeScale = 1f;
        SceneManager.LoadScene(nomSceneMenu);
    }

    static void ClicSecondaire()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play(AudioManager.Instance.menuClick2);
    }
}
