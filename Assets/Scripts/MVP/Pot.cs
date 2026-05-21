// Pot.cs
// -----------------------------------------------------------------------------
// À mettre sur un pot vide qui passe par 3 étapes successives :
//
//   Vide → Terre → Graine plantée → Arrosé
//
// Composants Unity requis :
//   - un Collider (pour être cliquable)
//   - 3 enfants optionnels (à glisser dans l'Inspector) :
//       * visuelTerre  : visuel terre (caché au start, affiché après "terre")
//       * visuelGraine : visuel graine/pousse (caché au start, après "graine")
//       * visuelEau    : visuel eau (caché au start, après "eau")
//
// Règles :
//   - on ne peut pas planter une graine sans terre
//   - on ne peut pas arroser sans graine plantée
//
// Le Player utilise DureeAction(item) et ValiderAction(item) pour gérer le
// "clic maintenu avec barre de progression". Si DureeAction renvoie 0, l'item
// n'est pas applicable au pot dans son état actuel.
// -----------------------------------------------------------------------------

using UnityEngine;

public class Pot : MonoBehaviour
{
    public GameObject visuelTerre;
    public GameObject visuelGraine;
    public GameObject visuelEau;

    // Durées (secondes) du clic maintenu pour chaque action
    public float dureeRemplirTerre = 1.2f;
    public float dureePlanterGraine = 1.5f;
    public float dureeArroser = 1.0f;

    private bool aTerre = false;
    private bool aGraine = false;
    private bool aEau = false;

    void Start()
    {
        if (visuelTerre  != null) visuelTerre.SetActive(false);
        if (visuelGraine != null) visuelGraine.SetActive(false);
        if (visuelEau    != null) visuelEau.SetActive(false);
    }

    // Renvoie la durée nécessaire pour appliquer l'item donné, ou 0 si l'action
    // n'est pas possible actuellement (ex: graine sans terre, item inconnu...).
    public float DureeAction(string item)
    {
        if (item == "terre" && !aTerre)             return dureeRemplirTerre;
        if (item == "graine" && aTerre && !aGraine) return dureePlanterGraine;
        if (item == "eau" && aGraine && !aEau)      return dureeArroser;
        return 0f;
    }

    // Message d'aide affiché quand le joueur clique sans pouvoir agir.
    public string RaisonRefus(string item)
    {
        if (item == "")        return "Mains vides. Prends terre, graine ou arrosoir.";
        if (item == "terre")   return aTerre  ? "Le pot a déjà de la terre." : "";
        if (item == "graine")  return !aTerre ? "Mets d'abord de la terre dans le pot." :
                                      aGraine ? "Une graine est déjà plantée." : "";
        if (item == "eau")     return !aTerre  ? "Mets d'abord de la terre." :
                                      !aGraine ? "Plante d'abord une graine." :
                                       aEau    ? "Le pot est déjà arrosé." : "";
        return "Cet item (" + item + ") ne va pas dans le pot.";
    }

    // Applique l'action après que la barre de progression soit pleine.
    // Consomme la ressource en main (terre/graine) ou libère l'outil (arrosoir).
    public void ValiderAction(string item)
    {
        if (item == "terre" && !aTerre)
        {
            aTerre = true;
            if (visuelTerre != null) visuelTerre.SetActive(true);
            GameState.LibererMain();
            Debug.Log("Pot rempli de terre.");
        }
        else if (item == "graine" && aTerre && !aGraine)
        {
            aGraine = true;
            if (visuelGraine != null) visuelGraine.SetActive(true);
            GameState.LibererMain();
            Debug.Log("Graine plantée.");
        }
        else if (item == "eau" && aGraine && !aEau)
        {
            aEau = true;
            if (visuelEau != null) visuelEau.SetActive(true);
            GameState.LibererMain();
            Debug.Log("Pot arrosé.");
        }
    }
}
