using UnityEngine;

// Si Pogoman se queda más de 1 s rebotando en la cabeza de un demonio, el demonio contraataca
// con algo de su estilo para quitárselo de encima:
//   - Demonio zombie: SACUDIDA, se zarandea y te avienta de lado.
//   - Hongo: ESPORAS, suelta una nube que pica y te bota para arriba.
//   - Ojo alado: DESCARGA, un chispazo que te empuja.
// (Los jefes y guardianes tienen sus propias reglas.)
[DefaultExecutionOrder(60)]
public class HeadStandPunish : MonoBehaviour
{
    [SerializeField] private float timeToPunish = 1f;
    [Tooltip("Si te bajas de la cabeza más de esto, el contador vuelve a cero.")]
    [SerializeField] private float forgetTime = 0.45f;
    [SerializeField] private float baseDamage = 10f;

    private PogoPlayer player;
    private Rigidbody rb;
    private CapsuleCollider capsule;
    private EnemyTracker current;
    private float onTime, offTime;
    private readonly RaycastHit[] hits = new RaycastHit[12];
    private Material fx;
    private static AudioClip zap, puff, shake;
    private AudioSource audioSource;

    // Para pruebas y HUD.
    public static int Punishments { get; private set; }

    private void Awake()
    {
        player = GetComponent<PogoPlayer>();
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        if (zap == null)
        {
            zap = Synth("Descarga", 0.35f, t => (Mathf.PerlinNoise(t * 900f, 0f) * 2f - 1f) * Mathf.Exp(-t * 8f) + Mathf.Sign(Mathf.Sin(t * 2f * Mathf.PI * 140f)) * 0.25f * Mathf.Exp(-t * 12f));
            puff = Synth("Esporas", 0.5f, t => (Mathf.PerlinNoise(t * 380f, 3f) * 2f - 1f) * Mathf.Sin(Mathf.Clamp01(t / 0.5f) * Mathf.PI));
            shake = Synth("Sacudida", 0.4f, t => Mathf.Sin(2f * Mathf.PI * (70f + 30f * Mathf.Sin(t * 60f)) * t) * Mathf.Exp(-t * 6f));
        }
    }

    private void FixedUpdate()
    {
        if (player == null || PogoBridge.IsDead(player))
            return;
        if (fx == null && ItemEffects.Instance != null)
            fx = ItemEffects.Instance.FxMaterial;

        float dt = Time.fixedDeltaTime;
        EnemyTracker below = EnemyBelow();
        // Cuenta aunque brinques de una cabeza a otra (si no, una pila de demonios sería un refugio).
        if (below != null)
        {
            current = below;
            onTime += dt;
            offTime = 0f;
        }
        else
        {
            offTime += dt;
            if (offTime > forgetTime)
            {
                current = null;
                onTime = 0f;
            }
        }

        if (current != null && onTime >= timeToPunish)
        {
            Punish(current);
            current = null;
            onTime = 0f;
        }
    }

    // El demonio que está justo debajo de los pies (hasta ~1.2 m de rebote).
    private EnemyTracker EnemyBelow()
    {
        float half = capsule != null ? capsule.height * 0.5f : 1f;
        float r = capsule != null ? capsule.radius * 0.9f : 0.3f;
        int n = Physics.SphereCastNonAlloc(transform.position, r, Vector3.down, hits, half + 1.3f, ~0, QueryTriggerInteraction.Collide);
        EnemyTracker best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (c.attachedRigidbody == rb) continue;
            EnemyTracker t = c.GetComponentInParent<EnemyTracker>();
            if (t == null || t.IsDead || t.IsBoss || t.GetComponent<DevilBoss>() != null || t.GetComponent<Guardian>() != null)
                continue;
            // Tiene que estar ENCIMA (no a un lado chocando con él).
            if (transform.position.y - half < t.Top.y - 0.45f) continue;
            if (hits[i].distance < bestD) { bestD = hits[i].distance; best = t; }
        }
        return best;
    }

    private float DamageMultiplier => EnemyDirector.Instance != null ? EnemyDirector.Instance.DamageMultiplier : 1f;

    private void Punish(EnemyTracker enemy)
    {
        Punishments++;
        Vector3 top = enemy.Top;
        Vector3 side = transform.position - enemy.Center; side.y = 0f;
        if (side.sqrMagnitude < 0.01f) side = Random.insideUnitSphere;
        side.y = 0f;
        side.Normalize();
        float damage = baseDamage * DamageMultiplier * (enemy.IsElite ? 2f : 1f);
        enemy.SendMessage("OnHeadPunish", SendMessageOptions.DontRequireReceiver);   // la animación del demonio

        Vector3 launch;
        if (enemy.GetComponent<OjoEnemy>() != null)
        {
            // Descarga eléctrica.
            launch = side * 9f + Vector3.up * 7f;
            ItemFx.Burst(top, 45, 0.6f, new Color(0.45f, 0.9f, 1f), fx, 9f, 0.18f, 0.3f);
            ItemFx.Burst(top, 20, 0.2f, Color.white, fx, 14f, 0.1f, 0.15f);
            Play(zap, 1f);
        }
        else if (enemy.GetComponentInChildren<HongoRigAnimator>() != null)
        {
            // Nube de esporas: sube y pica.
            launch = side * 4f + Vector3.up * 14f;
            ItemFx.Burst(top + Vector3.up * 0.3f, 60, 1.2f, new Color(0.55f, 0.95f, 0.35f, 0.7f), fx, 3f, 0.6f, 1.1f);
            ItemFx.Burst(top, 25, 0.4f, new Color(0.85f, 1f, 0.5f), fx, 6f, 0.25f, 0.5f);
            Play(puff, 1f);
        }
        else
        {
            // Sacudida: se zarandea y te avienta de lado.
            launch = side * 12f + Vector3.up * 8f;
            ItemFx.Burst(top, 35, 0.5f, new Color(0.55f, 0.4f, 0.3f), fx, 7f, 0.3f, 0.5f);
            Play(shake, Random.Range(0.9f, 1.1f));
        }

        PlayerVitals.Damage(player, damage);
        rb.linearVelocity = launch;
    }

    private void Play(AudioClip clip, float pitch)
    {
        if (clip == null) return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, 0.8f * GameSettings.SfxVolume);
    }

    private static AudioClip Synth(string name, float seconds, System.Func<float, float> f)
    {
        const int rate = 44100;
        int n = (int)(rate * seconds);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)rate) * 0.7f, -1f, 1f);
        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
