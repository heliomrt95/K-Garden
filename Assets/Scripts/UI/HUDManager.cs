using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construit au runtime le HUD complet : crosshair, inventaire, prompts contextuels,
/// rappel des contrôles et objectif courant. Aucun prefab requis.
/// </summary>
public class HUDManager : MonoBehaviour
{
    private Canvas canvas;
    private Text equippedText;
    private Text interactText;
    private Text controlsText;
    private Text seedsText;
    private Text objectiveText;
    private Text waveText;
    private Image crosshair;

    // Barre de santé de la plante
    private GameObject healthBarRoot;
    private Image healthBarFill;
    private Text healthBarLabel;

    // Barre de croissance
    private GameObject growthBarRoot;
    private Image growthBarFill;

    // Hotbar (4 slots : arrosoir / engrais / spray nuisible / graine)
    private Image[] hotbarSlots = new Image[4];
    private Image[] hotbarBorders = new Image[4];
    private Text[] hotbarLabels = new Text[4];

    private InteractionManager player;

    private static readonly Color[] HotbarTints = {
        new Color(0.30f, 0.55f, 0.95f, 1f),  // arrosoir
        new Color(0.20f, 0.75f, 0.45f, 1f),  // engrais
        new Color(0.85f, 0.20f, 0.20f, 1f),  // anti-nuisibles
        new Color(0.80f, 0.65f, 0.20f, 1f),  // graine
    };
    private static readonly string[] HotbarKeys = { "1", "2", "3", "4" };

    private void Start()
    {
        BuildHUD();
        player = FindObjectOfType<InteractionManager>();
    }

    private void Update()
    {
        if (player == null)
        {
            player = FindObjectOfType<InteractionManager>();
            return;
        }
        UpdateInteractionPrompt();
        UpdateInventory();
        UpdateObjective();
        UpdatePlantBars();
        UpdateWaveCounter();
        UpdateHotbar();
    }

