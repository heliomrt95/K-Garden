// AmbianceBuilder.cs
// -----------------------------------------------------------------------------
// Menu "Tools > Configurer Ambiance Extérieure" :
//  - crée (ou charge) un matériau Skybox procédural "Sky_Procedural.mat"
//    avec un ciel bleu doux + soleil
//  - l'applique à RenderSettings.skybox + ambient lighting = Skybox
//  - active un fog linéaire léger (25 → 60 m) couleur ciel pour fondre
//    les bords de la (petite) map
//  - règle Camera.main.clearFlags sur Skybox pour ne plus voir le gris Unity
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;

public static class AmbianceBuilder
{
    const string CHEMIN_MAT_SKY = "Assets/Resources/Materials/Sky_Procedural.mat";

    [MenuItem("Tools/Configurer Ambiance Extérieure")]
    public static void Configurer()
    {
        // 1) Crée ou récupère le matériau Skybox procédural
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(CHEMIN_MAT_SKY);
        if (sky == null)
        {
            string dossier = Path.GetDirectoryName(CHEMIN_MAT_SKY);
            if (!Directory.Exists(dossier)) Directory.CreateDirectory(dossier);

            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Erreur",
                    "Shader 'Skybox/Procedural' introuvable.", "OK");
                return;
            }
            sky = new Material(shader);
            AssetDatabase.CreateAsset(sky, CHEMIN_MAT_SKY);
        }

        // Paramètres : ciel bleu naturel, soleil visible, atmosphère douce
        sky.SetFloat("_SunDisk", 2f);                                // soleil simple, visible
        sky.SetFloat("_SunSize", 0.04f);
        sky.SetFloat("_SunSizeConvergence", 5f);
        sky.SetFloat("_AtmosphereThickness", 1.0f);
        sky.SetColor("_SkyTint",     new Color(0.5f, 0.65f, 0.85f)); // teinte bleu ciel
        sky.SetColor("_GroundColor", new Color(0.45f, 0.5f, 0.4f));  // gris-verdâtre
        sky.SetFloat("_Exposure",    1.3f);
        EditorUtility.SetDirty(sky);

        // 2) Skybox + ambient lighting
        RenderSettings.skybox          = sky;
        RenderSettings.ambientMode     = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.0f;
        RenderSettings.reflectionIntensity = 1.0f;

        // Une directional light "sun" si présente → liée à la skybox
        Light[] dirs = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        Light sun = null;
        foreach (var l in dirs) if (l.type == LightType.Directional) { sun = l; break; }
        if (sun != null) RenderSettings.sun = sun;

        // 3) Fog linéaire — fondu doux des bords de la map
        Color fogCol = new Color(0.72f, 0.82f, 0.9f); // bleu pâle (couleur horizon)
        RenderSettings.fog              = true;
        RenderSettings.fogMode          = FogMode.Linear;
        RenderSettings.fogStartDistance = 25f;
        RenderSettings.fogEndDistance   = 60f;
        RenderSettings.fogColor         = fogCol;

        // 4) Caméra : background = Skybox (plus de gris uni)
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = fogCol; // fallback si skybox ratée
            EditorUtility.SetDirty(cam.gameObject);
        }

        // 5) Recalcule le GI dynamique pour utiliser le skybox comme ambient
        DynamicGI.UpdateEnvironment();

        // 6) Marque la scène et sauvegarde les assets
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();

        Debug.Log("✅ Ambiance configurée : Skybox bleu (Sky_Procedural.mat) + " +
                  "Fog Linéaire 25→60 m + Camera clearFlags=Skybox" +
                  (cam == null ? " — ⚠ aucune caméra trouvée." : ""));

        EditorUtility.DisplayDialog("Ambiance Extérieure",
            "Skybox : Sky_Procedural.mat (ciel bleu)\n" +
            "Ambient : Skybox\n" +
            "Fog : Linéaire 25 → 60 m\n" +
            "Caméra : Clear Flags = Skybox\n\n" +
            "Pense à sauvegarder la scène (Cmd+S).", "OK");
    }
}
