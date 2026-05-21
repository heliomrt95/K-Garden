// Nuisible.cs
// -----------------------------------------------------------------------------
// Petite bête volante qui tourne en orbite autour d'un Pot et inflige des
// dégâts à la plante du Pot. Tuée par le spray (via Pot.TuerNuisible).
//
// Géré par Pot.cs — ne pas instancier à la main, c'est le Pot qui spawn ses
// propres nuisibles.
// -----------------------------------------------------------------------------

using UnityEngine;

public class Nuisible : MonoBehaviour
{
    public Pot potCible;                  // pot autour duquel on orbite
    public float rayon = 0.25f;           // distance horizontale au centre du pot
    public float vitesseAngulaire = 90f;  // degrés par seconde
    public float amplitudeY = 0.05f;      // bobbing vertical
    public float vitesseY = 3f;           // fréquence du bobbing
    public float degatsParSeconde = 5f;   // PV retirés à la plante par seconde

    private float angle;     // angle courant (degrés)
    private float t;         // temps écoulé (pour le bobbing)
    private Vector3 centre;  // position du centre d'orbite, calculée au Start

    void Start()
    {
        if (potCible == null) { Destroy(gameObject); return; }

        // Petit décalage initial pour ne pas que tous les nuisibles d'un même
        // pot orbitent en phase parfaite.
        angle = Random.Range(0f, 360f);
        t = Random.Range(0f, 100f);

        // Centre d'orbite : juste au-dessus du pot (à hauteur de la plante)
        centre = potCible.transform.position + Vector3.up * 0.35f;
    }

    void Update()
    {
        if (potCible == null) { Destroy(gameObject); return; }

        // Recalcule le centre au cas où le pot a bougé
        centre = potCible.transform.position + Vector3.up * 0.35f;

        // Orbite horizontale
        angle += vitesseAngulaire * Time.deltaTime;
        if (angle > 360f) angle -= 360f;

        // Bobbing vertical
        t += Time.deltaTime;
        float yOff = Mathf.Sin(t * vitesseY) * amplitudeY;

        // Position en orbite
        float rad = angle * Mathf.Deg2Rad;
        transform.position = centre + new Vector3(
            Mathf.Cos(rad) * rayon,
            yOff,
            Mathf.Sin(rad) * rayon);

        // Orientation : face à la direction du mouvement
        Vector3 tangente = new Vector3(-Mathf.Sin(rad), 0, Mathf.Cos(rad));
        transform.rotation = Quaternion.LookRotation(tangente);

        // Inflige des dégâts à la plante
        potCible.RecevoirDegats(degatsParSeconde * Time.deltaTime);
    }
}
