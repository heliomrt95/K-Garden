// Arrosoir.cs
// -----------------------------------------------------------------------------
// À mettre sur le GameObject "Arrosoir" dans la scène (le parent qui contient
// le Corps, le Bec, la Pomme et l'Anse).
//
// Quand le joueur clique dessus, il "prend" l'arrosoir : on note dans le
// GameState qu'il l'a en main.
//
// Composants Unity requis : un Collider quelque part dans la hiérarchie de
// l'arrosoir (les Cubes / Cylindres en ont un par défaut, donc OK).
// -----------------------------------------------------------------------------

using UnityEngine;

public class Arrosoir : MonoBehaviour
{
    // Méthode publique appelée par Player.cs quand on clique sur l'arrosoir.
    public void Prendre()
    {
        GameState.arrosoirEnMain = true;
        Debug.Log("Tu as pris l'arrosoir.");
    }
}
