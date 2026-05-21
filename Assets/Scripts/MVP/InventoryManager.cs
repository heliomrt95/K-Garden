// InventoryManager.cs
// -----------------------------------------------------------------------------
// Inventaire global du joueur : stocke combien de chaque ressource il a.
// Une SEULE instance dans la scène (singleton simple via InventoryManager.Instance).
// À mettre sur un GameObject vide "GameManager".
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    // Singleton : on y accède via InventoryManager.Instance depuis n'importe où
    public static InventoryManager Instance;

    // La "boîte" qui retient les quantités. Clé = nom (ex: "pollen"), valeur = nombre.
    private Dictionary<string, int> ressources = new Dictionary<string, int>();

    void Awake()
    {
        // S'il y en a déjà une, on supprime celle-ci (évite les doublons)
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Ajoute une ressource à l'inventaire
    public void AddItem(string nom, int quantite)
    {
        if (!ressources.ContainsKey(nom)) ressources[nom] = 0;
        ressources[nom] += quantite;
        Debug.Log("[Inventaire] +" + quantite + " " + nom + " (total " + ressources[nom] + ")");
    }

    // Retire une ressource (sans descendre en dessous de 0)
    public void RemoveItem(string nom, int quantite)
    {
        if (!ressources.ContainsKey(nom)) return;
        ressources[nom] = Mathf.Max(0, ressources[nom] - quantite);
    }

    // Vrai si le joueur a au moins "quantite" de cette ressource
    public bool HasEnough(string nom, int quantite)
    {
        return ressources.ContainsKey(nom) && ressources[nom] >= quantite;
    }

    // Combien on a de cette ressource (0 si jamais ramassée)
    public int GetQuantite(string nom)
    {
        return ressources.ContainsKey(nom) ? ressources[nom] : 0;
    }

    // ── Affichage simple en haut à droite (debug visuel de l'inventaire) ─────
    void OnGUI()
    {
        float y = 10f;
        GUI.Label(new Rect(Screen.width - 220, y, 210, 25), "── Inventaire ──");
        y += 22f;
        foreach (var kv in ressources)
        {
            GUI.Label(new Rect(Screen.width - 220, y, 210, 20), kv.Key + " : " + kv.Value);
            y += 18f;
        }
    }
}
