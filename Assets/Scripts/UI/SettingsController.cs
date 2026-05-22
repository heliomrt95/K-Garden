// SettingsController.cs
// -----------------------------------------------------------------------------
// Gère le panel de réglages (sliders sons/effets + boutons secondaires).
// Persiste les volumes via PlayerPrefs.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;

public class SettingsController : MonoBehaviour
{
    [Header("Sliders")]
    public Slider sliderSons;
    public Slider sliderEffets;

    [Header("Audio")]
    public AudioSource musiqueDeFond;     // optionnel

    // Accès global pour d'autres scripts (volume effets sonores)
    public static float volumeEffets = 0.8f;

    void Start()
    {
        if (sliderSons != null)
        {
            sliderSons.value = PlayerPrefs.GetFloat("VolSons", 0.8f);
            sliderSons.onValueChanged.AddListener(_ => AppliquerSons());
        }
        if (sliderEffets != null)
        {
            sliderEffets.value = PlayerPrefs.GetFloat("VolEffets", 0.8f);
            sliderEffets.onValueChanged.AddListener(_ => AppliquerEffets());
        }
        AppliquerSons();
        AppliquerEffets();
    }

    public void AppliquerSons()
    {
        if (sliderSons == null) return;
        AudioListener.volume = sliderSons.value;
        if (musiqueDeFond != null) musiqueDeFond.volume = sliderSons.value;
        PlayerPrefs.SetFloat("VolSons", sliderSons.value);
    }

    public void AppliquerEffets()
    {
        if (sliderEffets == null) return;
        volumeEffets = sliderEffets.value;
        PlayerPrefs.SetFloat("VolEffets", sliderEffets.value);
    }

    public void Terminer()
    {
        // Cache le panel — le MainMenuController ou PauseMenu réactivera le panel parent
        gameObject.SetActive(false);

        // Si on est sur le menu principal, rouvre le panel menu
        MainMenuController main = FindAnyObjectByType<MainMenuController>();
        if (main != null) main.FermerReglages();

        // Si on est en pause dans le jeu, rouvre le panel pause
        PauseMenu pause = FindAnyObjectByType<PauseMenu>();
        if (pause != null) pause.FermerReglages();
    }

    public void ActionGraphique() { Debug.Log("Options graphiques (à implémenter)"); }
    public void ActionControle()  { Debug.Log("Contrôles (à implémenter)"); }
    public void ActionSkin()      { Debug.Log("Personnalisation skin (à implémenter)"); }
    public void ActionLangues()   { Debug.Log("Langues (à implémenter)"); }
}
