// Pot.cs
// -----------------------------------------------------------------------------
// À mettre sur un pot vide qui peut recevoir de la terre puis de l'eau.
//
// Composants Unity requis :
//   - un Collider (pour être cliquable)
//   - 2 enfants optionnels (à glisser dans l'Inspector) :
//       * visuelTerre : un Cube/objet caché qui s'affiche quand le pot reçoit de la terre
//       * visuelEau   : pareil pour l'eau
//
// Règle : il faut mettre de la terre AVANT de pouvoir mettre de l'eau.
// -----------------------------------------------------------------------------

using UnityEngine;

public class Pot : MonoBehaviour
{
    // Glisse ici les GameObjects enfants à afficher quand on remplit
    public GameObject visuelTerre;
    public GameObject visuelEau;

    private bool aTerre = false;
    private bool aEau = false;

    void Start()
    {
        // Au démarrage, terre et eau cachés
        if (visuelTerre != null) visuelTerre.SetActive(false);
        if (visuelEau != null) visuelEau.SetActive(false);
    }

    // Appelé par Player.cs quand on clique sur ce pot
    public void Utiliser()
    {
        string item = GameState.itemEnMain;

        if (item == "terre")
        {
            if (aTerre) { Debug.Log("Le pot a déjà de la terre."); return; }
            aTerre = true;
            if (visuelTerre != null) visuelTerre.SetActive(true);
            GameState.LibererMain(); // consomme la terre (cube en main détruit)
            Debug.Log("Pot rempli de terre.");
        }
        else if (item == "eau")
        {
            if (!aTerre) { Debug.Log("Mets d'abord de la terre dans le pot."); return; }
            if (aEau)   { Debug.Log("Le pot est déjà arrosé."); return; }
            aEau = true;
            if (visuelEau != null) visuelEau.SetActive(true);
            GameState.LibererMain(); // arrosoir retourne à sa place
            Debug.Log("Pot arrosé.");
        }
        else if (item == "")
        {
            Debug.Log("Mains vides. Prends d'abord de la terre ou l'arrosoir.");
        }
        else
        {
            Debug.Log("Cet item (" + item + ") ne va pas dans le pot.");
        }
    }
}
