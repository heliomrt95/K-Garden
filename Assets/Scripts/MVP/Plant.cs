// Plant.cs
// -----------------------------------------------------------------------------
// À mettre sur chaque plante de la scène.
// La plante a 2 états :
//   - Sec     (marron) : a besoin d'eau
//   - Arrosee (vert)   : ok
//
// Quand le joueur clique dessus AVEC l'arrosoir en main, elle est arrosée.
// Sinon : message dans la console.
//
// Composants Unity requis :
//   - un Renderer (le mesh visible — cube, sphère ou modèle 3D)
//   - un Collider (Box ou autre) pour que le rayon du joueur la détecte
// -----------------------------------------------------------------------------

using UnityEngine;

public class Plant : MonoBehaviour
{
    // Les 2 états possibles. "enum" = liste de valeurs nommées.
    public enum Etat { Sec, Arrosee }

    // État de départ — réglable dans l'Inspector
    public Etat etatActuel = Etat.Sec;

    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
        MettreAJourCouleur();
    }

    // Appelé par Player.cs quand on clique sur cette plante.
    public void EssayerArroser()
    {
        // Cas 1 : pas d'arrosoir en main → message d'aide
        if (!GameState.arrosoirEnMain)
        {
            Debug.Log("Tu n'as pas d'arrosoir en main. Prends-le d'abord.");
            return;
        }

        // Cas 2 : plante déjà arrosée → rien à faire
        if (etatActuel == Etat.Arrosee)
        {
            Debug.Log("La plante est déjà arrosée.");
            return;
        }

        // Cas 3 : plante sèche + arrosoir en main → on arrose
        etatActuel = Etat.Arrosee;
        MettreAJourCouleur();
        Debug.Log("Plante arrosée !");
    }

    // Change la couleur du Renderer selon l'état actuel.
    void MettreAJourCouleur()
    {
        if (rend == null) return;
        if (etatActuel == Etat.Sec)     rend.material.color = new Color(0.6f, 0.4f, 0.2f); // marron
        if (etatActuel == Etat.Arrosee) rend.material.color = new Color(0.3f, 0.8f, 0.3f); // vert
    }
}
