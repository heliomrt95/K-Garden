// SerreBuilder.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Créer Serre" : construit dans la scène courante une serre
// arrondie type "tunnel" agricole — voûte en demi-ellipse, bâche opaque crème,
// armature en acier, pignons fermés aux 2 extrémités.
//
// Dimensions : 8m large × 12m long × 3.5m haut.
//
// Tous les morceaux sont des primitives Unity (cubes) regroupés sous un seul
// GameObject parent "Serre" — on peut donc la déplacer/agrandir d'un bloc.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using System.IO;

public static class SerreBuilder
{
    // Réglages de la serre (modifiables ici si besoin)
    const float largeur  = 8f;    // X total
    const float longueur = 12f;   // Z total
    const float hauteur  = 3.5f;  // Y (hauteur de la voûte)
    const int   nbArches = 8;     // nombre d'arches transversales
    const int   nbSegments = 14;  // facettes par arche (plus = plus lisse)

    [MenuItem("Tools/Créer Serre")]
    public static void CreerSerre()
    {
        // 1) Matériaux
        Material beton = ChargerOuCreerMateriau("Concrete",
            new Color(0.78f, 0.78f, 0.75f), metallique: 0f, brillance: 0.15f);
        Material acier = ChargerOuCreerMateriau("Steel",
            new Color(0.78f, 0.80f, 0.82f), metallique: 0.85f, brillance: 0.65f);
        Material bache = ChargerOuCreerMateriau("Tarp",
            new Color(0.94f, 0.96f, 0.91f), metallique: 0f, brillance: 0.08f);

        float Rx = largeur / 2f;   // demi-largeur (rayon horizontal de l'ellipse)
        float Ry = hauteur;        // hauteur de la voûte (rayon vertical)
        float L  = longueur;
        float archSpacing = L / (nbArches - 1);

        // 2) Parent vide
        GameObject serre = new GameObject("Serre");
        serre.transform.position = Vector3.zero;

        // 3) Sol béton (dalle légèrement plus grande que la serre)
        CreerCube("Sol_Beton",
            pos:   new Vector3(0f, 0.05f, 0f),
            scale: new Vector3(largeur + 0.2f, 0.1f, longueur + 0.2f),
            rot:   Quaternion.identity,
            mat:   beton, parent: serre.transform);

        // 4) Arches transversales (armature courbée)
        for (int a = 0; a < nbArches; a++)
        {
            float z = -L / 2f + a * archSpacing;
            CreerArche(serre.transform, Rx, Ry, z, nbSegments, 0.10f, acier, "Arche_" + a);
        }

        // 5) Longerons (poutres longitudinales le long de l'arche)
        // 6 longerons + 1 = 7, répartis autour de la demi-ellipse
        const int nbLongerons = 6;
        for (int l = 0; l <= nbLongerons; l++)
        {
            float theta = Mathf.PI * l / nbLongerons; // 0..π
            float x = -Mathf.Cos(theta) * Rx;
            float y = Mathf.Sin(theta) * Ry;
            CreerCube("Longeron_" + l,
                pos:   new Vector3(x, y, 0f),
                scale: new Vector3(0.07f, 0.07f, L),
                rot:   Quaternion.identity,
                mat:   acier, parent: serre.transform);
        }

        // 6) Panneaux de bâche entre les arches (forme la voûte)
        for (int a = 0; a < nbArches - 1; a++)
        {
            float zMid = -L / 2f + a * archSpacing + archSpacing / 2f;
            CreerRangeePanneaux(serre.transform, Rx, Ry, zMid, archSpacing, nbSegments, bache, "BachePanel_" + a);
        }

        // 7) Pignons : fermetures avant et arrière (mur en arc plein)
        CreerPignon(serre.transform, Rx, Ry, +L / 2f + 0.02f, bache, "Pignon_Avant");
        CreerPignon(serre.transform, Rx, Ry, -L / 2f - 0.02f, bache, "Pignon_Arriere");

        Selection.activeGameObject = serre;
        Debug.Log("✅ Serre arrondie créée : " + largeur + "m × " + longueur + "m × " + hauteur + "m.");
    }

