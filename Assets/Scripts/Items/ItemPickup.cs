using UnityEngine;

/// <summary>
/// Composant marqueur posé sur tout objet ramassable.
/// Le ramassage proprement dit est géré par InteractionManager.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    public ItemType itemType = ItemType.None;
    public int seedIndex = -1; // utilisé seulement pour itemType == Seed
}
