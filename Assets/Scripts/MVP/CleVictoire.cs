// CleVictoire.cs
// -----------------------------------------------------------------------------
// À attacher à la clé. Quand le joueur ramasse la clé (Pickup.Prendre()),
// CleVictoire.OnRamasse() est appelé automatiquement par Pickup → on cherche
// une PorteFinale dans la scène et on l'ouvre.
//
// Si aucune PorteFinale n'est marquée explicitement, on cherche un GameObject
// nommé "Porte" ou "Door" en fallback.
// -----------------------------------------------------------------------------

using UnityEngine;

public class CleVictoire : MonoBehaviour
{
    public void OnRamasse()
    {
        // 1) Cherche d'abord une PorteFinale explicite
        PorteFinale porte = Object.FindAnyObjectByType<PorteFinale>(FindObjectsInactive.Include);
        if (porte != null) { porte.Ouvrir(); return; }

        // 2) Fallback : objet nommé "Porte" ou "Door"
        GameObject p = GameObject.Find("Porte") ?? GameObject.Find("Door");
        if (p != null)
        {
            PorteFinale ajoutee = p.AddComponent<PorteFinale>();
            ajoutee.Ouvrir();
            return;
        }

        Debug.LogWarning("[CleVictoire] Aucune PorteFinale ni Porte/Door dans la scène. " +
                         "Sélectionne ta porte et fais Tools > Marquer Sélection comme Porte Finale.");
        SimpleUIMessage.Afficher("Tu as la clé ! (mais aucune porte à ouvrir)");
    }
}
