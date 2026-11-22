using UnityEngine;
using UnityEngine.InputSystem;

// Capa de movimiento de Pogoman encima de PogoPlayer (sin modificarlo).
// El control en el aire lo hace este script (a PogoPlayer se le pone aceleración en el aire 0)
// para que la velocidad se CONSERVE, estilo Megabonk:
//  - Bunny hop: con Espacio apretado (o justo a tiempo) al caer conservas tu velocidad y ganas un poco.
//    Si caes sin apretarlo pierdes parte del impulso.
//  - Slide (Ctrl): entras con la velocidad que traes y en bajada aceleras.
//    Espacio durante el slide = salto que conserva la velocidad (Ctrl + Espacio mantenidos = slide-hops).
//  - El dash te deja con más velocidad de la que traías (PogoAbilities).
//  - Yendo rápido, mover el stick GIRA tu dirección en vez de frenarte.
//  - Espacio al caer = salto alto; mantenerlo mientras sube = más alto; entre más rápido, más alto.
//  - Saltos extra en el aire (mejora), que también te redirigen.
[DefaultExecutionOrder(-10)]   // antes que PogoPlayer: su límite de velocidad ya llega ajustado
[RequireComponent(typeof(Rigidbody))]
public class PogoJump : MonoBehaviour
{
    [Header("Gravedad")]
    [SerializeField] private float extraGravity = 12f;
    [Tooltip("Fracción de la gravedad extra que se aplica mientras subes con el salto apretado.")]
    [SerializeField, Range(0f, 1f)] private float holdGravityFactor = 0.3f;
    [SerializeField] private float maxHoldTime = 0.35f;

    [Header("Salto con espacio")]
    [SerializeField] private float spaceBoost = 3f;
    [Tooltip("Si apretaste espacio hasta este tiempo antes de tocar el piso, cuenta.")]
    [SerializeField] private float jumpBuffer = 0.15f;
    [Tooltip("Si aprietas espacio hasta este tiempo después del rebote, también cuenta.")]
    [SerializeField] private float lateWindow = 0.1f;

    [Header("Bonus por velocidad")]
    [SerializeField] private float speedBonusPerUnit = 0.09f;
    [SerializeField] private float maxSpeedBonus = 1.5f;

    [Header("Límites")]
    [Tooltip("Velocidad vertical máxima de un salto normal.")]
    [SerializeField] private float maxJumpVelocity = 12.5f;
    [Tooltip("Arriba de esta velocidad el impulso viene de un géiser.")]
    [SerializeField] private float geyserVelocity = 12f;

    [Header("Control en el aire")]
    [Tooltip("Aceleración hasta la velocidad base (m/s²).")]
    [SerializeField] private float airAcceleration = 45f;
    [Tooltip("Grados por segundo que gira tu dirección a velocidad base (más rápido = gira menos).")]
    [SerializeField] private float airTurnRate = 540f;
    [SerializeField] private float minTurnRate = 170f;
    [Tooltip("Frenado al empujar en contra de tu dirección yendo rápido.")]
    [SerializeField] private float airBrake = 20f;
    [Tooltip("Frenado sin tocar nada (solo a velocidad base o menos).")]
    [SerializeField] private float idleDeceleration = 10f;
    [Tooltip("Velocidad extra que se pierde por segundo en el aire.")]
    [SerializeField] private float airDrag = 0.45f;

    [Header("Impulso (bunny hop)")]
    [Tooltip("Velocidad que ganas en cada rebote con espacio apretado.")]
    [SerializeField] private float perfectHopGain = 1.1f;
    [Tooltip("Fracción del impulso extra que conservas al caer SIN apretar espacio.")]
    [SerializeField, Range(0f, 1f)] private float missedHopKeep = 0.7f;
    [SerializeField] private float maxMomentumSpeed = 32f;
    [Tooltip("Velocidad extra que deja el dash al terminar.")]
    [SerializeField] private float dashMomentumBonus = 4f;

    [Header("Deslizamiento")]
    [Tooltip("Velocidad máxima del slide en plano si no traes impulso.")]
    [SerializeField] private float flatSlideSpeed = 12f;
    [Tooltip("Fuerza que mantiene a Pogoman pegado al piso mientras se desliza.")]
    [SerializeField] private float slideStickForce = 30f;
    [Tooltip("Velocidad máxima hacia arriba permitida al deslizarse (evita brincos en desniveles).")]
    [SerializeField] private float slideMaxUpSpeed = 0.5f;
    [SerializeField] private float slideJumpVelocity = 8.5f;
    [Tooltip("Velocidad horizontal que suma el salto desde el slide.")]
    [SerializeField] private float slideJumpBoost = 1.5f;

