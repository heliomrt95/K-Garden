// Player.cs
// -----------------------------------------------------------------------------
// Contrôle FPS du joueur + interaction au clic.
//
// À mettre sur : un GameObject vide "Player" qui contient la Main Camera en enfant.
// Composant requis : CharacterController.
//
// Interactions :
//   - Clic GAUCHE rapide        : ramasser un Pickup ou prendre dans une Source
//   - Clic GAUCHE MAINTENU      : agir sur un Pot ou une Plante avec l'item en main
//                                 → barre de progression au centre de l'écran
//                                 → si on relâche ou bouge avant la fin, l'action s'annule
//   - Touche R                  : reposer l'objet tenu
// -----------------------------------------------------------------------------

using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    public float vitesse = 4f;
    public float sensibiliteSouris = 2f;
    public float porteeInteraction = 3f;

    private CharacterController controleur;
    private Camera cam;
    private float rotationVerticale = 0f;

    // ── État de l'action progressive en cours ────────────────────────────────
    private GameObject cibleActuelle;   // Pot ou Plant visé pendant le clic maintenu
    private string itemAuDebut;         // item en main au démarrage de l'action
    private float progression;          // temps écoulé en secondes
    private float dureeRequise;         // durée nécessaire pour valider
    private string messageInfo;         // info temporaire à afficher sous le viseur

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

        // 1) Clic appuyé pour la première fois cette frame
        if (Input.GetMouseButtonDown(0))
        {
            if (cible == null) return;

            // Sources et Pickups : action instantanée, pas de clic maintenu
            Pickup pickup = cible.GetComponentInParent<Pickup>();
            if (pickup != null) { pickup.Prendre(); return; }

            Source source = cible.GetComponentInParent<Source>();
            if (source != null) { source.Prendre(); return; }

            // Pot : action progressive
            Pot pot = cible.GetComponentInParent<Pot>();
            if (pot != null)
            {
                float duree = pot.DureeAction(GameState.itemEnMain);
                if (duree > 0f)
                {
                    DemarrerAction(pot.gameObject, GameState.itemEnMain, duree);
                }
                else
                {
                    messageInfo = pot.RaisonRefus(GameState.itemEnMain);
                }
                return;
            }

            // Plante : action progressive (arrosage seulement)
            Plant plante = cible.GetComponent<Plant>();
            if (plante != null && GameState.itemEnMain == "eau")
            {
                DemarrerAction(plante.gameObject, "eau", 1.0f);
                return;
            }
        }

        // 2) Bouton maintenu : on continue à incrémenter la progression
        if (Input.GetMouseButton(0) && cibleActuelle != null)
        {
            // Cible perdue (sortie de portée, autre objet sous le viseur) ?
            if (cible == null ||
                (cible != cibleActuelle && cible.transform.root != cibleActuelle.transform.root))
            { AnnulerAction(); return; }

            // Item en main a changé ?
            if (GameState.itemEnMain != itemAuDebut) { AnnulerAction(); return; }

            progression += Time.deltaTime;
            if (progression >= dureeRequise) ValiderAction();
        }

        // 3) Bouton relâché : on annule si une action n'était pas terminée
        if (Input.GetMouseButtonUp(0) && cibleActuelle != null)
        {
            AnnulerAction();
        }
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

        Plant plante = cibleActuelle.GetComponent<Plant>();
        if (plante != null) plante.EssayerArroser();

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

    // ── HUD minimal : item en main + viseur + barre de progression ────────────
    void OnGUI()
    {
        // Item en main (coin haut-gauche)
        string texte = GameState.itemEnMain == ""
            ? "Mains vides"
            : "En main : " + GameState.itemEnMain + "  (R = reposer)";
        GUI.Label(new Rect(10, 10, 400, 25), texte);

        // Viseur
        GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 10, 20, 20), "+");

        // Barre de progression (si action en cours)
        if (cibleActuelle != null && dureeRequise > 0f)
        {
            float ratio = Mathf.Clamp01(progression / dureeRequise);
            float w = 200f, h = 16f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.5f + 30f;

            // Fond gris
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(x - 2, y - 2, w + 4, h + 4), Texture2D.whiteTexture);
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            // Remplissage vert
            GUI.color = new Color(0.35f, 0.85f, 0.40f, 1f);
            GUI.DrawTexture(new Rect(x, y, w * ratio, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // Message d'info temporaire (clic refusé)
        if (!string.IsNullOrEmpty(messageInfo))
        {
            float w = 400f;
            GUI.Label(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f + 55f, w, 20f),
                      messageInfo, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }
    }
}
