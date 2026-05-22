// Player.cs
// -----------------------------------------------------------------------------
// Contrôle FPS du joueur + interaction au clic.
//
// À mettre sur : un GameObject vide "Player" qui contient la Main Camera en enfant.
// Composant requis : CharacterController.
//
// Interactions :
//   - Clic GAUCHE rapide    : ramasser un Pickup ou prendre dans une Source
//   - Clic GAUCHE MAINTENU  : agir sur un Pot avec terre/graine/eau
//                             → barre verte au centre, se remplit en continu
//   - Clic GAUCHE SPAMMÉ    : agir sur un Pot avec le spray (anti-nuisibles)
//                             → barre orange qui monte par clic et redescend toute seule
//                             → à 100 % : 1 nuisible éliminé, la barre repart de zéro
//   - Touche R              : reposer l'objet tenu
// -----------------------------------------------------------------------------

using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    public float vitesse = 4f;
    public float sensibiliteSouris = 2f;
    public float porteeInteraction = 3f;

    [Header("Spray (anti-nuisibles, spam-clic)")]
    public float gainParClicSpray = 0.16f;     // 16 % par clic → ~7 clics pour 1 nuisible
    public float baisseSprayParSeconde = 0.45f; // la barre se vide à 45 % / s

    private CharacterController controleur;
    private Camera cam;
    private float rotationVerticale = 0f;
    private float prochainBruitDePas = 0f;
    const float INTERVALLE_PAS = 0.45f;

    // ── Clic maintenu (terre / graine / eau) ─────────────────────────────────
    private GameObject cibleActuelle;
    private string itemAuDebut;
    private float progression;
    private float dureeRequise;
    private string messageInfo;

    // ── Spam-clic (spray) ────────────────────────────────────────────────────
    private Pot cibleSpray;
    private float progressionSpray; // 0..1

    // ── HUD ──────────────────────────────────────────────────────────────────
    private string viseDebug = "";
    private Pot potVise;            // pot actuellement sous le viseur (pour la jauge de vie)

    void Start()
    {
        controleur = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Deplacement();
        Rotation();
        GererInteraction();
        if (Input.GetKeyDown(KeyCode.R)) { AnnulerAction(); GameState.LibererMain(); }
    }

    void Deplacement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 direction = transform.right * h + transform.forward * v;
        direction.y = -9.81f * Time.deltaTime;
        controleur.Move(direction * vitesse * Time.deltaTime);

        // Bruit de pas : joué à intervalle régulier seulement quand on bouge
        bool bouge = Mathf.Abs(h) > 0.05f || Mathf.Abs(v) > 0.05f;
        if (bouge && Time.time >= prochainBruitDePas && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(AudioManager.Instance.footstep, 0.7f);
            prochainBruitDePas = Time.time + INTERVALLE_PAS;
        }
    }

    void Rotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensibiliteSouris;
        float mouseY = Input.GetAxis("Mouse Y") * sensibiliteSouris;
        transform.Rotate(0, mouseX, 0);
        rotationVerticale -= mouseY;
        rotationVerticale = Mathf.Clamp(rotationVerticale, -80f, 80f);
        cam.transform.localEulerAngles = new Vector3(rotationVerticale, 0, 0);
    }

    // ── Boucle d'interaction ─────────────────────────────────────────────────
    void GererInteraction()
    {
        Vector3 centreEcran = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
        Ray rayon = cam.ScreenPointToRay(centreEcran);
        Physics.Raycast(rayon, out RaycastHit hit, porteeInteraction);
        GameObject cible = hit.collider != null ? hit.collider.gameObject : null;

        // HUD "Visé : XXX [Composants]" + mémorise le pot pour la jauge de vie
        potVise = null;
        if (cible == null) viseDebug = "";
        else
        {
            string root = cible.transform.root.name;
            string composants = "";
            if (cible.GetComponentInParent<Pickup>() != null) composants += " [Pickup]";
            if (cible.GetComponentInParent<Source>() != null) composants += " [Source]";
            Pot p = cible.GetComponentInParent<Pot>();
            if (p != null) { composants += " [Pot]"; potVise = p; }
            viseDebug = "Visé : " + root + composants;
        }

        // ── Spam-clic spray (cas spécial, prioritaire) ───────────────────────
        if (GameState.itemEnMain == "spray" && potVise != null && potVise.NuisiblesActifs() > 0)
        {
            // 1) Tout clic ajoute du progrès
            if (Input.GetMouseButtonDown(0))
            {
                cibleSpray = potVise;
                progressionSpray = Mathf.Clamp01(progressionSpray + gainParClicSpray);
                if (AudioManager.Instance != null)
                    AudioManager.Instance.Play(AudioManager.Instance.spray, 0.8f);
            }

            // 2) ⚠️ ORDRE CRITIQUE : vérifier le seuil AVANT la décroissance.
            //    Si on inversait (décroissance d'abord), la barre redescendrait
            //    sous 1.0 dans la même frame que le clic final → le nuisible
            //    ne mourrait jamais bien que la barre soit visuellement pleine.
            if (cibleSpray != null && progressionSpray >= 1f)
            {
                cibleSpray.TuerUnNuisible();
                progressionSpray = 0f;
                if (cibleSpray.NuisiblesActifs() == 0) cibleSpray = null;
            }

            // 3) Décroissance progressive entre les clics
            if (cibleSpray != null)
                progressionSpray = Mathf.Max(0f, progressionSpray - baisseSprayParSeconde * Time.deltaTime);

            return; // pas de logique clic-maintenu quand on est en mode spam
        }

        // Sortie de portée ou changement de cible → reset le spam
        if (cibleSpray != null && (potVise != cibleSpray || cibleSpray.NuisiblesActifs() == 0))
            cibleSpray = null;

        // ── Clic appuyé pour la première fois (pickup/source/clic-maintenu) ──
        if (Input.GetMouseButtonDown(0))
        {
            if (cible == null) return;

            Pickup pickup = cible.GetComponentInParent<Pickup>();
            if (pickup != null) { pickup.Prendre(); return; }

            Source source = cible.GetComponentInParent<Source>();
            if (source != null) { source.Prendre(); return; }

            Pot pot = cible.GetComponentInParent<Pot>();
            if (pot != null)
            {
                float duree = pot.DureeAction(GameState.itemEnMain);
                if (duree > 0f)
                    DemarrerAction(pot.gameObject, GameState.itemEnMain, duree);
                else
                    messageInfo = pot.RaisonRefus(GameState.itemEnMain);
                return;
            }
        }

        // ── Bouton maintenu : progression ────────────────────────────────────
        if (Input.GetMouseButton(0) && cibleActuelle != null)
        {
            if (cible == null ||
                (cible != cibleActuelle && cible.transform.root != cibleActuelle.transform.root))
            { AnnulerAction(); return; }

            if (GameState.itemEnMain != itemAuDebut) { AnnulerAction(); return; }

            progression += Time.deltaTime;
            if (progression >= dureeRequise) ValiderAction();
        }

        if (Input.GetMouseButtonUp(0) && cibleActuelle != null) AnnulerAction();
    }

    void DemarrerAction(GameObject cible, string item, float duree)
    {
        cibleActuelle = cible;
        itemAuDebut = item;
        progression = 0f;
        dureeRequise = duree;
        messageInfo = "";
    }

    void ValiderAction()
    {
        if (cibleActuelle == null) return;
        Pot pot = cibleActuelle.GetComponent<Pot>();
        if (pot != null) pot.ValiderAction(itemAuDebut);
        ResetAction();
    }

    void AnnulerAction() { ResetAction(); }

    void ResetAction()
    {
        cibleActuelle = null;
        itemAuDebut = "";
        progression = 0f;
        dureeRequise = 0f;
    }

    // ── HUD ──────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        // Item en main
        string texte = GameState.itemEnMain == ""
            ? "Mains vides"
            : "En main : " + GameState.itemEnMain + "  (R = reposer)";
        GUI.Label(new Rect(10, 10, 400, 25), texte);

        // Viseur
        GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 10, 20, 20), "+");

        // Texte "Visé : ..."
        if (!string.IsNullOrEmpty(viseDebug))
            GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height / 2f + 10, 400, 20),
                      viseDebug,
                      new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });

        // Jauge de vie + nuisibles du pot visé
        if (potVise != null)
        {
            float w = 200f, h = 10f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.5f - 50f;
            float ratio = Mathf.Clamp01(potVise.vie / potVise.vieMax);

            GUI.color = new Color(0.10f, 0.10f, 0.10f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            Color cVie = potVise.morte ? Color.gray
                       : ratio > 0.5f  ? new Color(0.35f, 0.85f, 0.40f)
                       : ratio > 0.25f ? new Color(0.95f, 0.80f, 0.20f)
                       :                 new Color(0.95f, 0.25f, 0.20f);
            GUI.color = cVie;
            GUI.DrawTexture(new Rect(x, y, w * ratio, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            string info = "Vie " + Mathf.RoundToInt(potVise.vie) + "/" + Mathf.RoundToInt(potVise.vieMax);
            if (potVise.NuisiblesActifs() > 0) info += "  •  Nuisibles : " + potVise.NuisiblesActifs();
            if (potVise.morte) info = "PLANTE MORTE";
            GUI.Label(new Rect(x, y - 18, w, 16), info,
                      new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }

        // Barre verte : clic maintenu (terre/graine/eau)
        if (cibleActuelle != null && dureeRequise > 0f)
            DessinerBarre(progression / dureeRequise, new Color(0.35f, 0.85f, 0.40f));

        // Barre orange : spam-clic (spray)
        if (cibleSpray != null && progressionSpray > 0f)
            DessinerBarre(progressionSpray, new Color(0.95f, 0.55f, 0.15f));

        // Message d'info temporaire (clic refusé)
        if (!string.IsNullOrEmpty(messageInfo))
        {
            float w = 400f;
            GUI.Label(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f + 55f, w, 20f),
                      messageInfo, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }
    }

    void DessinerBarre(float ratio, Color couleur)
    {
        ratio = Mathf.Clamp01(ratio);
        float w = 200f, h = 16f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.5f + 30f;

        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(x - 2, y - 2, w + 4, h + 4), Texture2D.whiteTexture);
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = couleur;
        GUI.DrawTexture(new Rect(x, y, w * ratio, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