    // ─────────────────────────────────────────────
    // CONSTRUCTION DU CANVAS
    // ─────────────────────────────────────────────
    private void BuildHUD()
    {
        GameObject canvasGO = new GameObject("HUD_Canvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        BuildCrosshair();
        BuildInteractPrompt();
        BuildEquipped();
        BuildSeeds();
        BuildControls();
        BuildObjective();
        BuildHealthBar();
        BuildGrowthBar();
        BuildWaveCounter();
        BuildHotbar();
    }

    private void BuildCrosshair()
    {
        GameObject ch = new GameObject("Crosshair");
        ch.transform.SetParent(canvas.transform, false);
        crosshair = ch.AddComponent<Image>();
        crosshair.color = new Color(1f, 1f, 1f, 0.85f);
        RectTransform rt = crosshair.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(6, 6);
        rt.anchoredPosition = Vector2.zero;

        // Petit anneau autour (sphère blanche transparente)
        GameObject ring = new GameObject("CrosshairRing");
        ring.transform.SetParent(canvas.transform, false);
        Image ri = ring.AddComponent<Image>();
        ri.color = new Color(1f, 1f, 1f, 0.25f);
        RectTransform rrt = ri.rectTransform;
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(18, 18);
    }

    private void BuildInteractPrompt()
    {
        interactText = MakeText("InteractPrompt", new Vector2(0.5f, 0.5f),
            new Vector2(0, 70), new Vector2(800, 50), 28, TextAnchor.MiddleCenter);
        interactText.color = new Color(1f, 1f, 0.85f);
        interactText.text = "";
    }

    private void BuildEquipped()
    {
        equippedText = MakeText("Equipped", new Vector2(0.5f, 0f),
            new Vector2(0, 60), new Vector2(900, 50), 26, TextAnchor.MiddleCenter);
        equippedText.supportRichText = true;
    }

    private void BuildSeeds()
    {
        seedsText = MakeText("Seeds", new Vector2(1f, 0.5f),
            new Vector2(-200, 0), new Vector2(380, 260), 20, TextAnchor.UpperLeft);
        seedsText.supportRichText = true;
    }

    private void BuildControls()
    {
        controlsText = MakeText("Controls", new Vector2(0f, 1f),
            new Vector2(220, -110), new Vector2(420, 240), 19, TextAnchor.UpperLeft);
        controlsText.supportRichText = true;
        controlsText.color = new Color(1f, 1f, 1f, 0.92f);
        controlsText.text =
            "<b>Contrôles</b>\n" +
            "ZQSD / WASD : déplacement\n" +
            "Souris : regarder\n" +
            "E : interagir\n" +
            "Clic gauche : pulvériser\n" +
            "1 arrosoir   2 engrais\n" +
            "3 anti-nuisibles   4 graine";
    }

    private void BuildObjective()
    {
        objectiveText = MakeText("Objective", new Vector2(1f, 1f),
            new Vector2(-260, -80), new Vector2(500, 120), 22, TextAnchor.UpperRight);
        objectiveText.supportRichText = true;
        objectiveText.color = new Color(0.85f, 1f, 0.85f);
    }

    // ─────────────────────────────────────────────
    // BARRES & COMPTEURS DYNAMIQUES
    // ─────────────────────────────────────────────
    private void BuildHealthBar()
    {
        healthBarRoot = new GameObject("HealthBar");
        healthBarRoot.transform.SetParent(canvas.transform, false);
        Image bg = healthBarRoot.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform root = bg.rectTransform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.sizeDelta = new Vector2(320, 24);
        root.anchoredPosition = new Vector2(0, -30);

        // Remplissage
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(healthBarRoot.transform, false);
        healthBarFill = fillGO.AddComponent<Image>();
        healthBarFill.color = new Color(0.85f, 0.25f, 0.25f, 0.95f);
        RectTransform fr = healthBarFill.rectTransform;
        fr.anchorMin = new Vector2(0, 0);
        fr.anchorMax = new Vector2(1, 1);
        fr.pivot = new Vector2(0, 0.5f);
        fr.offsetMin = new Vector2(3, 3);
        fr.offsetMax = new Vector2(-3, -3);

        healthBarLabel = MakeText("HealthLabel", new Vector2(0.5f, 1f),
            new Vector2(0, -54), new Vector2(320, 24), 16, TextAnchor.MiddleCenter);
        healthBarLabel.color = new Color(1f, 1f, 1f, 0.85f);
        healthBarLabel.supportRichText = true;

        healthBarRoot.SetActive(false);
    }

    private void BuildGrowthBar()
    {
        growthBarRoot = new GameObject("GrowthBar");
        growthBarRoot.transform.SetParent(canvas.transform, false);
        Image bg = growthBarRoot.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform root = bg.rectTransform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(220, 10);
        root.anchoredPosition = new Vector2(0, 35);

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(growthBarRoot.transform, false);
        growthBarFill = fillGO.AddComponent<Image>();
        growthBarFill.color = new Color(0.55f, 0.85f, 0.35f, 0.95f);
        RectTransform fr = growthBarFill.rectTransform;
        fr.anchorMin = new Vector2(0, 0);
        fr.anchorMax = new Vector2(1, 1);
        fr.pivot = new Vector2(0, 0.5f);
        fr.offsetMin = new Vector2(2, 2);
        fr.offsetMax = new Vector2(-2, -2);

        growthBarRoot.SetActive(false);
    }

    private void BuildWaveCounter()
    {
        waveText = MakeText("WaveCounter", new Vector2(1f, 1f),
            new Vector2(-260, -180), new Vector2(500, 60), 20, TextAnchor.UpperRight);
        waveText.supportRichText = true;
        waveText.color = new Color(1f, 0.85f, 0.65f);
        waveText.text = "";
    }

    private void BuildHotbar()
    {
        const float slotSize = 64f;
        const float spacing = 14f;
        float totalWidth = 4 * slotSize + 3 * spacing;
        float startX = -totalWidth / 2f + slotSize / 2f;

        for (int i = 0; i < 4; i++)
        {
            GameObject slotGO = new GameObject($"Slot_{i}");
            slotGO.transform.SetParent(canvas.transform, false);
            Image bg = slotGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            RectTransform rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(slotSize, slotSize);
            rt.anchoredPosition = new Vector2(startX + i * (slotSize + spacing), 110);

            // Bordure (image transparente affichée seulement quand le slot est équipé)
            GameObject borderGO = new GameObject("Border");
            borderGO.transform.SetParent(slotGO.transform, false);
            Image border = borderGO.AddComponent<Image>();
            border.color = new Color(1f, 0.85f, 0.3f, 0.95f);
            RectTransform br = border.rectTransform;
            br.anchorMin = new Vector2(0, 0);
            br.anchorMax = new Vector2(1, 1);
            br.offsetMin = new Vector2(-3, -3);
            br.offsetMax = new Vector2(3, 3);
            border.raycastTarget = false;
            border.gameObject.SetActive(false);
            // Place la bordure derrière le fond
            borderGO.transform.SetSiblingIndex(0);
            hotbarBorders[i] = border;

            // Icône de couleur (carré central)
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(slotGO.transform, false);
            Image icon = iconGO.AddComponent<Image>();
            icon.color = HotbarTints[i];
            RectTransform ir = icon.rectTransform;
            ir.anchorMin = new Vector2(0, 0);
            ir.anchorMax = new Vector2(1, 1);
            ir.offsetMin = new Vector2(10, 18);
            ir.offsetMax = new Vector2(-10, -10);
            icon.raycastTarget = false;
            hotbarSlots[i] = icon;

            // Numéro de touche (1 à 4) en bas
            GameObject keyGO = new GameObject("KeyLabel");
            keyGO.transform.SetParent(slotGO.transform, false);
            Text keyTxt = keyGO.AddComponent<Text>();
            keyTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            keyTxt.fontSize = 16;
            keyTxt.alignment = TextAnchor.MiddleCenter;
            keyTxt.color = new Color(1f, 1f, 1f, 0.85f);
            keyTxt.text = HotbarKeys[i];
            keyTxt.raycastTarget = false;
            RectTransform kr = keyTxt.rectTransform;
            kr.anchorMin = new Vector2(0, 0);
            kr.anchorMax = new Vector2(1, 0);
            kr.pivot = new Vector2(0.5f, 0f);
            kr.sizeDelta = new Vector2(slotSize, 16);
            kr.anchoredPosition = new Vector2(0, 1);
            hotbarLabels[i] = keyTxt;
        }
    }

    private Text MakeText(string name, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(canvas.transform, false);
        Text t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.4f, -1.4f);
        return t;
    }

