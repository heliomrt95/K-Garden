// Pickup.cs
// -----------------------------------------------------------------------------
// À mettre sur un GameObject ramassable (arrosoir, spray...).
// Quand le joueur clique dessus :
//   - l'objet est attaché à la caméra (visible "dans la main")
//   - il n'apparaît plus à sa position d'origine (table vide)
//   - GameState mémorise ce qu'on tient
//
// Si la main est déjà pleine → message d'erreur, pas de pickup.
// L'objet retourne à sa place quand on s'en sert (ex: arroser un pot).
//
// Composant requis : un Collider quelque part dans la hiérarchie (les Cubes /
// Cylindres en ont un par défaut).
// -----------------------------------------------------------------------------

using UnityEngine;

public class Pickup : MonoBehaviour
{
    // Le type d'item donné quand on ramasse cet objet (à régler dans l'Inspector).
    // Exemples : "eau" pour un arrosoir, "spray" pour un spray.
    public string typeItem = "eau";

    // Position / rotation locales de l'objet quand il est dans la main de la caméra.
    // Ajustables dans l'Inspector si l'objet apparaît mal orienté.
    public Vector3 positionEnMain = new Vector3(0.3f, -0.25f, 0.5f);
    public Vector3 rotationEnMain = Vector3.zero;

    public void Prendre()
    {
        // Main déjà pleine ?
        if (GameState.itemEnMain != "")
        {
            Debug.Log("Tu as déjà " + GameState.itemEnMain + " en main.");
            return;
        }

        // 1) Mémorise la position d'origine pour pouvoir y retourner plus tard
        GameState.parentOriginal = transform.parent;
        GameState.posOriginale = transform.localPosition;
        GameState.rotOriginale = transform.localRotation;

        // 2) Désactive les colliders pour ne pas gêner le raycast du joueur
        GameState.collidersOriginaux = GetComponentsInChildren<Collider>();
        foreach (var c in GameState.collidersOriginaux) c.enabled = false;

        // 3) Attache à la caméra
        Camera cam = Camera.main;
        if (cam != null)
        {
            transform.SetParent(cam.transform);
            transform.localPosition = positionEnMain;
            transform.localRotation = Quaternion.Euler(rotationEnMain);
        }

        // 4) Met à jour l'état global
        GameState.itemEnMain = typeItem;
        GameState.objetEnMain = gameObject;
        GameState.consommerAuRelache = false; // outil : retourne à sa place après usage

        Debug.Log("Tu as pris : " + typeItem);

        // Son de ramassage selon le type d'objet
        if (AudioManager.Instance != null)
        {
            AudioClip clip = (typeItem == "graine" || typeItem.StartsWith("graine_"))
                ? AudioManager.Instance.pickupSeed
                : AudioManager.Instance.pickupTool;
            AudioManager.Instance.Play(clip);
        }
    }
}
