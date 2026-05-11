using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire global. Singleton.
/// Suit la progression (quelles graines sont débloquées, état de la quête finale).
/// Émet des évènements quand le joueur atteint un nouveau jalon.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Progression")]
    public int seedsUnlocked = 1; // Au démarrage, seule Drosera (index 0) est dispo
    public bool[] plantsCompleted = new bool[3]; // Drosera, Nepenthes, Dionaea
    public bool bossSeedsAvailable = false;
    public bool keyObtained = false;

    [Header("Configuration des plantes")]
    public PlantData[] plantDataset = new PlantData[3];

    public System.Action<int> OnSeedUnlocked;
    public System.Action OnAllPlantsGrown;
    public System.Action OnKeyCollected;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Données par défaut si non configurées dans l'Inspector
        if (plantDataset[0] == null || string.IsNullOrEmpty(plantDataset[0].plantName))
        {
            plantDataset[0] = new PlantData {
                plantName = "Drosera (Rossolis)",
                stemColor = new Color(0.4f, 0.7f, 0.3f),
                flowerColor = new Color(1f, 0.4f, 0.6f),
                growthDuration = 25f,
                pestWaveCount = 8
            };
            plantDataset[1] = new PlantData {
                plantName = "Nepenthes",
                stemColor = new Color(0.3f, 0.55f, 0.25f),
                flowerColor = new Color(0.7f, 0.2f, 0.2f),
                growthDuration = 35f,
                pestWaveCount = 12
            };
            plantDataset[2] = new PlantData {
                plantName = "Dionaea (Attrape-mouche)",
                stemColor = new Color(0.25f, 0.5f, 0.2f),
                flowerColor = new Color(0.9f, 0.3f, 0.3f),
                growthDuration = 45f,
                pestWaveCount = 16
            };
        }
    }

    public void NotifyPlantHarvested(int plantIndex)
    {
        if (plantIndex < 0 || plantIndex >= plantsCompleted.Length) return;
        plantsCompleted[plantIndex] = true;

        // Débloque la graine suivante
        int nextSeed = plantIndex + 1;
        if (nextSeed < 3)
        {
            seedsUnlocked = Mathf.Max(seedsUnlocked, nextSeed + 1);
            OnSeedUnlocked?.Invoke(nextSeed);
            Debug.Log($"[GameManager] Graine {nextSeed} débloquée ({plantDataset[nextSeed].plantName})");
        }
        else
        {
            // Toutes les plantes ont été récoltées → la plante carnivore est plantable
            bossSeedsAvailable = true;
            OnAllPlantsGrown?.Invoke();
            Debug.Log("[GameManager] Les 3 graines de la plante carnivore sont disponibles !");
        }
    }

    public void NotifyKeyCollected()
    {
        if (keyObtained) return;
        keyObtained = true;
        OnKeyCollected?.Invoke();
        Debug.Log("[GameManager] La clé a été ramassée — déclenchement de la fin.");
    }
}

[System.Serializable]
public class PlantData
{
    public string plantName;
    public Color stemColor = Color.green;
    public Color flowerColor = Color.magenta;
    public float growthDuration = 30f; // en secondes
    public int pestWaveCount = 10;     // nb d'ennemis pendant la phase de défense
}

public enum ItemType
{
    None,
    WateringCan,
    FertilizerSpray,
    PestSpray,
    Seed,
    SoilHandful,
    Key
}
