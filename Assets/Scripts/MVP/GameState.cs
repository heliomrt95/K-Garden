// GameState.cs
// -----------------------------------------------------------------------------
// "Mémoire partagée" du jeu — accessible par tous les autres scripts.
// On y stocke ce que le joueur tient dans la main (un seul slot à la fois).
//
// Pas de MonoBehaviour : classe statique, pas besoin de GameObject.
// -----------------------------------------------------------------------------

using UnityEngine;

public static class GameState
{
    // Nom de l'item tenu : "" = mains vides, sinon "eau", "spray", "terre", etc.
    public static string itemEnMain = "";

    // Référence vers le GameObject visuellement tenu dans la main
    // (soit l'objet original ramassé, soit un petit cube généré pour les ressources)
    public static GameObject objetEnMain = null;

    // true = le GO en main doit être détruit quand on s'en sert (ressource type terre/eau)
    // false = il doit retourner à sa place d'origine (outil type arrosoir/spray)
    public static bool consommerAuRelache = false;

    // Mémoire de la position d'origine, pour pouvoir y retourner l'objet
    public static Transform parentOriginal;
    public static Vector3 posOriginale;
    public static Quaternion rotOriginale;
    public static Collider[] collidersOriginaux;

    // Libère la main : soit on remet l'objet à sa place, soit on le détruit.
    public static void LibererMain()
    {
        if (objetEnMain == null) { Reset(); return; }

        if (consommerAuRelache)
        {
            Object.Destroy(objetEnMain);
        }
        else
        {
            // Retour à la position d'origine (table, étagère...)
            objetEnMain.transform.SetParent(parentOriginal);
            objetEnMain.transform.localPosition = posOriginale;
            objetEnMain.transform.localRotation = rotOriginale;
            if (collidersOriginaux != null)
                foreach (var c in collidersOriginaux) if (c != null) c.enabled = true;
        }
        Reset();
    }

    static void Reset()
    {
        itemEnMain = "";
        objetEnMain = null;
        parentOriginal = null;
        collidersOriginaux = null;
    }
}