    // ── Une arche = N petits cubes posés le long d'une demi-ellipse ───────────
    static void CreerArche(Transform parent, float Rx, float Ry, float z,
                           int segments, float thickness, Material mat, string nom)
    {
        GameObject arche = new GameObject(nom);
        arche.transform.parent = parent;
        arche.transform.localPosition = new Vector3(0, 0, z);

        for (int i = 0; i < segments; i++)
        {
            // Deux angles successifs sur la demi-ellipse (0 = base gauche, π = base droite)
            float t0 = Mathf.PI * i       / segments;
            float t1 = Mathf.PI * (i + 1) / segments;
            Vector3 p0 = new Vector3(-Mathf.Cos(t0) * Rx, Mathf.Sin(t0) * Ry, 0);
            Vector3 p1 = new Vector3(-Mathf.Cos(t1) * Rx, Mathf.Sin(t1) * Ry, 0);

            Vector3 mid = (p0 + p1) * 0.5f;
            Vector3 dir = p1 - p0;
            float len = dir.magnitude;
            float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = "Segment_" + i;
            seg.transform.parent = arche.transform;
            seg.transform.localPosition = mid;
            seg.transform.localRotation = Quaternion.Euler(0, 0, angleDeg);
            seg.transform.localScale = new Vector3(len, thickness, thickness);
            seg.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    // ── Une rangée de panneaux de bâche entre 2 arches ────────────────────────
    static void CreerRangeePanneaux(Transform parent, float Rx, float Ry, float zMid,
                                    float length, int segments, Material mat, string nom)
    {
        GameObject row = new GameObject(nom);
        row.transform.parent = parent;

        for (int i = 0; i < segments; i++)
        {
            float t0 = Mathf.PI * i       / segments;
            float t1 = Mathf.PI * (i + 1) / segments;
            Vector3 p0 = new Vector3(-Mathf.Cos(t0) * Rx, Mathf.Sin(t0) * Ry, zMid);
            Vector3 p1 = new Vector3(-Mathf.Cos(t1) * Rx, Mathf.Sin(t1) * Ry, zMid);

            Vector3 mid = (p0 + p1) * 0.5f;
            Vector3 dir = p1 - p0;
            float len = dir.magnitude;
            float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel_" + i;
            panel.transform.parent = row.transform;
            panel.transform.localPosition = mid;
            panel.transform.localRotation = Quaternion.Euler(0, 0, angleDeg);
            // Légèrement plus long que l'écart entre arches pour éviter les trous
            panel.transform.localScale = new Vector3(len * 1.02f, 0.03f, length * 1.01f);
            panel.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    // ── Un pignon : mur en arc à chaque extrémité ─────────────────────────────
    static void CreerPignon(Transform parent, float Rx, float Ry, float z,
                            Material mat, string nom)
    {
        GameObject pignon = new GameObject(nom);
        pignon.transform.parent = parent;

        const int slices = 16;
        float sliceWidth = (2f * Rx) / slices;

        for (int i = 0; i < slices; i++)
        {
            // x centre de la tranche, allant de gauche à droite
            float x = -Rx + (i + 0.5f) * sliceWidth;
            // Hauteur de la voûte à cette position x (équation de demi-ellipse)
            float ratio = Mathf.Clamp(-x / Rx, -1f, 1f);
            float t = Mathf.Acos(ratio);
            float h = Mathf.Sin(t) * Ry;
            if (h < 0.01f) continue; // saute les tranches quasi-nulles aux bords

            GameObject slice = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slice.name = "Slice_" + i;
            slice.transform.parent = pignon.transform;
            slice.transform.localPosition = new Vector3(x, h * 0.5f, z);
            slice.transform.localScale = new Vector3(sliceWidth * 1.02f, h, 0.05f);
            slice.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    // ── Helper générique pour créer un Cube enfant ────────────────────────────
    static void CreerCube(string nom, Vector3 pos, Vector3 scale, Quaternion rot,
                          Material mat, Transform parent)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = nom;
        piece.transform.parent = parent;
        piece.transform.localPosition = pos;
        piece.transform.localRotation = rot;
        piece.transform.localScale = scale;
        piece.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // ── Charge un matériau existant ou en crée un nouveau ─────────────────────
    static Material ChargerOuCreerMateriau(string nom, Color couleur,
                                           float metallique, float brillance)
    {
        string dossier = "Assets/Resources/Materials";
        string chemin = dossier + "/" + nom + ".mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(chemin);
        if (mat != null) return mat;

        if (!Directory.Exists(dossier)) Directory.CreateDirectory(dossier);
        mat = new Material(Shader.Find("Standard"));
        mat.color = couleur;
        mat.SetFloat("_Metallic", metallique);
        mat.SetFloat("_Glossiness", brillance);
        AssetDatabase.CreateAsset(mat, chemin);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
