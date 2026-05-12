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
    private Image crosshair;

    private InteractionManager player;

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
}
