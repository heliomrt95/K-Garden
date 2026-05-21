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
        // NB : on ne touche PAS à la couleur du matériau au démarrage. On laisse
        // celle du modèle d'origine. Le matériau ne change que lors d'un
        // arrosage explicite, ce qui évite de repeindre par erreur le sol ou
        // tout autre objet sur lequel un Plant aurait été attaché.
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
        if (rend != null) rend.material.color = new Color(0.3f, 0.8f, 0.3f);
        GameState.LibererMain(); // arrosoir retourne sur la table
        Debug.Log("Plante arrosée !");
    }
}