    [Header("Rampas")]
    [SerializeField] private float slopeAcceleration = 20f;
    [SerializeField] private float maxExtraSlideSpeed = 20f;
    [SerializeField] private float extraSpeedDecay = 5f;
    [Tooltip("Qué tan rápido se pierde el impulso deslizándote en plano (más bajo = lo conservas más).")]
    [SerializeField] private float slideMomentumDecay = 1.5f;
    [SerializeField] private float uphillMomentumDecay = 8f;
    [SerializeField] private float minSlopeAngle = 4f;

    [Header("Saltos en el aire (mejora)")]
    [SerializeField] private float airJumpVelocity = 9.5f;
    [Tooltip("Si hay piso más cerca que esto, el espacio cuenta como salto normal, no como salto en el aire.")]
    [SerializeField] private float airJumpGroundClearance = 0.9f;

    public static PogoJump Instance { get; private set; }
    public float CrouchCharge => 0f;   // (lo lee PogoAnimator; la carga se cambió por el salto desde el slide)
    public bool LastJumpWasBoosted { get; private set; }
    public int AirJumpsLeft { get; private set; }
    public int AirJumpsMax => PlayerStats.Instance != null ? Mathf.RoundToInt(PlayerStats.Instance.Get(PogoStat.AirJumps)) : 0;
    public event System.Action AirJumped;
    public event System.Action SlideJumped;
    public event System.Action GeyserLaunched;
    // Balero de Diablito: impulso extra por rebote con espacio.
    public float ExtraHopGain { get; set; }
    // Velocidad mínima permitida mientras dura el dash (la pone PogoAbilities).
    public float DashSpeedCap { get; set; }
    public float HorizontalSpeed { get; private set; }
    public float BaseSpeed => baseMaxAirSpeed * SpeedMultiplier;
    public float CurrentMaxAirSpeed => Mathf.Max(BaseSpeed, HorizontalSpeed, DashSpeedCap);
    // Objetos: Manubrio Manijuela (control en el aire) y Compresor de los Lamentos (altura del salto con espacio).
    public float AirControlMultiplier { get; set; } = 1f;
    public float ChargedJumpHeightMultiplier { get; set; } = 1f;
    // El impulso de este rebote vino de un géiser (no de un salto con espacio).
    public bool GeyserAscent => geyserAscent;
    // Con la gravedad suave mientras sostienes espacio la altura no crece con v², por eso 0.63 y no 0.5
    // (medido: ×1.5 de velocidad² daba solo ×1.38 de altura).
    private float ChargedVelocityFactor => Mathf.Pow(Mathf.Max(1f, ChargedJumpHeightMultiplier), 0.63f);

    // Resorte Sónico: rebotar sobre un enemigo recupera los saltos en el aire.
    public void RefillAirJumps() => AirJumpsLeft = AirJumpsMax;

    private Rigidbody rb;
    private PogoPlayer player;
    private CapsuleCollider capsule;
    private float baseMaxAirSpeed = 8.5f;
    private float lastVerticalSpeed;
    private float lastJumpPress = -10f;
    private float lastBounceTime = -10f;
    private float lastSlideJump = -10f;
    private bool boostedThisBounce;
    private bool geyserAscent;
    private float holdTimer;
    private float extraSlideSpeed;
    private bool wasCrouching;
    private bool wasSliding;
    private bool slideHop;
    private bool geyserFlight;
    private float geyserFlightTime;
    private bool airJumpRequested;
    private bool slideJumpRequested;

    private float SpeedMultiplier => PlayerStats.Instance != null ? PlayerStats.Instance.Get(PogoStat.MoveSpeed) : 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        player = GetComponent<PogoPlayer>();
        capsule = GetComponent<CapsuleCollider>();
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        baseMaxAirSpeed = PogoBridge.GetFloat(player, "maxAirSpeed", 8.5f);
        // El control en el aire es de este script: PogoPlayer ya no acelera ni frena en el aire.
        PogoBridge.SetFloat(player, "airAcceleration", 0f);
        PogoBridge.SetFloat(player, "airDeceleration", 0f);

