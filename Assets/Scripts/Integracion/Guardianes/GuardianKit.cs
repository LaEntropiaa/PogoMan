using UnityEngine;

// Recursos compartidos de los guardianes (materiales, sonidos y música) y su música de combate:
// al aparecer un guardián suena su tema durante todo el combate y al vencerlo vuelve la música normal.
public class GuardianKit : MonoBehaviour
{
    public static GuardianKit Instance { get; private set; }

    [Header("Materiales")]
    [SerializeField] private Material telegraphMaterial;
    [SerializeField] private Material fxMaterial;
    [SerializeField] private Material soulMaterial;

    [Header("Sonidos")]
    [SerializeField] private AudioClip emergeClip;
    [SerializeField] private AudioClip defeatClip;
    [SerializeField] private AudioClip windupClip;
    [Tooltip("Golpes: Fundidor (martillazo, purga), Viuda (péndulo, doblar), Recaudador (embargo, timbrado), Barquero (remada, cortejo).")]
    [SerializeField] private AudioClip[] attackClips = new AudioClip[8];
    [SerializeField] private AudioClip soulHitClip;

    [Header("Música de combate")]
    [SerializeField] private AudioClip musicFundidor;
    [SerializeField] private AudioClip musicViuda;
    [SerializeField] private AudioClip musicRecaudador;
    [SerializeField] private AudioClip musicBarquero;

    [Header("Guardianes por pilar")]
    [SerializeField] private Enemy[] guardianPrefabs = new Enemy[4];

    // Invulnerabilidad breve después de un golpe de guardián (para que un multigolpe no se sume entero).
    public const float PlayerInvulnerability = 0.4f;
    private static float lastPlayerHit = -100f;
    public static bool TryHitPlayer()
    {
        if (Time.time - lastPlayerHit < PlayerInvulnerability) return false;
        lastPlayerHit = Time.time;
        return true;
    }

    public Material FxMaterial => fxMaterial;
    public Material SoulMaterial => soulMaterial;
    public AudioClip SoulHitClip => soulHitClip;
    public Enemy[] GuardianPrefabs => guardianPrefabs;

    private AudioSource audioSource;
    private AudioClip previousMusic;
    private float lastPulseFx;

    private void Awake()
    {
        Instance = this;
        Telegraph.SharedMaterial = telegraphMaterial;
        audioSource = UiKit.UiAudio(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private AudioClip MusicFor(GuardianKind k) =>
        k == GuardianKind.Fundidor ? musicFundidor : k == GuardianKind.Viuda ? musicViuda : k == GuardianKind.Recaudador ? musicRecaudador : musicBarquero;

    // En el modo infinito suena su propia música: los guardianes no la cambian.
    public static bool KeepMusic;

    public void OnGuardianSpawn(Guardian g)
    {
        GameMusic music = KeepMusic ? null : FindAnyObjectByType<GameMusic>();
        if (music != null)
        {
            if (previousMusic == null) previousMusic = music.CurrentClip;
            music.SwitchTo(MusicFor(g.Kind), 0.6f);
        }
        if (emergeClip != null) audioSource.PlayOneShot(emergeClip, GameSettings.SfxVolume);
    }

    public void OnGuardianDefeated(Guardian g)
    {
        if (defeatClip != null) audioSource.PlayOneShot(defeatClip, GameSettings.SfxVolume);
        ItemFx.Burst(g.transform.position + Vector3.up * 1.5f, 60, 1.2f, new Color(1f, 0.6f, 0.2f), fxMaterial, 9f, 0.5f, 0.9f);
        // ¿Queda otro guardián vivo? Si no, regresa la música del mapa.
        foreach (Guardian other in Guardian.Alive)
            if (other != g && !other.IsDead) return;
        GameMusic music = KeepMusic ? null : FindAnyObjectByType<GameMusic>();
        if (music != null && previousMusic != null)
        {
            music.SwitchTo(previousMusic, 2f);
            previousMusic = null;
        }
    }

    public void EmergeFx(Vector3 ground)
    {
        AttackFx.Magic(ground + Vector3.up * 1f, 2.5f, new Color(0.65f, 0.2f, 1f));
        Dust(ground, 2f);
    }

    // Golpe al piso: grietas con rocas, onda y un poco de polvo.
    public void Dust(Vector3 at, float radius)
    {
        FxKit.Crack(at, radius, new Color(1f, 0.6f, 0.25f));
        FxKit.Ring(at, radius * 1.3f, new Color(1f, 0.9f, 0.6f), new Color(0.8f, 0.45f, 0.2f));
    }

    public void Foam(Vector3 at) => AttackFx.Magic(at + Vector3.up * 0.4f, 1.2f, new Color(0.35f, 1f, 0.9f));

    public void Pulse(Vector3 at)
    {
        if (Time.time - lastPulseFx < 0.05f) return;
        lastPulseFx = Time.time;
        // Llamas moradas de alma por donde avanza la línea.
        for (int i = 0; i < 3; i++) AttackFx.Wisp(at + Vector3.up * 0.3f, i == 0 ? new Color(0.95f, 0.8f, 1f) : new Color(0.7f, 0.25f, 1f), 0.7f);
    }

    public void Windup(Guardian g, GuardianKind k, int attack)
    {
        if (windupClip != null) PlayAt(windupClip, g.transform.position, 0.5f);
    }

    public void Play(GuardianKind k, int attack, Vector3 at)
    {
        // Destello de su color al soltar el golpe.
        AttackFx.Magic(at + Vector3.up * 1.6f, 1.2f, GuardianOrb.ColorFor(k));
        int i = (int)k * 2 + Mathf.Clamp(attack, 0, 1);
        if (attackClips != null && i < attackClips.Length && attackClips[i] != null)
            PlayAt(attackClips[i], at, 1f);
    }

    private static void PlayAt(AudioClip clip, Vector3 at, float vol) =>
        AudioSource.PlayClipAtPoint(clip, at, vol * GameSettings.SfxVolume);
}
