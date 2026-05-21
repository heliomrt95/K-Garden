// PlantReward.cs
// -----------------------------------------------------------------------------
// Quand une plante arrive à maturité ET survit (vie > 0), elle donne des
// ressources au joueur en fonction de son niveau.
//
// Pas besoin d'attacher ce script à un GameObject : c'est une classe statique
// (utilitaire) appelée directement par Pot.cs via PlantReward.DonnerRecompenses().
// -----------------------------------------------------------------------------

using UnityEngine;

public static class PlantReward
{
    public static void DonnerRecompenses(int niveau)
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("PlantReward : pas d'InventoryManager dans la scène.");
            return;
        }

        if (niveau == 1)
        {
            InventoryManager.Instance.AddItem("pollen", 1);
            InventoryManager.Instance.AddItem("seve", 1);
            SimpleUIMessage.Afficher("Récolte plante N1 : +1 pollen, +1 sève");
        }
        else if (niveau == 2)
        {
            InventoryManager.Instance.AddItem("essenceRare", 1);
            InventoryManager.Instance.AddItem("spores", 1);
            SimpleUIMessage.Afficher("Récolte plante N2 : +1 essence rare, +1 spores");
        }
        else if (niveau == 3)
        {
            InventoryManager.Instance.AddItem("cristalVegetal", 1);
            SimpleUIMessage.Afficher("Récolte plante N3 : +1 cristal végétal !");
        }
        else
        {
            Debug.LogWarning("PlantReward : niveau inconnu : " + niveau);
        }
    }
}