        // Sin fricción: así no se queda pegado a las paredes al empujar contra ellas.
        if (capsule != null)
            capsule.sharedMaterial = new PhysicsMaterial("PogomanSinFriccion")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
    }

    // ---- Anti-atorones ----
    // Si Pogoman queda apoyado en una orilla que el detector de piso de PogoPlayer no ve,
    // nunca "aterriza" y se queda colgado. Aquí se detecta el apoyo con los contactos reales.
    private float supportedTime;
    private bool supportedThisStep;
    private float lastSlideTime = -10f;
    // (Para pruebas: permite comparar con el comportamiento anterior.)
    public static bool RampFix = true;
    [Tooltip("Distancia máxima bajo los pies para volver a pegar el slide a la rampa.")]
    [SerializeField] private float slideSnapDistance = 0.7f;

    // Busca piso justo debajo; si está a menos de slideSnapDistance, baja a Pogoman hasta tocarlo
    // y le quita la velocidad que lo despegaba (la conserva a lo largo de la rampa).
    private bool SnapToSlope(ref Vector3 v)
    {
        var capsule = GetComponent<CapsuleCollider>();
        float radius = capsule != null ? capsule.radius * 0.9f : 0.3f;
        float bottom = capsule != null ? capsule.height * 0.5f - capsule.radius : 0.65f;
        Vector3 origin = rb.position + Vector3.up * 0.05f;
        if (!Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, bottom + 0.05f + slideSnapDistance, ~0, QueryTriggerInteraction.Ignore))
            return false;
        if (hit.rigidbody != null || Vector3.Angle(hit.normal, Vector3.up) > 55f)
            return false;
        float gap = hit.distance - (bottom + 0.05f + (capsule != null ? capsule.radius - radius : 0.035f));
        if (gap > 0.01f)
        {
            rb.position += Vector3.down * gap;
            transform.position = rb.position;   // PogoPlayer revisa el piso en este mismo paso
        }
        float away = Vector3.Dot(v, hit.normal);
        if (away > 0f)
        {
            // Sigue la rampa en vez de salir volando, sin perder rapidez.
            float speed = v.magnitude;
            v = Vector3.ProjectOnPlane(v, hit.normal).normalized * speed;
        }
        return true;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.rigidbody != null && collision.rigidbody.GetComponent<Enemy>() != null)
            return;
        for (int i = 0; i < collision.contactCount; i++)
            if (collision.GetContact(i).normal.y > 0.3f)
            {
                supportedThisStep = true;
                return;
            }
    }

    // Lo llaman los géiseres del mapa al lanzar a Pogoman.
    public void OnExternalLaunch(Vector3 velocity)
    {
        GeyserLaunched?.Invoke();
        geyserFlight = true;
        geyserFlightTime = 0f;
        lastVerticalSpeed = velocity.y;   // que no cuente como rebote
    }

    // Al terminar el dash: sales en esa dirección con más velocidad de la que traías.
    public void ExitDash(Vector3 direction, float entrySpeed)
    {
        float speed = Mathf.Min(Mathf.Max(entrySpeed, BaseSpeed) + dashMomentumBonus, maxMomentumSpeed);
        Vector3 v = rb.linearVelocity;
        Vector3 h = direction.normalized * speed;
        rb.linearVelocity = new Vector3(h.x, v.y, h.z);
        HorizontalSpeed = speed;
        PogoBridge.SetFloat(player, "maxAirSpeed", speed + 0.5f);
        // Si estás deslizándote, el slide también acepta esa velocidad.
        extraSlideSpeed = Mathf.Max(extraSlideSpeed, speed - flatSlideSpeed * SpeedMultiplier);
    }

    private static bool JumpHeld() =>
        (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) ||
        (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);

    private static bool JumpPressed() =>
        (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
        (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

    private void Update()
    {
        if (Time.timeScale <= 0f || player == null)
            return;

        if (JumpPressed())
        {
            lastJumpPress = Time.time;
            if (player.IsSliding)
                slideJumpRequested = true;
            else if (CanAirJump())
                airJumpRequested = true;
        }
    }

    private void FixedUpdate()
    {
        if (player == null || PogoBridge.IsDead(player))
            return;

        float dt = Time.fixedDeltaTime;
        Vector3 v = rb.linearVelocity;
        bool crouching = PogoBridge.IsCtrlHeld(player);
        bool grounded = PogoBridge.IsGrounded(player);
        bool sliding = crouching && grounded;
        float baseSpeed = BaseSpeed;

        if (grounded && Time.time - lastBounceTime > 0.05f)
            AirJumpsLeft = AirJumpsMax;

        // Apoyado en algo, sin "piso" según PogoPlayer y casi sin moverse: rebota igual.
        bool stuck = supportedThisStep && !grounded && v.y < 0.5f && v.y > -1f;
        supportedThisStep = false;
        supportedTime = stuck ? supportedTime + dt : 0f;
        // Agachado no: en una rampa PogoPlayer a veces no ve el piso y esto hacía "tropiezos" en el slide.
        if (supportedTime > 0.08f && !geyserFlight && !(crouching && RampFix))
        {
            supportedTime = 0f;
            v.y = crouching ? 2f : PogoBridge.GetFloat(player, "jumpForce", 7.5f);
            lastVerticalSpeed = 0f;   // que cuente como rebote
        }

        // ---- Vuelo de géiser: casi balístico hasta tocar piso ----
        if (geyserFlight)
        {
            geyserFlightTime += dt;
            if ((geyserFlightTime > 0.3f && grounded) || geyserFlightTime > 6f)
            {
                geyserFlight = false;
            }
            else
            {
                Vector3 wish = WishDirection();
                Vector3 h = Flat(v);
                h = Vector3.ClampMagnitude(h + wish * 6f * dt, Mathf.Max(h.magnitude, baseSpeed));
                v = new Vector3(h.x, v.y, h.z);
                Finish(v, h.magnitude);
                return;
            }
        }

        // ---- ¿Acaba de rebotar? (la velocidad vertical saltó hacia arriba) ----
        // Agachado no hay rebotes: si sube es porque el terreno lo empujó.
        if (!crouching && v.y - lastVerticalSpeed > 4f && v.y > 2f && Time.time - lastSlideJump > 0.25f)
            OnBounce(ref v);

        // ---- Salto desde el slide (Espacio, o mantenerlo al volver a caer agachado) ----
        bool justLanded = sliding && !wasSliding;
        if (sliding && Time.time - lastSlideJump > 0.3f && (slideJumpRequested || (justLanded && JumpHeld())))
            SlideJump(ref v);
        slideJumpRequested = false;
        wasSliding = sliding;

        if ((grounded && Time.time - lastSlideJump > 0.2f) || v.y <= 0f)
            slideHop = false;

        // ---- Slide en rampas: si el piso se separa un instante (bajadas, bordes), se pega otra vez ----
        if (sliding) lastSlideTime = Time.time;
        // PogoPlayer.StartSlide deja la velocidad vertical en 0 en cada "aterrizaje": en bajada eso te
        // despegaba de la rampa, caías, volvías a "aterrizar"... (los tropiezos). Aquí la velocidad sigue la rampa.
        if (RampFix && crouching && !geyserFlight && Time.time - lastSlideTime < 0.25f &&
            Time.time - lastSlideJump > 0.3f && v.y < 1f && SnapToSlope(ref v))
        {
            sliding = true;
            lastSlideTime = Time.time;
        }

        // ---- Deslizándose: pegado al piso, sin brincos (los géiseres sí lanzan) ----
        bool justSlideJumped = Time.time - lastSlideJump < 0.2f;
        if (sliding && !justSlideJumped && v.y < geyserVelocity)
        {
            if (v.y > slideMaxUpSpeed)
                v.y = slideMaxUpSpeed;
            v += Vector3.down * slideStickForce * dt;
        }

        // Saltando desde el slide con Ctrl apretado: se anula la caída en picada mientras subes.
        if (slideHop && crouching && v.y > 0f)
            v += Vector3.up * PogoBridge.GetFloat(player, "diveAcceleration", 35f) * dt;

        // ---- Espacio apretado justo después del rebote ----
        if (!boostedThisBounce && !geyserAscent && v.y > 0f &&
            Time.time - lastBounceTime < lateWindow && lastJumpPress > lastBounceTime)
        {
            v.y = Mathf.Min(v.y + spaceBoost, maxJumpVelocity) * ChargedVelocityFactor;
            boostedThisBounce = true;
            LastJumpWasBoosted = true;
            GainHop(ref v);
        }

        // ---- Salto en el aire ----
        if (airJumpRequested)
        {
            airJumpRequested = false;
            if (AirJumpsLeft > 0 && !grounded)
            {
                AirJumpsLeft--;
                v.y = Mathf.Max(v.y, airJumpVelocity);
                holdTimer = 0f;
                LastJumpWasBoosted = true;

                // Te redirige hacia donde apuntas sin perder velocidad.
                Vector3 wish = WishDirection();
                if (wish.sqrMagnitude > 0.01f)
                {
                    Vector3 h = wish.normalized * Mathf.Max(Flat(v).magnitude, baseSpeed);
                    v = new Vector3(h.x, v.y, h.z);
                }
                AirJumped?.Invoke();
            }
        }

        // ---- Gravedad (más suave mientras subes con el salto apretado) ----
        float gravity = extraGravity;
        if (v.y > 0f && JumpHeld() && holdTimer < maxHoldTime)
        {
            gravity *= holdGravityFactor;
            holdTimer += dt;
        }
        else if (v.y <= 0f)
        {
            holdTimer = maxHoldTime;   // ya no se puede "estirar" este salto
        }
        v += Vector3.down * gravity * dt;

        // ---- Control en el aire (conserva la velocidad) ----
        if (!grounded)
            AirControl(ref v, dt, baseSpeed);

        // ---- Slide y rampas ----
        HandleSlopes(ref v, dt, crouching, sliding, grounded);

        Vector3 flat = Flat(v);
        if (flat.magnitude > maxMomentumSpeed && DashSpeedCap <= 0f)
        {
            flat = flat.normalized * maxMomentumSpeed;
            v = new Vector3(flat.x, v.y, flat.z);
        }

        Finish(v, flat.magnitude);
    }

    private void Finish(Vector3 v, float horizontal)
    {
        HorizontalSpeed = horizontal;
        // El límite de PogoPlayer en el aire nunca debe cortar la velocidad que traes.
        PogoBridge.SetFloat(player, "maxAirSpeed", Mathf.Max(BaseSpeed, horizontal, DashSpeedCap) + 0.5f);
        rb.linearVelocity = v;
        lastVerticalSpeed = v.y;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // Dirección que pide el jugador, relativa a la cámara (la misma entrada que lee PogoPlayer).
    private Vector3 WishDirection()
    {
        Vector2 input = PogoBridge.Get<Vector2>(player, "input");
        Transform cam = PogoBridge.Get<Transform>(player, "cameraTransform");
        if (cam == null || input.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        Vector3 forward = cam.forward; forward.y = 0f; forward.Normalize();
        Vector3 right = cam.right; right.y = 0f; right.Normalize();
        return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
    }

    private void AirControl(ref Vector3 v, float dt, float baseSpeed)
    {
        Vector3 h = Flat(v);
        float speed = h.magnitude;
        Vector3 wish = WishDirection();
        float control = Mathf.Max(0.1f, AirControlMultiplier);

        if (wish.sqrMagnitude > 0.01f)
        {
            Vector3 dir = wish.normalized;
            if (speed <= baseSpeed + 0.05f)
            {
                // A velocidad normal: control directo y rápido.
                h = Vector3.MoveTowards(h, dir * baseSpeed * wish.magnitude, airAcceleration * control * dt);
            }
            else
            {
                // Yendo rápido: la dirección gira, la velocidad se queda.
                float angle = Vector3.Angle(h, dir);
                float turn = Mathf.Clamp(airTurnRate * baseSpeed / speed, minTurnRate, airTurnRate) * control;
                Vector3 newDir = Vector3.RotateTowards(h / speed, dir, turn * Mathf.Deg2Rad * dt, 0f);
                if (angle > 120f)
                    speed = Mathf.MoveTowards(speed, baseSpeed, airBrake * control * dt);   // jalar para atrás frena
                h = newDir * speed;
            }
        }
        else if (speed <= baseSpeed)
        {
            h = Vector3.MoveTowards(h, Vector3.zero, idleDeceleration * dt);
        }

        // El impulso extra se va perdiendo poco a poco.
        speed = h.magnitude;
        if (speed > baseSpeed)
            h = h / speed * Mathf.Max(baseSpeed, speed - airDrag * dt);

        v = new Vector3(h.x, v.y, h.z);
    }

    // Espacio en el aire: solo cuenta como salto extra si no estás a punto de tocar el piso
    // (si no, es el salto alto normal que se guarda para el rebote).
    private bool CanAirJump()
    {
        if (geyserFlight || AirJumpsLeft <= 0 || PogoBridge.IsDead(player) || player.IsSliding)
            return false;
        if (PogoBridge.IsGrounded(player) || Time.time - lastBounceTime < lateWindow + 0.05f)
            return false;

        float half = capsule != null ? capsule.height * 0.5f : 1f;
        return !Physics.Raycast(transform.position, Vector3.down, half + airJumpGroundClearance, ~0, QueryTriggerInteraction.Ignore);
    }

    private void OnBounce(ref Vector3 v)
    {
        lastBounceTime = Time.time;
        holdTimer = 0f;
        boostedThisBounce = false;
        LastJumpWasBoosted = false;
        geyserAscent = v.y >= geyserVelocity;

        if (geyserAscent)
            return;

        float horizontal = Flat(v).magnitude;
        float bonus = Mathf.Min(horizontal * speedBonusPerUnit, maxSpeedBonus);

        bool space = JumpHeld() || Time.time - lastJumpPress < jumpBuffer;
        if (space)
        {
            bonus += spaceBoost;
            boostedThisBounce = true;
            LastJumpWasBoosted = true;
            GainHop(ref v);
        }
        else
        {
            // Rebote sin espacio: se pierde parte del impulso extra.
            Vector3 h = Flat(v);
            float speed = h.magnitude;
            float b = BaseSpeed;
            if (speed > b)
            {
                h = h / speed * (b + (speed - b) * missedHopKeep);
                v = new Vector3(h.x, v.y, h.z);
            }
        }

        v.y = Mathf.Min(v.y + bonus, maxJumpVelocity);
        if (space)
            v.y *= ChargedVelocityFactor;
    }

    // Bunny hop bien hecho: conservas la velocidad y ganas un poco.
    private void GainHop(ref Vector3 v)
    {
        Vector3 h = Flat(v);
        float speed = h.magnitude;
        if (speed < BaseSpeed * 0.85f)
            return;   // parado o casi parado no se acumula nada
        h = h / speed * Mathf.Min(speed + perfectHopGain + ExtraHopGain, maxMomentumSpeed);
        v = new Vector3(h.x, v.y, h.z);
    }

    private void SlideJump(ref Vector3 v)
    {
        Vector3 h = Flat(v);
        float speed = h.magnitude;
        Vector3 dir = speed > 0.5f ? h / speed : WishDirection().normalized;
        if (dir.sqrMagnitude < 0.01f)
            dir = transform.forward;

        speed = Mathf.Min(Mathf.Max(speed, BaseSpeed) + slideJumpBoost, maxMomentumSpeed);
        h = dir * speed;
        float up = slideJumpVelocity + Mathf.Min(speed * speedBonusPerUnit, maxSpeedBonus);
        v = new Vector3(h.x, Mathf.Max(v.y, up), h.z);

        lastSlideJump = Time.time;
        slideHop = true;
        holdTimer = 0f;
        LastJumpWasBoosted = true;
        SlideJumped?.Invoke();
    }

    private void HandleSlopes(ref Vector3 v, float dt, bool crouching, bool sliding, bool grounded)
    {
        float flatMax = flatSlideSpeed * SpeedMultiplier;
        float current = Flat(v).magnitude;

        // Al agacharte (o mientras caes agachado) se conserva toda la velocidad que traes.
        bool pinMomentum = crouching && (!wasCrouching || !grounded);
        wasCrouching = crouching;
        if (pinMomentum)
            extraSlideSpeed = Mathf.Clamp(Mathf.Max(extraSlideSpeed, current - flatMax), 0f, maxMomentumSpeed - flatMax);

        float slopeSin = 0f;
        Vector3 downhill = Vector3.zero;

        if (sliding && Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f,
                ~0, QueryTriggerInteraction.Ignore))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle > minSlopeAngle && angle < 60f)
            {
                downhill = Vector3.ProjectOnPlane(Vector3.down, hit.normal).normalized;
                slopeSin = Mathf.Sin(angle * Mathf.Deg2Rad);
            }
        }

        if (slopeSin > 0f)
        {
            v += downhill * slopeAcceleration * slopeSin * dt;

            // Cuesta abajo se permite ir más rápido que el límite normal.
            if (Vector3.Dot(v, downhill) > 0f)
                extraSlideSpeed = Mathf.Min(Mathf.Max(maxExtraSlideSpeed, extraSlideSpeed),
                    extraSlideSpeed + slopeAcceleration * slopeSin * dt);
            else
                extraSlideSpeed = Mathf.MoveTowards(extraSlideSpeed, 0f, uphillMomentumDecay * dt);
        }
        else
        {
            // En plano y agachado casi no se pierde; sin agacharte se pierde rápido.
            float decay = crouching ? slideMomentumDecay : extraSpeedDecay;
            extraSlideSpeed = Mathf.MoveTowards(extraSlideSpeed, 0f, decay * dt);
        }

        PogoBridge.SetFloat(player, "maxSlideSpeed", Mathf.Max(flatMax + extraSlideSpeed, DashSpeedCap));
    }
}
