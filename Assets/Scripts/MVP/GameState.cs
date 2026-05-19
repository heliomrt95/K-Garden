// GameState.cs
// -----------------------------------------------------------------------------
// "Mémoire partagée" du jeu : une seule variable accessible partout.
// Ici on stocke juste : quel spray le joueur a choisi.
//
// Pas de MonoBehaviour, pas de GameObject : c'est une classe "static".
// Du coup pas besoin de l'attacher à quoi que ce soit dans Unity, elle existe
// toute seule et tous les autres scripts peuvent lire/écrire dedans.
// -----------------------------------------------------------------------------

public static class GameState
{
    // Le spray actuellement sélectionné par le joueur.
    // Valeurs possibles : "aucun", "eau", "engrais", "pesticide"... (à toi de choisir)
    public static string spraySelectionne = "aucun";
}
