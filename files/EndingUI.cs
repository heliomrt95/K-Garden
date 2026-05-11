using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Affiche l'écran de fin quand la clé est ramassée :
/// fade-to-black instantané + texte blanc au centre.
///
/// Configuration scène :
///  - Créer un Canvas (Screen Space - Overlay) avec :
///     * une Image plein écran noire (alpha=0 au départ)
///     * un Text (TMP ou legacy) centré avec le message final
///  - Assigner les références et le Canvas à ce script.
/// </summary>
public class EndingUI : MonoBehaviour
{
    public CanvasGroup endingCanvas;          // alpha 0 → 1
    public Text endingText;                   // ou TMPro.TMP_Text si tu utilises TextMeshPro
    [TextArea(2, 4)]
    public string endingMessage = "Vous avez trouvé la clé... mais à quoi bon partir ? Vos petites fleurs ont encore besoin de vous.";

    public float fadeDuration = 0.3f;

    private void Awake()
    {
        if (endingCanvas != null)
        {
            endingCanvas.alpha = 0;
            endingCanvas.interactable = false;
            endingCanvas.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnKeyCollected += TriggerEnding;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnKeyCollected -= TriggerEnding;
    }

    public void TriggerEnding()
    {
        StartCoroutine(EndingRoutine());
    }

    private IEnumerator EndingRoutine()
    {
        if (endingText != null) endingText.text = "";

        // Fade-to-black
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            if (endingCanvas != null) endingCanvas.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        if (endingCanvas != null) endingCanvas.alpha = 1f;

        // Petite pause de noir total
        yield return new WaitForSeconds(0.6f);

        // Apparition progressive du texte
        if (endingText != null)
        {
            for (int i = 0; i <= endingMessage.Length; i++)
            {
                endingText.text = endingMessage.Substring(0, i);
                yield return new WaitForSeconds(0.04f);
            }
        }

        // Libère la souris pour permettre au joueur de quitter
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
