// Plant.cs
// -----------------------------------------------------------------------------
// À mettre sur chaque plante de la scène.
// 2 états :
//   - Sec     (marron) : a besoin d'eau
//   - Arrosee (vert)   : ok
//
// Quand le joueur clique dessus AVEC l'arrosoir (item "eau") en main → arrosée.
// L'arrosoir retourne automatiquement à sa place après usage.
// -----------------------------------------------------------------------------

using UnityEngine;

public class Plant : MonoBehaviour
{
    public enum Etat { Sec, Arrosee }
    public Etat etatActuel = Etat.Sec;

    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
        MettreAJourCouleur();
    }

    public void EssayerArroser()
    {
        if (GameState.itemEnMain != "eau")
        {
            Debug.Log("Il te faut l'arrosoir (eau) en main.");
            return;
        }
        if (etatActuel == Etat.Arrosee)
        {
            Debug.Log("La plante est déjà arrosée.");
            return;
        }
        etatActuel = Etat.Arrosee;
        MettreAJourCouleur();
        GameState.LibererMain(); // arrosoir retourne sur la table
        Debug.Log("Plante arrosée !");
    }

    void MettreAJourCouleur()
    {
        if (rend == null) return;
        if (etatActuel == Etat.Sec)     rend.material.color = new Color(0.6f, 0.4f, 0.2f);
        if (etatActuel == Etat.Arrosee) rend.material.color = new Color(0.3f, 0.8f, 0.3f);
    }
}
