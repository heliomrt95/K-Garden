using System.Collections;
using UnityEngine;

/// <summary>
/// Logique d'un pot individuel.
/// État : Empty → SoilAdded → Fertilized → SeedPlanted → Watered → Growing → Harvestable.
/// L'objet d'interaction (poignée de terre, spray, graine, arrosoir) est passé par le joueur via ReceiveInput.
/// </summary>
public class PotInteraction : MonoBehaviour
{
    public enum PotState { Empty, SoilAdded, Fertilized, SeedPlanted, Watered, Growing, Harvestable, Burnt }

    public PotState State { get; private set; } = PotState.Empty;
    public int PlantedSeedIndex { get; private set; } = -1;
    public bool IsBossPot { get; private set; } = false;

    private GameObject soilVisual;
    private PlantManager activePlant;

    private void Start()
    {
        // Récupère le visuel de terre créé par LevelBuilder
        Transform sv = transform.Find("SoilVisual");
        if (sv != null) soilVisual = sv.gameObject;
    }

    /// <summary>
    /// Appelé par InteractionManager quand le joueur interagit avec ce pot.
    /// </summary>
    public void ReceiveInput(ItemType item, int seedIdx)
    {
        switch (State)
        {
            case PotState.Empty:
                if (item == ItemType.SoilHandful)
                {
                    State = PotState.SoilAdded;
                    if (soilVisual != null) soilVisual.SetActive(true);
                    Debug.Log($"[Pot {name}] Terre ajoutée.");
                }
                else Debug.Log($"[Pot {name}] Il faut d'abord ajouter de la terre (poignée de terreau).");
                break;

            case PotState.SoilAdded:
                if (item == ItemType.FertilizerSpray)
                {
                    State = PotState.Fertilized;
                    StartCoroutine(FlashSoilColor(new Color(0.4f, 0.3f, 0.1f)));
                    Debug.Log($"[Pot {name}] Engrais appliqué.");
                }
                else Debug.Log($"[Pot {name}] Il faut maintenant pulvériser de l'engrais.");
                break;

            case PotState.Fertilized:
                if (item == ItemType.Seed && seedIdx >= 0)
                {
                    PlantedSeedIndex = seedIdx;
                    State = PotState.SeedPlanted;
                    SpawnSeedVisual(seedIdx);
                    Debug.Log($"[Pot {name}] Graine plantée (idx {seedIdx}).");
                }
                else Debug.Log($"[Pot {name}] Il faut planter une graine.");
                break;

            case PotState.SeedPlanted:
                if (item == ItemType.WateringCan)
                {
                    State = PotState.Watered;
                    Debug.Log($"[Pot {name}] Plante arrosée — début de croissance.");
                    BeginGrowth();
                }
                else Debug.Log($"[Pot {name}] Il faut maintenant arroser.");
                break;

            case PotState.Growing:
                // Pendant la croissance, on peut ré-arroser ou ré-fertiliser pour booster la croissance des plantes carnivores
                if (item == ItemType.WateringCan && activePlant != null)
                    activePlant.AddBoost(0.1f);
                else if (item == ItemType.FertilizerSpray && activePlant != null)
                    activePlant.AddBoost(0.05f);
                break;

            case PotState.Harvestable:
                // Ramasser la graine produite (logique dans PlantManager → drop au moment de la récolte)
                Debug.Log($"[Pot {name}] Plante mature — la graine a été lâchée à proximité.");
                break;
        }
    }

    private void SpawnSeedVisual(int idx)
    {
        // Petite sphère sur le dessus de la terre
        GameObject seed = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        seed.name = "PlantedSeed";
        seed.transform.parent = transform;
        seed.transform.localPosition = new Vector3(0, 0.95f, 0);
        seed.transform.localScale = Vector3.one * 0.1f;
        Destroy(seed.GetComponent<Collider>());
        seed.GetComponent<Renderer>().material.color = GetSeedColor(idx);
    }

    private Color GetSeedColor(int idx)
    {
        return idx switch
        {
            0 => new Color(0.6f, 0.8f, 0.2f),
            1 => new Color(0.7f, 0.4f, 0.2f),
            2 => new Color(0.5f, 0.2f, 0.5f),
            _ => Color.white
        };
    }

