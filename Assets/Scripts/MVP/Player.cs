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
//   - clic gauche : interagir avec ce qui est en face
//       → arrosoir : on le prend
//       → plante   : on essaie de l'arroser
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

    void Start()
    {
        controleur = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>();
        // Verrouille le curseur au centre de l'écran (FPS classique)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Deplacement();
        Rotation();
        if (Input.GetMouseButtonDown(0)) Interagir();
    }

    void Deplacement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 direction = transform.right * h + transform.forward * v;
        direction.y = -9.81f * Time.deltaTime; // gravité simple
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
        // Lance un rayon invisible depuis le centre de l'écran vers l'avant
        Vector3 centreEcran = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
        Ray rayon = cam.ScreenPointToRay(centreEcran);

        if (Physics.Raycast(rayon, out RaycastHit hit, porteeInteraction))
        {
            // Si l'objet touché est l'arrosoir, on le prend.
            // GetComponentInParent regarde aussi les parents : pratique car
            // l'arrosoir est un Empty avec des Cubes/Cylindres enfants.
            Arrosoir arrosoir = hit.collider.GetComponentInParent<Arrosoir>();
            if (arrosoir != null) { arrosoir.Prendre(); return; }

            // Sinon si c'est une plante, on essaie de l'arroser.
            Plant plante = hit.collider.GetComponent<Plant>();
            if (plante != null) plante.EssayerArroser();
        }
    }

    // Affichage simple à l'écran (sans Canvas, pratique pour un MVP)
    void OnGUI()
    {
        string texte = GameState.arrosoirEnMain ? "Arrosoir en main" : "Mains vides";
        GUI.Label(new Rect(10, 10, 400, 25), texte);
        // Petit "+" au centre de l'écran = viseur
        GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 10, 20, 20), "+");
    }
}
