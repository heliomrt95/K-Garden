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
        // Garantit qu'on a un InventoryManager + SimpleUIMessage. Si le joueur
        // n'a pas créé le GameObject "GameManager", on le crée automatiquement
        // pour que le drop fonctionne quoi qu'il arrive.
        AssurerManagers();

        // Son de récompense (niveaux 1-3 → keySpawn ; niveau 4 → rugissement)
        if (AudioManager.Instance != null)
        {
            AudioClip clip = (niveau == 4)
                ? AudioManager.Instance.carnivoreRoar
                : AudioManager.Instance.keySpawn;
            AudioManager.Instance.Play(clip, 0.9f);
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
        else if (niveau == 4)
        {
            // Plante carnivore : objectif final → drop la clé de victoire
            // juste au-dessus du BacCarnivore (ou de l'origine en fallback).
            SimpleUIMessage.Afficher("La plante carnivore a libéré une clé dorée !");

            GameObject bac = GameObject.Find("BacCarnivore");
            Vector3 posCle = bac != null
                ? bac.transform.position + Vector3.up * 0.6f
                : Vector3.up * 1f;
            CleSpawner.SpawnCle(posCle);
        }
        else
        {
            Debug.LogWarning("PlantReward : niveau inconnu : " + niveau);
        }
    }

    // Crée automatiquement le GameObject "GameManager" avec les composants
    // requis s'il n'existe pas — évite les drops perdus quand l'utilisateur
    // a oublié l'étape de setup.
    static void AssurerManagers()
    {
        if (InventoryManager.Instance != null && SimpleUIMessage.Instance != null) return;

        GameObject gm = GameObject.Find("GameManager");
        if (gm == null)
        {
            gm = new GameObject("GameManager");
            Debug.Log("[PlantReward] GameManager créé automatiquement.");
        }
        if (InventoryManager.Instance == null && gm.GetComponent<InventoryManager>() == null)
            gm.AddComponent<InventoryManager>();
        if (SimpleUIMessage.Instance == null && gm.GetComponent<SimpleUIMessage>() == null)
            gm.AddComponent<SimpleUIMessage>();
        // Note : SeedUnlockManager n'est pas auto-créé ici — il a besoin des
        // références aux sachets niveau 2 et 3 que seul l'utilisateur connaît.
    }
}
