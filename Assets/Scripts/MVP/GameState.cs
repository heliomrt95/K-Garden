// GameState.cs
// -----------------------------------------------------------------------------
// "Mémoire partagée" du jeu : une variable accessible partout.
// Ici on stocke juste : est-ce que le joueur tient l'arrosoir ?
//
// Pas de MonoBehaviour, pas de GameObject : c'est une classe "static".
// Du coup pas besoin de l'attacher à quoi que ce soit dans Unity, elle existe
// toute seule et tous les autres scripts peuvent lire/écrire dedans.
// -----------------------------------------------------------------------------

public static class GameState
{
    // Le joueur a-t-il l'arrosoir en main ?
    // true = oui (il peut arroser), false = non
    public static bool arrosoirEnMain = false;
}
