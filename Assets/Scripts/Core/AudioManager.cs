// AudioManager.cs
// -----------------------------------------------------------------------------
// Singleton audio centralisé pour K-Garden. Trois sources :
//   - musicSource  : musique de fond en loop (OST)
//   - sfxSource    : sons d'UI / interactions 2D (PlayOneShot)
//   - ambientSource: réservé aux sons d'ambiance loopés (non utilisé par défaut)
//
// Les AudioClip sont assignés une seule fois dans l'Inspector (ou via le menu
// "Tools > Configurer Audio" qui les pose automatiquement depuis Assets/Audio/).
//
// Usage côté gameplay :
//   AudioManager.Instance?.Play(AudioManager.Instance.menuClick1);
//   AudioManager.Instance?.PlayAt(AudioManager.Instance.wateringCan, transform.position);
// -----------------------------------------------------------------------------

using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Musique")]
    public AudioClip musicOST;
    [Range(0f, 1f)] public float musicVolume = 0.35f;

    [Header("Joueur")]
    public AudioClip footstep;

    [Header("Outils & ressources (pickup)")]
    public AudioClip pickupTool;   // prendre arrosoir / spray
    public AudioClip pickupSeed;   // prendre la graine
    public AudioClip pickupDirt;   // prendre de la terre

    [Header("Pot — actions")]
    public AudioClip plantSeed;    // planter la graine
    public AudioClip wateringCan;  // arroser
    public AudioClip spray;        // utiliser le spray

    [Header("Plante")]
    public AudioClip plantGrow;    // début de croissance (plante normale)
    public AudioClip carnivoreGrow;// début de croissance (carnivore)
    public AudioClip carnivoreRoar;// maturité carnivore

    [Header("UI")]
    public AudioClip menuClick1;   // bouton principal
    public AudioClip menuClick2;   // bouton secondaire

    [Header("Divers")]
    public AudioClip keySpawn;     // apparition récompense
    public AudioClip insectFly;    // ambiance nuisible (3D, loop)

    AudioSource musicSource;
    AudioSource sfxSource;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
    }

    void Start()
    {
        if (musicOST != null)
        {
            musicSource.clip = musicOST;
            musicSource.Play();
        }
    }

    // ── API simple ───────────────────────────────────────────────────────────
    // Joue un SFX 2D (UI, interactions du joueur visibles à l'écran).
    public void Play(AudioClip clip, float volumeMul = 1f)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, Volume2D(volumeMul));
    }

    // Joue un SFX 3D en un point du monde (utile pour ce qui n'est pas à
    // proximité immédiate du joueur). Volume utilise le slider Effets.
    public void PlayAt(AudioClip clip, Vector3 position, float volumeMul = 1f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, Volume2D(volumeMul));
    }

    // Volume global réutilisable par les autres scripts (slider Effets).
    public static float Volume2D(float mul = 1f)
    {
        return Mathf.Clamp01(SettingsController.volumeEffets * mul);
    }

    public void StopMusic() { if (musicSource != null) musicSource.Stop(); }
    public void PlayMusic() { if (musicSource != null && musicSource.clip != null) musicSource.Play(); }
}