    private void BeginGrowth()
    {
        State = PotState.Growing;

        // Crée le GameObject de la plante (les capsules empilées sont gérées par PlantManager)
        GameObject plantGO = new GameObject($"Plant_{PlantedSeedIndex}");
        plantGO.transform.parent = transform;
        plantGO.transform.localPosition = new Vector3(0, 0.95f, 0);

        activePlant = plantGO.AddComponent<PlantManager>();
        activePlant.Initialize(PlantedSeedIndex, this);

        // Démarre la vague de nuisibles correspondante
        if (WaveManager.Instance != null)
            WaveManager.Instance.StartWave(this, GameManager.Instance.plantDataset[PlantedSeedIndex].pestWaveCount);
    }

    public void OnPlantMatured()
    {
        State = PotState.Harvestable;
        DropNextSeed();
    }

    public void OnPlantDestroyed()
    {
        State = PotState.Burnt;
        Debug.Log($"[Pot {name}] La plante a été détruite par les nuisibles ! Re-essaye.");
        // Réinitialiser pour permettre au joueur de recommencer
        StartCoroutine(ResetAfterDelay(2f));
    }

    private IEnumerator ResetAfterDelay(float t)
    {
        yield return new WaitForSeconds(t);
        // Détruit visuels enfants sauf SoilVisual (on reste à SoilAdded ? ou Empty ?)
        foreach (Transform child in transform)
        {
            if (child.name == "SoilVisual") continue;
            Destroy(child.gameObject);
        }
        if (soilVisual != null) soilVisual.SetActive(false);
        State = PotState.Empty;
        PlantedSeedIndex = -1;
        activePlant = null;
    }

    private void DropNextSeed()
    {
        // Chaque plante donne la graine de la suivante (sauf la dernière qui débloque la phase boss)
        int nextIdx = PlantedSeedIndex + 1;
        GameManager.Instance.NotifyPlantHarvested(PlantedSeedIndex);

        if (nextIdx < 3)
        {
            // Spawn une graine ramassable à côté du pot
            GameObject seed = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            seed.name = $"DroppedSeed_{nextIdx}";
            seed.tag = "Pickup";
            seed.transform.position = transform.position + new Vector3(0, 1.4f, 0);
            seed.transform.localScale = Vector3.one * 0.18f;
            seed.GetComponent<Renderer>().material.color = GetSeedColor(nextIdx);

            Rigidbody rb = seed.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.mass = 0.05f;

            ItemPickup pickup = seed.AddComponent<ItemPickup>();
            pickup.itemType = ItemType.Seed;
            pickup.seedIndex = nextIdx;
        }
        else
        {
            // Toutes les plantes ordinaires ont été cultivées : on lâche les 3 graines pour la plante boss
            for (int i = 0; i < 3; i++)
            {
                GameObject seed = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                seed.name = $"BossSeed_{i}";
                seed.tag = "Pickup";
                seed.transform.position = transform.position + new Vector3(i * 0.3f - 0.3f, 1.4f, 0);
                seed.transform.localScale = Vector3.one * 0.2f;
                seed.GetComponent<Renderer>().material.color = GetSeedColor(i);

                Rigidbody rb = seed.AddComponent<Rigidbody>();
                rb.useGravity = true;

                ItemPickup pickup = seed.AddComponent<ItemPickup>();
                pickup.itemType = ItemType.Seed;
                pickup.seedIndex = i;
            }
            // Spawn la zone de plantation du boss
            CarnivorousBoss.SpawnBossPot();
        }
    }

    private IEnumerator FlashSoilColor(Color c)
    {
        if (soilVisual == null) yield break;
        Renderer r = soilVisual.GetComponent<Renderer>();
        Color original = r.material.color;
        r.material.color = c;
        yield return new WaitForSeconds(0.5f);
        // On garde la couleur fertilisée (un peu plus sombre)
    }

    public Vector3 GetPlantTargetPosition()
    {
        return transform.position + Vector3.up * 1.2f;
    }
}
