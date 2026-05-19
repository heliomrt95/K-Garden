// Player.cs
// -----------------------------------------------------------------------------
// Contrôle du joueur en vue 1ère personne (FPS).
//
// À mettre sur : un GameObject vide "Player" qui contient la Main Camera en enfant.
// Composants Unity requis sur ce GameObject : CharacterController (collisions).
//
// Commandes :
//   - W/A/S/D ou flèches : se déplacer
//   - souris : regarder autour
//   - clic gauche : interagir avec ce qui est en face (spray ou plante)
// -----------------------------------------------------------------------------

using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    // --- Réglages visibles dans l'Inspector ---
    public float vitesse = 4f;             // vitesse de déplacement (m/s)
    public float sensibiliteSouris = 2f;   // sensibilité de la rotation à la souris
    public float porteeInteraction = 3f;   // distance max pour cliquer sur un objet (m)

    // --- Variables internes ---
    private CharacterController controleur;
    private Camera cam;
    private float rotationVerticale = 0f;  // angle haut/bas de la caméra

    // Start() est appelée une fois au lancement de la scène
    void Start()
    {
        controleur = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>(); // récupère la caméra enfant
        // Verrouille le curseur au centre de l'écran (comme dans n'importe quel FPS)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Update() est appelée à chaque image (60 fois par seconde environ)
    void Update()
    {
        Deplacement();
        Rotation();
        // Si on appuie sur le clic gauche, on tente une interaction
        if (Input.GetMouseButtonDown(0)) Interagir();
    }

    void Deplacement()
    {
        // Input.GetAxis renvoie un nombre entre -1 et +1 selon les touches appuyées
        float h = Input.GetAxis("Horizontal"); // A/D ou flèches gauche/droite
        float v = Input.GetAxis("Vertical");   // W/S ou flèches haut/bas

        // Calcule la direction dans le repère du joueur (avant + côté)
        Vector3 direction = transform.right * h + transform.forward * v;
        // Petite gravité pour rester collé au sol
        direction.y = -9.81f * Time.deltaTime;

        controleur.Move(direction * vitesse * Time.deltaTime);
    }

    void Rotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensibiliteSouris;
        float mouseY = Input.GetAxis("Mouse Y") * sensibiliteSouris;

        // Rotation gauche/droite : tourne tout le joueur (corps + caméra)
        transform.Rotate(0, mouseX, 0);

        // Rotation haut/bas : tourne seulement la caméra, en limitant les angles
        rotationVerticale -= mouseY;
        rotationVerticale = Mathf.Clamp(rotationVerticale, -80f, 80f);
        cam.transform.localEulerAngles = new Vector3(rotationVerticale, 0, 0);
    }

    void Interagir()
    {
        // Lance un rayon invisible depuis le centre de l'écran vers l'avant
        Vector3 centreEcran = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
        Ray rayon = cam.ScreenPointToRay(centreEcran);

        // Si le rayon touche un objet à moins de "porteeInteraction" mètres
        if (Physics.Raycast(rayon, out RaycastHit hit, porteeInteraction))
        {
            // L'objet touché a-t-il un script SprayPicker ? Si oui, on l'active
            SprayPicker spray = hit.collider.GetComponent<SprayPicker>();
            if (spray != null) spray.Selectionner();

            // L'objet touché a-t-il un script Plant ? Si oui, on tente de la soigner
            Plant plante = hit.collider.GetComponent<Plant>();
            if (plante != null) plante.EssayerDeSoigner();
        }
    }

    // OnGUI() affiche du texte à l'écran sans avoir à créer un Canvas.
    // Pratique pour un MVP, à remplacer par un vrai HUD plus tard.
    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 400, 25),
                  "Spray en main : " + GameState.spraySelectionne);
        // Petit point au centre de l'écran qui sert de viseur
        GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 10, 20, 20), "+");
    }
}
