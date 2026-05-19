// SprayPicker.cs
// -----------------------------------------------------------------------------
// À mettre sur chaque spray présent dans la scène.
// Quand le joueur clique dessus, le type de ce spray devient le spray actif.
//
// Composants Unity requis sur le même GameObject : un Collider (ex: Box Collider)
// pour que le rayon du joueur puisse le détecter.
// -----------------------------------------------------------------------------

using UnityEngine;

public class SprayPicker : MonoBehaviour
{
    // Le type du spray, à renseigner dans l'Inspector pour chaque objet.
    // Exemples : "eau", "engrais", "pesticide"
    public string typeDeSpray = "eau";

    // Méthode publique appelée par Player.cs quand on clique sur ce spray.
    public void Selectionner()
    {
        GameState.spraySelectionne = typeDeSpray;
        Debug.Log("Spray sélectionné : " + typeDeSpray);
    }
}
