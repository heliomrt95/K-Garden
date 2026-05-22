// Source.cs
// -----------------------------------------------------------------------------
// À mettre sur un GameObject "source de ressource" (ex: tas de terre).
// La source ne se déplace pas et n'est pas consommée — elle DONNE une ressource
// (terre, eau, graine...) au joueur sous forme d'un petit cube coloré dans la main.
//
// Quand le joueur clique dessus :
//   - un petit cube coloré est créé et attaché à la caméra (visible dans la main)
//   - GameState mémorise ce qu'on tient
//   - la source reste à sa place
//
// Le cube en main est détruit quand on s'en sert (ex: dans un pot).
//
// Composant requis : un Collider sur le GameObject (Box, Sphere...).
// -----------------------------------------------------------------------------

using UnityEngine;

public class Source : MonoBehaviour
{
    // Type d'item donné (ex: "terre", "eau", "graine")
    public string typeItem = "terre";

    // Couleur du petit cube généré dans la main
    public Color couleurEnMain = new Color(0.45f, 0.30f, 0.18f);

    // Taille du petit cube généré
    public float tailleEnMain = 0.12f;

    public void Prendre()
    {
        if (GameState.itemEnMain != "")
        {
            Debug.Log("Tu as déjà " + GameState.itemEnMain + " en main.");
            return;
        }

        // 1) Crée un petit cube visuel pour la main
        GameObject visuel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visuel.name = "MainContent_" + typeItem;
        visuel.transform.localScale = Vector3.one * tailleEnMain;

        // Couleur (instancie un matériau Standard avec la couleur voulue)
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = couleurEnMain;
        visuel.GetComponent<Renderer>().sharedMaterial = mat;

        // Pas de collider sinon ça bloque le raycast du joueur
        Object.Destroy(visuel.GetComponent<Collider>());

        // 2) Attache à la caméra (position bas-droite)
        Camera cam = Camera.main;
        if (cam != null)
        {
            visuel.transform.SetParent(cam.transform);
            visuel.transform.localPosition = new Vector3(0.3f, -0.25f, 0.5f);
            visuel.transform.localRotation = Quaternion.identity;
        }

        // 3) État global
        GameState.itemEnMain = typeItem;
        GameState.objetEnMain = visuel;
        GameState.consommerAuRelache = true; // ressource : détruite après usage

        Debug.Log("Tu as pris : " + typeItem);

        // Son selon la ressource ramassée
        if (AudioManager.Instance != null)
        {
            AudioClip clip;
            if (typeItem == "terre")
                clip = AudioManager.Instance.pickupDirt;
            else if (typeItem == "graine" || typeItem.StartsWith("graine_"))
                clip = AudioManager.Instance.pickupSeed;
            else
                clip = AudioManager.Instance.pickupDirt; // défaut
            AudioManager.Instance.Play(clip);
        }
    }
}
