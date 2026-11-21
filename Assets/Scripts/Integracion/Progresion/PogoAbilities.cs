using UnityEngine;
using UnityEngine.InputSystem;

// Habilidades de Pogoman que vienen de las mejoras (se agrega solo al iniciar el nivel):
//  - Dash con Shift (o RB en control): distancia, recarga y rapidez escalan con las stats.
//    Durante el dash Pogoman no recibe daño.
//  - Efecto y sonido de los saltos en el aire.
[DefaultExecutionOrder(200)]
public class PogoAbilities : MonoBehaviour
{
    [SerializeField] private float baseDashDuration = 0.17f;

    public static PogoAbilities Instance { get; private set; }

    public float DashCooldownLeft { get; private set; }
    public float DashCooldownTotal { get; private set; } = 1f;
    public bool Dashing => dashTime > 0f;

    private Rigidbody rb;
    private PogoPlayer player;
    private Camera cam;
    private Vector3 dashDirection;
    private float dashSpeed;
    private float dashTime;
    private float entrySpeed;
    private bool dashRequested;
    private TrailRenderer trail;
    private AudioSource audioSource;
    private AudioClip dashClip, airJumpClip;
    private Material fxMaterial;

    public void Configure(AudioClip dash, AudioClip airJump, Material fx)
    {
        dashClip = dash;
        airJumpClip = airJump;
        fxMaterial = fx;
        if (trail != null)
            trail.sharedMaterial = fx;
    }

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody>();
        player = GetComponent<PogoPlayer>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.22f;
        trail.minVertexDistance = 0.1f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.9f), new Keyframe(1f, 0f));
        trail.colorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(new Color(0.6f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.6f, 1f), 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) },
        };
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.emitting = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        cam = Camera.main;
        if (PogoJump.Instance != null)
            PogoJump.Instance.AirJumped += OnAirJumped;
    }

    private static bool DashPressed() =>
        (Keyboard.current != null && (Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame)) ||
        (Gamepad.current != null && (Gamepad.current.rightShoulder.wasPressedThisFrame || Gamepad.current.buttonEast.wasPressedThisFrame));

    private void Update()
    {
        if (Time.timeScale <= 0f || (player != null && PogoBridge.IsDead(player)))
            return;

        DashCooldownLeft = Mathf.Max(0f, DashCooldownLeft - Time.deltaTime);

        // Estela cuando traes mucho impulso (bunny hops, slide, dash).
        PogoJump jump = PogoJump.Instance;
        bool fast = jump != null && jump.HorizontalSpeed > jump.BaseSpeed * 1.6f;
        trail.emitting = Dashing || fast;

        if (DashPressed() && DashCooldownLeft <= 0f && !Dashing)
            dashRequested = true;
    }

    private void FixedUpdate()
    {
        PlayerStats stats = PlayerStats.Instance;

        if (dashRequested)
        {
            dashRequested = false;
            StartDash(stats);
        }

        if (!Dashing)
            return;

        dashTime -= Time.fixedDeltaTime;
        Vector3 v = dashDirection * dashSpeed;
        v.y = Mathf.Max(rb.linearVelocity.y * 0.2f, 0.4f);   // casi horizontal, flota un poquito
        rb.linearVelocity = v;

        if (dashTime <= 0f)
            EndDash();
    }

    private void StartDash(PlayerStats stats)
    {
        float distance = stats != null ? stats.Get(PogoStat.DashDistance) : 6f;
        float speedMult = stats != null ? Mathf.Max(0.3f, stats.Get(PogoStat.DashSpeed)) : 1f;
        float duration = baseDashDuration / speedMult;

        dashDirection = InputDirection();
        Vector3 hv = rb.linearVelocity; hv.y = 0f;
        entrySpeed = hv.magnitude;
        dashSpeed = distance / duration;
        dashTime = duration;
        DashCooldownTotal = stats != null ? stats.Get(PogoStat.DashCooldown) : 2.5f;
        DashCooldownLeft = DashCooldownTotal;

        if (PogoJump.Instance != null)
            PogoJump.Instance.DashSpeedCap = dashSpeed + 1f;
        if (PlayerVitals.Instance != null)
            PlayerVitals.Instance.InvulnerableUntil = Time.time + duration + 0.12f;

        trail.emitting = true;
        Burst(transform.position, 14, 0.5f);
        Play(dashClip, Random.Range(0.95f, 1.08f), 0.7f);
    }

    private void EndDash()
    {
        dashTime = 0f;

        // Sales del dash con más velocidad de la que traías (y se conserva con bunny hops / slide).
        if (PogoJump.Instance != null)
        {
            PogoJump.Instance.DashSpeedCap = 0f;
            PogoJump.Instance.ExitDash(dashDirection, entrySpeed);
        }
    }

    private Vector3 InputDirection()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (Gamepad.current != null && input.sqrMagnitude < 0.01f)
            input = Gamepad.current.leftStick.ReadValue();

        if (input.sqrMagnitude > 0.01f && cam != null)
        {
            Vector3 f = cam.transform.forward; f.y = 0f; f.Normalize();
            Vector3 r = cam.transform.right; r.y = 0f; r.Normalize();
            return (r * input.x + f * input.y).normalized;
        }

        Vector3 h = rb.linearVelocity; h.y = 0f;
        if (h.sqrMagnitude > 1f)
            return h.normalized;

        if (cam != null)
        {
            Vector3 f = cam.transform.forward; f.y = 0f;
            if (f.sqrMagnitude > 0.01f)
                return f.normalized;
        }
        return transform.forward;
    }

    private void OnAirJumped()
    {
        Burst(transform.position + Vector3.down * 0.6f, 12, 0.35f);
        int used = PogoJump.Instance != null ? PogoJump.Instance.AirJumpsMax - PogoJump.Instance.AirJumpsLeft : 1;
        Play(airJumpClip, 1f + used * 0.06f, 0.6f);
    }

    private void Play(AudioClip clip, float pitch, float volume)
    {
        if (clip == null)
            return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }

    // Aro de aire que sale del salto / dash.
    private void Burst(Vector3 point, int count, float radius)
    {
        var go = new GameObject("Rafaga");
        go.transform.position = point;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        main.startColor = new Color(0.75f, 0.92f, 1f, 0.8f);
        main.maxParticles = count;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0f;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        if (fxMaterial != null)
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = fxMaterial;

        ps.Play();
    }
}