    // ─────────────────────────────────────────────
    // UPDATES
    // ─────────────────────────────────────────────
    private void UpdateInteractionPrompt()
    {
        string hint = player.GetInteractionHint();
        interactText.text = string.IsNullOrEmpty(hint) ? "" : hint;
    }

    private void UpdateInventory()
    {
        string equipped;
        switch (player.equippedItem)
        {
            case ItemType.WateringCan:     equipped = "Arrosoir"; break;
            case ItemType.FertilizerSpray: equipped = "Spray d'engrais"; break;
            case ItemType.PestSpray:       equipped = "Spray anti-nuisibles"; break;
            case ItemType.SoilHandful:     equipped = "Poignée de terre"; break;
            case ItemType.Seed:
                equipped = (player.equippedSeedIndex >= 0 && GameManager.Instance != null)
                    ? $"Graine : {GameManager.Instance.plantDataset[player.equippedSeedIndex].plantName}"
                    : "Graine";
                break;
            default: equipped = "<i>aucun objet équipé</i>"; break;
        }
        equippedText.text = $"<b>Main :</b> {equipped}";

        if (GameManager.Instance == null) return;
        string s = "<b>Inventaire</b>\n";
        for (int i = 0; i < GameManager.Instance.plantDataset.Length; i++)
        {
            int count = player.GetSeedCount(i);
            bool unlocked = i < GameManager.Instance.seedsUnlocked;
            if (!unlocked && count == 0) continue;
            string n = GameManager.Instance.plantDataset[i].plantName;
            s += $"• {n} <color=#A0E0A0>×{count}</color>\n";
        }
        seedsText.text = s;
    }

