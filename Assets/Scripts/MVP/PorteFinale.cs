// PorteFinale.cs
// -----------------------------------------------------------------------------
// À attacher au GameObject "Porte" (celui caché derrière les buissons).
// Appelé par CleVictoire.OnRamasse() quand le joueur prend la clé.
//
// Deux modes :
//   - Disparition simple : la porte fait SetActive(false) après un délai
//   - Rotation : la porte tourne sur Y de 90° (effet "s'ouvre"). Attention,
//     le pivot du modèle doit être sur un côté de la porte, sinon ça pivote
//     bizarrement. Si ça ne rend pas bien, garde le mode Disparition.
// -----------------------------------------------------------------------------

using System.Collections;
using UnityEngine;

public class PorteFinale : MonoBehaviour
{
    public enum Mode { Disparition, Rotation }

    [Header("Animation d'ouverture")]
    public Mode mode = Mode.Disparition;
    public float delaiAvantAction = 1.0f;     // s : délai d'attente avant de bouger
    public float dureeAnimation = 1.5f;       // s : durée de la rotation
    public float angleOuverture = 95f;        // ° (mode Rotation seulement)
    public Vector3 axeRotation = Vector3.up;  // axe (mode Rotation seulement)

    [Header("La serre bloque aussi le joueur — on la désactive en même temps")]
    [Tooltip("À glisser : le GameObject 'Serre'. Si vide, cherché auto par nom.")]
    public GameObject serreADesactiver;

    private bool ouvert = false;

    public void Ouvrir()
    {
        if (ouvert) return;
        ouvert = true;
        SimpleUIMessage.Afficher("La porte s'ouvre… Tu peux sortir.");
        StartCoroutine(Animer());
    }

    IEnumerator Animer()
    {
        yield return new WaitForSeconds(delaiAvantAction);

        // 1) Anime / fait disparaître la porte
        if (mode == Mode.Rotation)
        {
            Quaternion rotInit = transform.rotation;
            Quaternion rotFin = rotInit * Quaternion.AngleAxis(angleOuverture, axeRotation);
            float t = 0f;
            while (t < dureeAnimation)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dureeAnimation);
                transform.rotation = Quaternion.Slerp(rotInit, rotFin, k);
                yield return null;
            }
            transform.rotation = rotFin;
        }
        else
        {
            gameObject.SetActive(false);
        }

        // 2) Désactive la SERRE pour que le joueur puisse sortir
        //    (les panneaux/pignons/arches sont enfants de "Serre")
        GameObject serre = serreADesactiver ?? GameObject.Find("Serre");
        if (serre != null)
        {
            serre.SetActive(false);
            Debug.Log("[PorteFinale] Serre désactivée — le joueur peut sortir.");
        }
        else
        {
            Debug.LogWarning("[PorteFinale] Aucun GameObject 'Serre' trouvé à désactiver.");
        }
    }
}
