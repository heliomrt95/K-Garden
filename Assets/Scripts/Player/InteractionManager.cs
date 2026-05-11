using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gère :
///   - le raycast d'interaction (touche E)
///   - l'inventaire courant (objet équipé)
///   - le tir avec le spray anti-nuisibles (clic gauche)
///   - le ramassage des graines / outils
/// À placer sur l'objet Player, à côté du PlayerController.
/// </summary>
public class InteractionManager : MonoBehaviour
{
    [Header("Raycast")]
    public Camera playerCamera;
    public float interactDistance = 3f;
    public LayerMask interactLayers = ~0;

    [Header("Tir spray")]
    public float sprayRange = 8f;
    public float sprayCooldown = 0.15f;

    [Header("Inventaire")]
    public ItemType equippedItem = ItemType.None;
    public int equippedSeedIndex = -1;
    private HashSet<ItemType> ownedItems = new HashSet<ItemType>();
    private Dictionary<int, int> seedCounts = new Dictionary<int, int>(); // index -> qty

    private float lastSprayTime;
    private GameObject currentTarget;

    public System.Action<ItemType, int> OnInventoryChanged;

    private void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void Update()
    {
        DetectTarget();
        HandleInteractInput();
        HandleSprayInput();
        HandleScrollInventory();
    }

    private void DetectTarget()
    {
        currentTarget = null;
        Ray r = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(r, out RaycastHit hit, interactDistance, interactLayers, QueryTriggerInteraction.Collide))
        {
            currentTarget = hit.collider.gameObject;
        }
    }

    private void HandleInteractInput()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;
        if (currentTarget == null) return;

        // 1) Pickup
        ItemPickup pickup = currentTarget.GetComponent<ItemPickup>();
        if (pickup != null) { TryPickup(pickup); return; }

        // 2) Tas de terreau → on prend une poignée
        if (currentTarget.CompareTag("SoilPile"))
        {
            ownedItems.Add(ItemType.SoilHandful);
            equippedItem = ItemType.SoilHandful;
            OnInventoryChanged?.Invoke(equippedItem, equippedSeedIndex);
            Debug.Log("[Interaction] Poignée de terre prise.");
            return;
        }

        // 3) Pot → on lui passe l'objet équipé
        PotInteraction pot = currentTarget.GetComponentInParent<PotInteraction>();
        if (pot != null)
        {
            pot.ReceiveInput(equippedItem, equippedSeedIndex);

            // Consommer le seul item consommable : la poignée de terre et la graine
            if (equippedItem == ItemType.SoilHandful) UnequipConsumable();
            if (equippedItem == ItemType.Seed)
            {
                ConsumeSeed(equippedSeedIndex);
            }
            return;
        }

        // 4) Clé finale
        if (currentTarget.CompareTag("Key"))
        {
            GameManager.Instance.NotifyKeyCollected();
            Destroy(currentTarget);
            return;
        }
    }

    private void TryPickup(ItemPickup pickup)
    {
        switch (pickup.itemType)
        {
            case ItemType.Seed:
                if (!seedCounts.ContainsKey(pickup.seedIndex)) seedCounts[pickup.seedIndex] = 0;
                seedCounts[pickup.seedIndex]++;
                equippedItem = ItemType.Seed;
                equippedSeedIndex = pickup.seedIndex;
                Debug.Log($"[Interaction] Graine récupérée (idx {pickup.seedIndex}). Total : {seedCounts[pickup.seedIndex]}");
                Destroy(pickup.gameObject);
                break;
            default:
                ownedItems.Add(pickup.itemType);
                equippedItem = pickup.itemType;
                Debug.Log($"[Interaction] Outil pris : {pickup.itemType}");
                pickup.gameObject.SetActive(false); // outils réutilisables : on les masque
                break;
        }
        OnInventoryChanged?.Invoke(equippedItem, equippedSeedIndex);
    }

    private void HandleSprayInput()
    {
        if (!Input.GetMouseButton(0)) return;
        if (equippedItem != ItemType.PestSpray) return;
        if (Time.time - lastSprayTime < sprayCooldown) return;

        lastSprayTime = Time.time;
        Ray r = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        Debug.DrawRay(r.origin, r.direction * sprayRange, Color.red, 0.1f);

        if (Physics.Raycast(r, out RaycastHit hit, sprayRange))
        {
            PestEnemy pest = hit.collider.GetComponentInParent<PestEnemy>();
            if (pest != null) pest.TakeDamage(1);
        }
    }

    private void HandleScrollInventory()
    {
        // Cycle entre les outils disponibles avec les chiffres 1-4
        if (Input.GetKeyDown(KeyCode.Alpha1) && ownedItems.Contains(ItemType.WateringCan))
            { equippedItem = ItemType.WateringCan; OnInventoryChanged?.Invoke(equippedItem, -1); }
        if (Input.GetKeyDown(KeyCode.Alpha2) && ownedItems.Contains(ItemType.FertilizerSpray))
            { equippedItem = ItemType.FertilizerSpray; OnInventoryChanged?.Invoke(equippedItem, -1); }
        if (Input.GetKeyDown(KeyCode.Alpha3) && ownedItems.Contains(ItemType.PestSpray))
            { equippedItem = ItemType.PestSpray; OnInventoryChanged?.Invoke(equippedItem, -1); }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            // équiper la première graine disponible
            foreach (var kv in seedCounts)
            {
                if (kv.Value > 0)
                {
                    equippedItem = ItemType.Seed;
                    equippedSeedIndex = kv.Key;
                    OnInventoryChanged?.Invoke(equippedItem, equippedSeedIndex);
                    return;
                }
            }
        }
    }

    private void UnequipConsumable()
    {
        ownedItems.Remove(ItemType.SoilHandful);
        equippedItem = ItemType.None;
        OnInventoryChanged?.Invoke(equippedItem, -1);
    }

    private void ConsumeSeed(int idx)
    {
        if (seedCounts.ContainsKey(idx)) seedCounts[idx]--;
        equippedItem = ItemType.None;
        equippedSeedIndex = -1;
        OnInventoryChanged?.Invoke(equippedItem, -1);
    }

    public bool HasSeed(int idx) => seedCounts.ContainsKey(idx) && seedCounts[idx] > 0;
    public bool HasAllThreeBossSeeds() => HasSeed(0) && HasSeed(1) && HasSeed(2);
}
