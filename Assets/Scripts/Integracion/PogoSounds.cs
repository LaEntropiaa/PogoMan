using UnityEngine;

// Sonidos de Pogoman sin tocar el script de movimiento: "escucha" la velocidad
// del Rigidbody. Un salto hacia arriba repentino = rebote del pogo; uno muy
// fuerte = géiser. Mientras PogoPlayer.IsSliding sea true suena el deslizamiento.
[RequireComponent(typeof(Rigidbody))]
public class PogoSounds : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip bounceClip;
    [SerializeField] private AudioClip geyserClip;
    [SerializeField] private AudioClip slideLoopClip;

    [Header("Detección")]
    [Tooltip("Cambio mínimo de velocidad vertical (m/s) para contar como rebote.")]
    [SerializeField] private float bounceMinVelocityJump = 4f;
    [Tooltip("Velocidad hacia arriba a partir de la cual es un géiser y no un rebote.")]
    [SerializeField] private float geyserVelocity = 12f;

    [Header("Volumen")]
    [SerializeField, Range(0f, 1f)] private float bounceVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float geyserVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float slideVolume = 0.4f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.08f);

    private Rigidbody rb;
    private PogoPlayer player;
    private AudioSource oneShots;
    private AudioSource slideSource;
    private float lastVerticalSpeed;
    private float slideFade;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        player = GetComponent<PogoPlayer>();

        oneShots = gameObject.AddComponent<AudioSource>();
        oneShots.playOnAwake = false;
        oneShots.spatialBlend = 0f;

        slideSource = gameObject.AddComponent<AudioSource>();
        slideSource.playOnAwake = false;
        slideSource.loop = true;
        slideSource.clip = slideLoopClip;
        slideSource.spatialBlend = 0f;
        slideSource.volume = 0f;
    }

    private void FixedUpdate()
    {
        float vy = rb.linearVelocity.y;
        float jump = vy - lastVerticalSpeed;

        if (jump > bounceMinVelocityJump && vy > 2f)
        {
            // Con el Compresor un salto con espacio puede pasar de 12 m/s sin ser géiser.
            if (vy >= geyserVelocity && (PogoJump.Instance == null || PogoJump.Instance.GeyserAscent))
                Play(geyserClip, geyserVolume);
            else
                Play(bounceClip, bounceVolume);
        }

        lastVerticalSpeed = vy;
    }

    private void Update()
    {
        bool sliding = player != null && player.IsSliding;
        slideFade = Mathf.MoveTowards(slideFade, sliding ? 1f : 0f, Time.deltaTime * 6f);

        if (slideSource.clip == null)
            return;

        if (slideFade > 0f && !slideSource.isPlaying)
            slideSource.Play();
        else if (slideFade <= 0f && slideSource.isPlaying)
            slideSource.Stop();

        Vector3 v = rb.linearVelocity;
        float speed = new Vector2(v.x, v.z).magnitude;
        slideSource.pitch = 0.8f + Mathf.Clamp01(speed / 18f) * 0.5f;
        slideSource.volume = slideVolume * slideFade * GameSettings.SfxVolume;
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        oneShots.pitch = Random.Range(pitchRange.x, pitchRange.y);
        oneShots.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }
}
