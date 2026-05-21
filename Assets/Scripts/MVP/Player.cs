// Player.cs
// -----------------------------------------------------------------------------
// Contrôle FPS du joueur + interaction au clic gauche.
//
// À mettre sur : un GameObject vide "Player" qui contient la Main Camera en enfant.
// Composant requis : CharacterController.
//
// Commandes :
//   - W/A/S/D ou flèches : se déplacer
//   - souris             : regarder autour
//   - clic gauche        : interagir avec ce qui est en face
//   - touche R           : reposer l'objet tenu (le retourner à sa place)
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
        if (Input.GetMouseButtonDown(0)) Interagir();
        // Touche R = repose l'objet (utile si on a pris quelque chose par erreur)
        if (Input.GetKeyDown(KeyCode.R)) GameState.LibererMain();
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

    void Interagir()
    {
        Vector3 centreEcran = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
        Ray rayon = cam.ScreenPointToRay(centreEcran);

        if (Physics.Raycast(rayon, out RaycastHit hit, porteeInteraction))
        {
            // On essaie chaque type d'interaction dans l'ordre.
            // GetComponentInParent regarde aussi les parents : pratique car
            // les objets sont souvent composés (parent + plusieurs enfants visuels).

            Pickup pickup = hit.collider.GetComponentInParent<Pickup>();
            if (pickup != null) { pickup.Prendre(); return; }

            Source source = hit.collider.GetComponentInParent<Source>();
            if (source != null) { source.Prendre(); return; }

            Pot pot = hit.collider.GetComponentInParent<Pot>();
            if (pot != null) { pot.Utiliser(); return; }

            Plant plante = hit.collider.GetComponent<Plant>();
            if (plante != null) plante.EssayerArroser();
        }
    }

    // Petit HUD sans Canvas : montre ce qu'on tient en main + un viseur
    void OnGUI()
    {
        string texte = GameState.itemEnMain == ""
            ? "Mains vides"
            : "En main : " + GameState.itemEnMain + "  (R = reposer)";
        GUI.Label(new Rect(10, 10, 400, 25), texte);
        GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 10, 20, 20), "+");
    }
}
