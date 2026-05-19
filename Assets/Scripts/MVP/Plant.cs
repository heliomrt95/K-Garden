// Plant.cs
// -----------------------------------------------------------------------------
// À mettre sur chaque plante de la scène.
// La plante a 3 états : Normal, Malade, Guerie.
// Quand le joueur clique dessus avec le BON spray sélectionné, elle guérit.
// Sinon : message d'erreur dans la console.
//
// Composants Unity requis :
//   - un Renderer (le mesh visible — le cube, la sphère ou ton modèle 3D)
//   - un Collider (Box ou autre) pour que le rayon du joueur la détecte
// -----------------------------------------------------------------------------

using UnityEngine;

public class Plant : MonoBehaviour
{
    // Les 3 états possibles. "enum" = liste de valeurs nommées (plus lisible qu'un int).
    public enum Etat { Normal, Malade, Guerie }

    // Réglages dans l'Inspector
    public Etat etatDepart = Etat.Malade;        // dans quel état la plante commence
    public string sprayQuiGuerit = "eau";        // quel spray la soigne

    private Etat etatActuel;
    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
        etatActuel = etatDepart;
        MettreAJourCouleur();
    }

    // Appelé par Player.cs quand on clique sur cette plante.
    public void EssayerDeSoigner()
    {
        // Cas où ça marche : plante malade ET le bon spray en main
        if (etatActuel == Etat.Malade && GameState.spraySelectionne == sprayQuiGuerit)
        {
            etatActuel = Etat.Guerie;
            MettreAJourCouleur();
            Debug.Log("Plante guérie !");
        }
        else
        {
            // Sinon on log un message d'aide pour comprendre pourquoi ça n'a rien fait
            Debug.Log("Échec : état=" + etatActuel
                      + ", spray en main=" + GameState.spraySelectionne
                      + ", spray requis=" + sprayQuiGuerit);
        }
    }

    // Change la couleur du Renderer selon l'état actuel.
    void MettreAJourCouleur()
    {
        if (rend == null) return;
        if (etatActuel == Etat.Normal) rend.material.color = Color.green;
        if (etatActuel == Etat.Malade) rend.material.color = new Color(0.6f, 0.4f, 0.2f); // marron
        if (etatActuel == Etat.Guerie) rend.material.color = new Color(0.4f, 1f, 0.4f);   // vert vif
    }
}
