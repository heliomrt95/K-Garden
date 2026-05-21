// SimpleUIMessage.cs
// -----------------------------------------------------------------------------
// Affiche un message temporaire au centre-haut de l'écran (ex: "Nouvelle
// graine débloquée !"). Appelé depuis n'importe où via SimpleUIMessage.Afficher().
// À mettre sur le même GameObject "GameManager" que InventoryManager.
// -----------------------------------------------------------------------------

using UnityEngine;

public class SimpleUIMessage : MonoBehaviour
{
    public static SimpleUIMessage Instance;

    public float dureeAffichage = 3f;  // secondes d'affichage par message

    private string message = "";
    private float tempsRestant = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Méthode statique : appelable depuis n'importe quel script
    public static void Afficher(string texte)
    {
        if (Instance == null)
        {
            Debug.Log("[Message] " + texte + "  (SimpleUIMessage absent de la scène)");
            return;
        }
        Instance.message = texte;
        Instance.tempsRestant = Instance.dureeAffichage;
    }

    void Update()
    {
        if (tempsRestant > 0f) tempsRestant -= Time.deltaTime;
    }

    void OnGUI()
    {
        if (tempsRestant <= 0f || string.IsNullOrEmpty(message)) return;

        float w = 500f, h = 50f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.18f;

        // Fond noir semi-transparent
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        // Texte blanc centré
        GUI.color = Color.white;
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 18;
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, w, h), message, style);
    }
}