    private void UpdateObjective()
    {
        if (GameManager.Instance == null) return;
        var gm = GameManager.Instance;
        string obj;

        if (gm.keyObtained)
            obj = "Vous avez trouvé la clé.";
        else if (gm.bossSeedsAvailable)
            obj = "Plantez les 3 graines dans le pot central, puis nourrissez la plante carnivore.";
        else
        {
            obj = "Faites pousser une plante carnivore.";
            for (int i = 0; i < gm.plantsCompleted.Length; i++)
            {
                if (!gm.plantsCompleted[i] && i < gm.seedsUnlocked)
                {
                    obj = $"Cultiver : <b>{gm.plantDataset[i].plantName}</b>";
                    break;
                }
            }
        }
        objectiveText.text = $"<b>Objectif</b>\n{obj}";
    }

    private void UpdatePlantBars()
    {
        PlantManager plant = PlantManager.ActivePlant;
        bool show = plant != null && !plant.isMature;

        healthBarRoot.SetActive(show);
        growthBarRoot.SetActive(show);

        if (!show) return;

        // Santé : remplissage horizontal + couleur qui passe du rouge au vert
        float hf = plant.HealthFraction;
        healthBarFill.rectTransform.anchorMax = new Vector2(hf, 1f);
        healthBarFill.color = Color.Lerp(new Color(0.85f, 0.2f, 0.2f), new Color(0.35f, 0.85f, 0.35f), hf);
        if (GameManager.Instance != null && plant.seedIndex >= 0 && plant.seedIndex < GameManager.Instance.plantDataset.Length)
            healthBarLabel.text = $"<b>{GameManager.Instance.plantDataset[plant.seedIndex].plantName}</b>  {Mathf.RoundToInt(hf * 100f)}%";
        else
            healthBarLabel.text = $"{Mathf.RoundToInt(hf * 100f)}%";

        // Croissance
        float gf = plant.GrowthProgress;
        growthBarFill.rectTransform.anchorMax = new Vector2(gf, 1f);
    }

    private void UpdateWaveCounter()
    {
        if (WaveManager.Instance == null || !WaveManager.Instance.IsActive)
        {
            waveText.text = "";
            return;
        }
        int killed = WaveManager.Instance.KillCount;
        int total = WaveManager.Instance.TotalEnemies;
        waveText.text = $"<b>Nuisibles</b>\n{killed} / {total}";
    }

    private void UpdateHotbar()
    {
        ItemType eq = player.equippedItem;
        int activeIndex = -1;
        switch (eq)
        {
            case ItemType.WateringCan:     activeIndex = 0; break;
            case ItemType.FertilizerSpray: activeIndex = 1; break;
            case ItemType.PestSpray:       activeIndex = 2; break;
            case ItemType.Seed:            activeIndex = 3; break;
        }

        for (int i = 0; i < 4; i++)
        {
            hotbarBorders[i].gameObject.SetActive(i == activeIndex);
            // Atténue les slots dont l'item n'est pas encore possédé
            bool owned = SlotOwned(i);
            Color c = HotbarTints[i];
            c.a = owned ? 1f : 0.35f;
            hotbarSlots[i].color = c;
            hotbarLabels[i].color = new Color(1f, 1f, 1f, owned ? 0.85f : 0.4f);
        }
    }

    private bool SlotOwned(int slot)
    {
        if (player == null) return false;
        switch (slot)
        {
            case 0: return player.HasItem(ItemType.WateringCan);
            case 1: return player.HasItem(ItemType.FertilizerSpray);
            case 2: return player.HasItem(ItemType.PestSpray);
            case 3:
                if (GameManager.Instance == null) return false;
                for (int i = 0; i < GameManager.Instance.plantDataset.Length; i++)
                    if (player.GetSeedCount(i) > 0) return true;
                return false;
        }
        return false;
    }
}
