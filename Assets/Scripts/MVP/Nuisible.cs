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
    public float rayon = 0.22f;           // distance horizontale au centre du pot
    public float vitesseAngulaire = 120f; // degrés par seconde (insectes nerveux)
    public float amplitudeY = 0.06f;      // bobbing vertical
    public float vitesseY = 3.5f;         // fréquence du bobbing
    public float hauteurCentre = 0.40f;   // hauteur du centre d'orbite au-dessus du pot
    public float degatsParSeconde = 0.8f; // PV retirés à la plante par seconde
                                          // Avec 3 nuisibles max : pic à 2.4 PV/s
                                          // → plante meurt en ~40 s sans intervention,
                                          //   maturité à 30 s donc gagnable même passif.

    private float angle;     // angle courant (degrés)
    private float t;         // temps écoulé (pour le bobbing)
    private Vector3 centre;  // position du centre d'orbite, calculée au Start

    void Start()
    {
        if (potCible == null) { Destroy(gameObject); return; }

        // Décalage initial pour casser la synchro entre nuisibles du même groupe
        angle = Random.Range(0f, 360f);
        t = Random.Range(0f, 100f);

        centre = potCible.transform.position + Vector3.up * hauteurCentre;

        // Son d'ambiance 3D : bourdonnement loopé attaché au nuisible
        if (AudioManager.Instance != null && AudioManager.Instance.insectFly != null)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.clip = AudioManager.Instance.insectFly;
            src.loop = true;
            src.spatialBlend = 1f;           // 3D
            src.minDistance = 0.5f;
            src.maxDistance = 6f;
            src.volume = AudioManager.Volume2D(0.6f);
            src.Play();
        }
    }

    void Update()
    {
        if (potCible == null) { Destroy(gameObject); return; }

        // Recalcule le centre au cas où le pot a bougé
        centre = potCible.transform.position + Vector3.up * hauteurCentre;

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
