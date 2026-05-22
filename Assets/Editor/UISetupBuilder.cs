// UISetupBuilder.cs
// -----------------------------------------------------------------------------
// Construit automatiquement les Canvas du Menu Principal et du Menu Pause +
// branche les scripts MainMenuController, PauseMenu et SettingsController.
//
// Menus :
//   - Tools > UI > Créer Menu Principal (dans la scène ouverte)
//   - Tools > UI > Créer Menu Pause   (dans la scène de jeu ouverte)
//
// Tout est posé avec :
//   - les bons anchors / pivots
//   - les bons VerticalLayoutGroup / GridLayoutGroup
//   - les boutons branchés aux scripts via UnityEventTools (persistants)
//
// Les sprites sont les placeholders Unity (UISprite arrondi). Tu remplaceras
// par tes propres assets Figma plus tard, en gardant la hiérarchie intacte.
// -----------------------------------------------------------------------------

using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UISetupBuilder
{
    // ── Palette ──────────────────────────────────────────────────────────────
    static readonly Color CMarron   = new Color(0.478f, 0.310f, 0.208f); // boutons
    static readonly Color CBeige    = new Color(0.910f, 0.910f, 0.780f); // textes boutons
    static readonly Color CVertLogo = new Color(0.627f, 0.784f, 0.475f); // texte "Menu"
    static readonly Color CFondVert = new Color(0.55f, 0.78f, 0.55f);    // placeholder fond
    static readonly Color CSliderBg = new Color(0.85f, 0.65f, 0.45f);    // barre sliders

    // ── Application du logo sur les Logo_KGarden de la scène ─────────────────
    [MenuItem("Tools/UI/Appliquer Logo K-Garden")]
    public static void AppliquerLogo()
    {
        Sprite sprite = ChargerSpriteLogo();
        if (sprite == null)
        {
            EditorUtility.DisplayDialog("Logo introuvable",
                "Aucun logo trouvé. Essayé : Assets/logo.png, Assets/Logo.png, Assets/logo 1.png.\n" +
                "Place ton logo et relance.", "OK");
            return;
        }

        int n = 0;
        foreach (var img in Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
        {
            if (img.gameObject.name != "Logo_KGarden") continue;
            AppliquerSpriteSurLogo(img, sprite);
            n++;
        }
        if (n > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        EditorUtility.DisplayDialog("Logo K-Garden",
            n + " logo(s) mis à jour.\n" +
            (n == 0 ? "Astuce : ouvre la scène MainMenu d'abord." : ""), "OK");
    }

    static Sprite ChargerSpriteLogo()
    {
        // Cherche n'importe quel asset Texture2D dont le nom commence par "logo"
        // (insensible à la casse), peu importe où il est dans Assets/
        string[] guids = AssetDatabase.FindAssets("logo t:Texture2D");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Ignore les sous-packs (Pure Poly, BOXOPHOBIC, etc.) pour ne pas
            // attraper un logo de pack par erreur
            if (path.Contains("Pure Poly") || path.Contains("BOXOPHOBIC")) continue;

            string nom = System.IO.Path.GetFileNameWithoutExtension(path).ToLower();
            if (!nom.StartsWith("logo")) continue;

            // Force l'import en Sprite
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.SaveAndReimport();
            }
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null)
            {
                Debug.Log("[Logo] Trouvé : " + path);
                return s;
            }
        }
        return null;
    }

    static void AppliquerSpriteSurLogo(Image img, Sprite sprite)
    {
        img.sprite = sprite;
        img.color = Color.white;
        img.preserveAspect = true;
        img.type = Image.Type.Simple;

        // Supprime le placeholder texte "K-GARDEN" si présent
        Transform t = img.transform.Find("Placeholder_Text");
        if (t != null) Object.DestroyImmediate(t.gameObject);

        // Adapte la taille en conservant le ratio du sprite
        if (sprite.rect.height > 0)
        {
            float ratio = sprite.rect.width / sprite.rect.height;
            float hauteurCible = 280f; // garde la même hauteur que le placeholder
            img.rectTransform.sizeDelta = new Vector2(hauteurCible * ratio, hauteurCible);
        }
    }

    // ── Reset & reconstruction propre ────────────────────────────────────────
    [MenuItem("Tools/UI/RESET et Reconstruire Menu Principal")]
    public static void ResetEtReconstruire()
    {
        // Supprime tous les Canvas et UIManager existants dans la scène
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            Object.DestroyImmediate(c.gameObject);
        var ui = GameObject.Find("UIManager");
        if (ui != null) Object.DestroyImmediate(ui);

        // Reconstruit
        CreerMenuPrincipalInterne();
    }

    // ── Menu principal ───────────────────────────────────────────────────────
    [MenuItem("Tools/UI/Créer Menu Principal")]
    public static void CreerMenuPrincipal()
    {
        if (!ConfirmerSiNonVide("Menu Principal")) return;
        CreerMenuPrincipalInterne();
    }

    static void CreerMenuPrincipalInterne()
    {
        // Ajoute une Camera si absente (sinon Unity affiche "No cameras rendering")
        AssurerCameraMenu();


        Canvas canvas = CreerCanvas("Canvas_MenuPrincipal");
        AssurerEventSystem();

        // Background plein écran (placeholder vert)
        Image bg = CreerImage(canvas.transform, "Background", CFondVert);
        StretchFull(bg.rectTransform);

        // Panel_MainMenu (stretch full)
        GameObject panelMenu = CreerPanel(canvas.transform, "Panel_MainMenu");
        StretchFull(panelMenu.GetComponent<RectTransform>());

        // Logo à gauche : essaie de charger Assets/logo.png automatiquement.
        // Si absent → placeholder texte "K-GARDEN" qui sera remplacé plus tard.
        Image logo = CreerImage(panelMenu.transform, "Logo_KGarden", Color.white);
        AncrerEnHautGauche(logo.rectTransform, new Vector2(100, -100), new Vector2(700, 280));
        Sprite spriteLogo = ChargerSpriteLogo();
        if (spriteLogo != null)
            AppliquerSpriteSurLogo(logo, spriteLogo);
        else
            AjouterTextePlaceholder(logo.gameObject, "K-GARDEN", 80, CMarron);

        // Label "Menu" en bas à gauche — couleur foncée + outline blanc épais
        // pour rester lisible sur n'importe quel fond
        var labelMenu = CreerTexte(panelMenu.transform, "Label_Menu", "Menu", 72,
                                   new Color(0.20f, 0.45f, 0.20f)); // vert foncé
        labelMenu.outlineColor = Color.white;
        labelMenu.outlineWidth = 0.25f;
        AncrerEnBasGauche(labelMenu.rectTransform, new Vector2(180, 80), new Vector2(300, 80));

        // MenuButtons à droite, vertical
        GameObject menuButtons = CreerConteneur(panelMenu.transform, "MenuButtons");
        var rtMB = menuButtons.GetComponent<RectTransform>();
        rtMB.anchorMin = new Vector2(1, 0.5f);
        rtMB.anchorMax = new Vector2(1, 0.5f);
        rtMB.pivot = new Vector2(1, 0.5f);
        rtMB.anchoredPosition = new Vector2(-100, 0);
        rtMB.sizeDelta = new Vector2(350, 500);
        ConfigurerVerticalLayout(menuButtons, 30, TextAnchor.MiddleRight);

        Button btnJouer    = CreerBouton(menuButtons.transform, "Button_Jouer",    "JOUER",    320, 100);
        Button btnBoutique = CreerBouton(menuButtons.transform, "Button_Boutique", "BOUTIQUE", 320, 100);
        Button btnReglage  = CreerBouton(menuButtons.transform, "Button_Reglage",  "REGLAGE",  320, 100);

        // Panel_Settings (caché au start)
        GameObject panelSettings = ConstruirePanelSettings(canvas.transform);
        panelSettings.SetActive(false);

        // UIManager
        GameObject manager = new GameObject("UIManager");
        manager.transform.SetParent(canvas.transform.parent ?? canvas.transform, false);
        MainMenuController mc = manager.AddComponent<MainMenuController>();
        mc.panelMenu = panelMenu;
        mc.panelSettings = panelSettings;

        // SettingsController sur le panel
        var sc = panelSettings.GetComponent<SettingsController>();

        // Branche les boutons
        AjouterListener(btnJouer.onClick, mc, "Jouer");
        AjouterListener(btnBoutique.onClick, mc, "OuvrirBoutique");
        AjouterListener(btnReglage.onClick, mc, "OuvrirReglages");

        // Bouton TERMINE du panel settings → ferme et retourne au menu
        var btnTermine = panelSettings.transform.Find("Button_Termine").GetComponent<Button>();
        AjouterListener(btnTermine.onClick, sc, "Terminer");

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Selection.activeGameObject = canvas.gameObject;
        Debug.Log("✅ Menu Principal créé. Tu peux remplacer le Background et le Logo par tes propres sprites.");
    }

    // ── Menu pause ───────────────────────────────────────────────────────────
    [MenuItem("Tools/UI/Créer Menu Pause")]
    public static void CreerMenuPause()
    {
        Canvas canvas = CreerCanvas("Canvas_Pause");
        canvas.sortingOrder = 10; // au-dessus du jeu
        AssurerEventSystem();

        // Fond noir semi-transparent
        Image overlay = CreerImage(canvas.transform, "Overlay", new Color(0, 0, 0, 0.5f));
        StretchFull(overlay.rectTransform);

        // Panel_Pause (centré)
        GameObject panelPause = CreerPanel(canvas.transform, "Panel_Pause");
        StretchFull(panelPause.GetComponent<RectTransform>());

        GameObject buttons = CreerConteneur(panelPause.transform, "PauseButtons");
        var rtB = buttons.GetComponent<RectTransform>();
        rtB.anchorMin = rtB.anchorMax = new Vector2(0.5f, 0.5f);
        rtB.pivot = new Vector2(0.5f, 0.5f);
        rtB.anchoredPosition = Vector2.zero;
        rtB.sizeDelta = new Vector2(400, 350);
        ConfigurerVerticalLayout(buttons, 40, TextAnchor.MiddleCenter);

        Button btnReprendre = CreerBouton(buttons.transform, "Button_Reprendre", "REVENIR AU JEUX", 360, 120);
        Button btnReglage   = CreerBouton(buttons.transform, "Button_Reglage",   "REGLAGE",          360, 100);

        // Panel Settings (réutilise la même structure)
        GameObject panelSettings = ConstruirePanelSettings(canvas.transform);
        panelSettings.SetActive(false);

        // Script PauseMenu sur le canvas
        PauseMenu pm = canvas.gameObject.AddComponent<PauseMenu>();
        pm.panelPause = panelPause;
        pm.panelSettings = panelSettings;

        var sc = panelSettings.GetComponent<SettingsController>();
        AjouterListener(btnReprendre.onClick, pm, "Reprendre");
        AjouterListener(btnReglage.onClick, pm, "OuvrirReglages");

        var btnTermine = panelSettings.transform.Find("Button_Termine").GetComponent<Button>();
        AjouterListener(btnTermine.onClick, sc, "Terminer");

        // Au démarrage le canvas pause est invisible (PauseMenu.Start s'en charge)
        panelPause.SetActive(false);

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Selection.activeGameObject = canvas.gameObject;
        Debug.Log("✅ Menu Pause créé. Touche Échap en jeu pour l'ouvrir.");
    }

    // ── Construction du Panel Settings (commun aux 2 menus) ──────────────────
    static GameObject ConstruirePanelSettings(Transform parent)
    {
        GameObject panel = CreerPanel(parent, "Panel_Settings");
        StretchFull(panel.GetComponent<RectTransform>());

        // Voile sombre
        Image overlay = CreerImage(panel.transform, "BlurOverlay", new Color(0, 0, 0, 0.35f));
        StretchFull(overlay.rectTransform);

        // Section SONS (label + slider) en haut gauche
        var lblSons = CreerTexte(panel.transform, "Label_Sons", "SONS", 36, Color.white);
        AncrerCentre(lblSons.rectTransform, new Vector2(-450, 180), new Vector2(400, 50));
        Slider sliderSons = CreerSlider(panel.transform, "Slider_Sons", new Vector2(-450, 120), new Vector2(500, 24));

        // Section EFFETS SONORES en haut droite
        var lblEff = CreerTexte(panel.transform, "Label_Effets", "EFFETS SONORES", 36, Color.white);
        AncrerCentre(lblEff.rectTransform, new Vector2(450, 180), new Vector2(500, 50));
        Slider sliderEffets = CreerSlider(panel.transform, "Slider_Effets", new Vector2(450, 120), new Vector2(500, 24));

        // Grille 4 boutons
        GameObject grid = CreerConteneur(panel.transform, "OptionsGrid");
        var rtGrid = grid.GetComponent<RectTransform>();
        rtGrid.anchorMin = rtGrid.anchorMax = new Vector2(0.5f, 0.5f);
        rtGrid.pivot = new Vector2(0.5f, 0.5f);
        rtGrid.anchoredPosition = new Vector2(0, -60);
        rtGrid.sizeDelta = new Vector2(950, 200);

        GridLayoutGroup glg = grid.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(450, 80);
        glg.spacing = new Vector2(30, 30);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;
        glg.childAlignment = TextAnchor.MiddleCenter;

        Button bGraph = CreerBouton(grid.transform, "Button_Graphique",  "OPTIONS GRAPHIQUE",  450, 80);
        Button bCtrl  = CreerBouton(grid.transform, "Button_Controle",   "CONTROLE",            450, 80);
        Button bSkin  = CreerBouton(grid.transform, "Button_PersoSkin",  "PERSONNALISATION SKIN", 450, 80);
        Button bLang  = CreerBouton(grid.transform, "Button_Langues",    "LANGUES",             450, 80);

        // Bouton TERMINE en bas centre
        Button btnTermine = CreerBouton(panel.transform, "Button_Termine", "TERMINE", 280, 90);
        var rtT = btnTermine.GetComponent<RectTransform>();
        rtT.anchorMin = rtT.anchorMax = new Vector2(0.5f, 0);
        rtT.pivot = new Vector2(0.5f, 0);
        rtT.anchoredPosition = new Vector2(0, 80);

        // Script SettingsController + branchements
        SettingsController sc = panel.AddComponent<SettingsController>();
        sc.sliderSons = sliderSons;
        sc.sliderEffets = sliderEffets;
        AjouterListener(bGraph.onClick, sc, "ActionGraphique");
        AjouterListener(bCtrl.onClick,  sc, "ActionControle");
        AjouterListener(bSkin.onClick,  sc, "ActionSkin");
        AjouterListener(bLang.onClick,  sc, "ActionLangues");

        return panel;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers de construction
    // ─────────────────────────────────────────────────────────────────────────

    static bool ConfirmerSiNonVide(string nom)
    {
        Canvas existant = Object.FindAnyObjectByType<Canvas>();
        if (existant == null) return true;
        return EditorUtility.DisplayDialog("Canvas déjà présent",
            "Un Canvas existe déjà dans la scène. Créer le " + nom + " quand même ?",
            "Oui", "Annuler");
    }

    static Canvas CreerCanvas(string nom)
    {
        GameObject go = new GameObject(nom);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static void AssurerEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    static void AssurerCameraMenu()
    {
        // Le Canvas en Screen Space Overlay marche sans Camera, mais Unity
        // affiche "No cameras rendering" dans le Game View tant qu'il n'y en
        // a aucune. On ajoute donc une Camera minimale, fond gris.
        if (Object.FindAnyObjectByType<Camera>() != null) return;
        GameObject cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        Camera c = cam.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
        cam.AddComponent<AudioListener>();
    }

    static Image CreerImage(Transform parent, string nom, Color couleur)
    {
        GameObject go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        Image img = go.AddComponent<Image>();
        img.color = couleur;
        return img;
    }

    static GameObject CreerPanel(Transform parent, string nom)
    {
        GameObject go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static GameObject CreerConteneur(Transform parent, string nom)
    {
        GameObject go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static TextMeshProUGUI CreerTexte(Transform parent, string nom, string texte, int taille, Color couleur)
    {
        GameObject go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = texte;
        tmp.fontSize = taille;
        tmp.color = couleur;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineColor = new Color(0, 0, 0, 0.8f);
        tmp.outlineWidth = 0.15f;
        return tmp;
    }

    static Sprite SpriteArrondi()
    {
        // Sprite Unity built-in arrondi avec 9-slice intégré
        return (Sprite)AssetDatabase.GetBuiltinExtraResource(typeof(Sprite), "UI/Skin/UISprite.psd");
    }

    static Button CreerBouton(Transform parent, string nom, string texte, float largeur, float hauteur)
    {
        GameObject go = new GameObject(nom);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(largeur, hauteur);

        Image img = go.AddComponent<Image>();
        img.sprite = SpriteArrondi();
        img.type = Image.Type.Sliced;
        img.color = CMarron;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = CMarron;
        cb.highlightedColor = new Color(0.60f, 0.40f, 0.28f);
        cb.pressedColor     = new Color(0.35f, 0.22f, 0.15f);
        btn.colors = cb;

        // Garantit que le bouton garde sa taille même dans un LayoutGroup
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = largeur;
        le.preferredHeight = hauteur;

        // Texte enfant
        var tmp = CreerTexte(go.transform, "Text", texte, Mathf.RoundToInt(hauteur * 0.40f), CBeige);
        StretchFull(tmp.rectTransform);

        return btn;
    }

    static Slider CreerSlider(Transform parent, string nom, Vector2 ancrePos, Vector2 taille)
    {
        GameObject go = new GameObject(nom);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = ancrePos;
        rt.sizeDelta = taille;

        // Background
        Image bg = CreerImage(go.transform, "Background", CSliderBg);
        bg.sprite = SpriteArrondi();
        bg.type = Image.Type.Sliced;
        StretchFull(bg.rectTransform);

        // Fill Area > Fill
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        var rtFA = fillArea.AddComponent<RectTransform>();
        rtFA.anchorMin = new Vector2(0, 0); rtFA.anchorMax = new Vector2(1, 1);
        rtFA.offsetMin = new Vector2(5, 5); rtFA.offsetMax = new Vector2(-5, -5);

        Image fill = CreerImage(fillArea.transform, "Fill", new Color(0.55f, 0.35f, 0.15f, 0));
        StretchFull(fill.rectTransform);

        // Handle Slide Area > Handle
        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(go.transform, false);
        var rtHA = handleArea.AddComponent<RectTransform>();
        rtHA.anchorMin = new Vector2(0, 0); rtHA.anchorMax = new Vector2(1, 1);
        rtHA.offsetMin = new Vector2(8, 0); rtHA.offsetMax = new Vector2(-8, 0);

        Image handle = CreerImage(handleArea.transform, "Handle", Color.black);
        var rtH = handle.rectTransform;
        rtH.sizeDelta = new Vector2(6, taille.y + 8);

        Slider slider = go.AddComponent<Slider>();
        slider.targetGraphic = handle;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f;
        slider.value = 0.7f;

        return slider;
    }

    // ── Anchoring helpers ────────────────────────────────────────────────────
    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void AncrerCentre(RectTransform rt, Vector2 pos, Vector2 taille)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = taille;
    }

    static void AncrerEnHautGauche(RectTransform rt, Vector2 pos, Vector2 taille)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = taille;
    }

    static void AncrerEnBasGauche(RectTransform rt, Vector2 pos, Vector2 taille)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta = taille;
    }

    static void ConfigurerVerticalLayout(GameObject go, float spacing, TextAnchor alignement)
    {
        VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.childAlignment = alignement;
        vlg.childControlHeight = false;
        vlg.childControlWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = false;
    }

    static void AjouterTextePlaceholder(GameObject parent, string texte, int taille, Color couleur)
    {
        var tmp = CreerTexte(parent.transform, "Placeholder_Text", texte, taille, couleur);
        StretchFull(tmp.rectTransform);
    }

    // Ajoute un handler persistant qui sera sauvegardé dans la scène
    static void AjouterListener<T>(UnityEngine.Events.UnityEvent ev, T cible, string nomMethode) where T : Object
    {
        var methode = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction),
                                                     cible, nomMethode, false, false)
                      as UnityEngine.Events.UnityAction;
        if (methode != null)
            UnityEventTools.AddPersistentListener(ev, methode);
    }
}
