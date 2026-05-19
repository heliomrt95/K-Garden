using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Script utilitaire à placer sur un GameObject "GameRoot" dans la scène.
/// Au démarrage, il :
///   1. instancie LevelBuilder
///   2. instancie le Player avec caméra + CharacterController + InteractionManager
///   3. instancie GameManager + WaveManager
///   4. génère un NavMesh runtime (nécessite NavMeshSurface du package "AI Navigation")
///
/// Si tu préfères tout configurer à la main dans la scène Unity, ce script est optionnel.
/// </summary>
public class SceneSetup : MonoBehaviour
{
    public bool autoSpawnPlayer = true;
    public Vector3 playerSpawnPosition = new Vector3(0, 1f, -4f);

    private void Awake()
    {
        // 1. GameManager
        if (GameManager.Instance == null)
        {
            GameObject gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();
        }

        // 2. WaveManager
        if (WaveManager.Instance == null)
        {
            GameObject wm = new GameObject("WaveManager");
            wm.AddComponent<WaveManager>();
        }

        // 3. LevelBuilder (s'il n'est pas déjà dans la scène)
        if (FindObjectOfType<LevelBuilder>() == null)
        {
            GameObject lb = new GameObject("LevelBuilder");
            lb.AddComponent<LevelBuilder>();
        }

        // 4. Player
        if (autoSpawnPlayer && FindObjectOfType<PlayerController>() == null)
        {
            CreatePlayer();
        }

        // 5. HUD
        if (FindObjectOfType<HUDManager>() == null)
        {
            GameObject hud = new GameObject("HUDManager");
            hud.AddComponent<HUDManager>();
        }
    }

    private void CreatePlayer()
    {
        // Désactive la Main Camera par défaut pour éviter le double AudioListener
        GameObject defaultCam = GameObject.Find("Main Camera");
        if (defaultCam != null) defaultCam.SetActive(false);

        // Auto-corrige le spawn si la valeur sérialisée est hors de la serre actuelle
        Vector3 spawnPos = playerSpawnPosition;
        LevelBuilder lb = FindObjectOfType<LevelBuilder>();
        if (lb != null)
        {
            float halfLen = lb.greenhouseLength / 2f;
            float halfWid = lb.greenhouseWidth / 2f;
            if (Mathf.Abs(spawnPos.z) > halfLen - 0.8f || Mathf.Abs(spawnPos.x) > halfWid - 0.8f)
                spawnPos = new Vector3(0, 1f, -halfLen + 2f);
        }

        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = spawnPos;

        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0, 0.9f, 0);

        // Caméra à hauteur d'yeux
        GameObject cam = new GameObject("PlayerCamera");
        cam.tag = "MainCamera";
        cam.transform.parent = player.transform;
        cam.transform.localPosition = new Vector3(0, 1.65f, 0);
        Camera c = cam.AddComponent<Camera>();
        c.nearClipPlane = 0.05f;
        cam.AddComponent<AudioListener>();

        PlayerController pc = player.AddComponent<PlayerController>();
        pc.cameraTransform = cam.transform;

        InteractionManager im = player.AddComponent<InteractionManager>();
        im.playerCamera = c;
    }
}
