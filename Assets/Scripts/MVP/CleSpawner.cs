// CleSpawner.cs
// -----------------------------------------------------------------------------
// Crée à l'exécution une clé dorée (3 primitives : manche + anneau + dent),
// avec rotation/flotaison + Pickup + CleVictoire. Utilisé quand la plante
// carnivore atteint sa maturité (cf. PlantReward.DonnerRecompenses(4)).
// -----------------------------------------------------------------------------

using UnityEngine;

public static class CleSpawner
{
    public static GameObject SpawnCle(Vector3 position)
    {
        GameObject cle = new GameObject("CleVictoire");
        cle.transform.position = position;

        // Matériau doré commun
        Material matDore = new Material(Shader.Find("Standard"));
        matDore.color = new Color(0.95f, 0.80f, 0.20f);
        matDore.SetFloat("_Metallic", 0.9f);
        matDore.SetFloat("_Glossiness", 0.8f);
        matDore.SetColor("_EmissionColor", new Color(0.4f, 0.3f, 0.05f));
        matDore.EnableKeyword("_EMISSION");

        // Manche vertical
        GameObject manche = GameObject.CreatePrimitive(PrimitiveType.Cube);
        manche.name = "Manche";
        manche.transform.SetParent(cle.transform, false);
        manche.transform.localScale = new Vector3(0.04f, 0.22f, 0.04f);
        Object.Destroy(manche.GetComponent<Collider>());
        manche.GetComponent<Renderer>().sharedMaterial = matDore;

        // Anneau en haut (sphère écrasée)
        GameObject anneau = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        anneau.name = "Anneau";
        anneau.transform.SetParent(cle.transform, false);
        anneau.transform.localPosition = new Vector3(0, 0.14f, 0);
        anneau.transform.localScale = new Vector3(0.10f, 0.10f, 0.04f);
        Object.Destroy(anneau.GetComponent<Collider>());
        anneau.GetComponent<Renderer>().sharedMaterial = matDore;

        // Dent latérale en bas
        GameObject dent = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dent.name = "Dent";
        dent.transform.SetParent(cle.transform, false);
        dent.transform.localPosition = new Vector3(0.04f, -0.08f, 0);
        dent.transform.localScale = new Vector3(0.05f, 0.04f, 0.04f);
        Object.Destroy(dent.GetComponent<Collider>());
        dent.GetComponent<Renderer>().sharedMaterial = matDore;

        // Animation rotation + flotaison
        cle.AddComponent<CleAnimation>();

        // Pickup (typeItem = "cle")
        Pickup pickup = cle.AddComponent<Pickup>();
        pickup.typeItem = "cle";
        pickup.positionEnMain = new Vector3(0.3f, -0.2f, 0.5f);
        pickup.rotationEnMain = new Vector3(0f, 0f, 45f);

        // CleVictoire : déclenche l'ouverture de la porte au ramassage
        cle.AddComponent<CleVictoire>();

        // Collider global pour le clic
        BoxCollider bc = cle.AddComponent<BoxCollider>();
        bc.center = new Vector3(0.02f, 0.03f, 0f);
        bc.size = new Vector3(0.16f, 0.30f, 0.06f);

        return cle;
    }
}

// Petite animation autonome (rotation + flotaison) pour attirer l'œil sur la clé
public class CleAnimation : MonoBehaviour
{
    public float vitesseRotation = 90f;   // °/s
    public float amplitude = 0.06f;       // m
    public float vitesseY = 2f;           // Hz

    private float t;
    private Vector3 posInit;

    void Start() { posInit = transform.position; }

    void Update()
    {
        transform.Rotate(0, vitesseRotation * Time.deltaTime, 0);
        t += Time.deltaTime;
        transform.position = posInit + Vector3.up * Mathf.Sin(t * vitesseY) * amplitude;
    }
}
