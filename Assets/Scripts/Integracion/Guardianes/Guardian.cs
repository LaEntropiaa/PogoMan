using System.Collections.Generic;
using UnityEngine;

public enum GuardianKind { Fundidor, Viuda, Recaudador, Barquero }

[System.Serializable]
public class GuardianAttackSpec
{
    public string clip;
    [Tooltip("Segundos desde el inicio del clip hasta el golpe (Balance.json).")]
    public float activeTime = 1f;
    [Tooltip("Duración del clip completo.")]
    public float duration = 2f;
    public float damage = 15f;
    [Tooltip("Empieza a contar al terminar la recuperación.")]
    public float cooldown = 3f;
}

// Guardián de un pilar (Fundidor, Viuda, Recaudador, Barquero).
// Fases: aparición (sale girando del suelo) → persecución → preparación (señal en el suelo)
// → ejecución (el golpe sale una sola vez al cruzar el tiempo activo) → recuperación (ventana
// para rebotarle encima) → ... → derrota. Al morir se cancela todo lo pendiente y el pilar
// entrega su llave y el cofre gratis como siempre (BossPillar ve que el jefe ya no existe).
public class Guardian : Enemy
{
    public static readonly List<Guardian> Alive = new List<Guardian>();

    [Header("Guardián")]
    [SerializeField] private GuardianKind kind;
    [SerializeField] private float maxHealth = 1000f;
    [Tooltip("Vida extra por minuto de partida (escalado de dificultad).")]
    [SerializeField] private float healthPerMinute = 0.04f;
    [SerializeField] private string idleClip, moveClip, hitClip, defeatClip;
    [SerializeField] private float defeatLength = 2f;
    [SerializeField] private GuardianAttackSpec[] attacks = new GuardianAttackSpec[2];
    [SerializeField] private float recoverTime = 1.1f;
    [Tooltip("Altura a la que flota (Barquero/Viuda).")]
    [SerializeField] private float hover = 0f;

    // Nombres que lee PogoBridge (barras de vida, EnemyTracker).
    private float currentHealth;
    private bool isDead;

    public GuardianKind Kind => kind;
    public bool IsDead => isDead;

    private enum State { Emerge, Chase, Attack, Recover, Flinch, Defeat }
    private State state = State.Emerge;
    private float stateTime;
    private Animator anim;
    private Transform model;
    private Transform player;
    private PogoPlayer pogo;
    private Collider playerCollider;
    private Collider body;
    private float[] cooldownLeft = new float[2];
    private float flinchCooldown;
    private bool wasGrounded;
    private Vector3 strafe;
    private float strafeTime;
    private AudioSource voice;

    // ---- Ataque en curso ----
    private int attackIndex = -1;
    private float attackTime;
    private int firedSteps;
    private bool hitPlayerThisAttack;
    private Vector3 lockedForward;
    private readonly List<Telegraph> signs = new List<Telegraph>();
    private readonly List<GameObject> pending = new List<GameObject>();   // proyectiles/ondas del encuentro
    private Vector3[] points = new Vector3[3];
    private float[] lineAngles = new float[3];
    private Light furnace;

    private float Minutes => PlayerStats.Instance != null ? PlayerStats.Instance.RunTime / 60f : 0f;
    private GuardianKit Kit => GuardianKit.Instance;

    // ------------------------------------------------------------------ inicio
    protected override void Awake()
    {
        base.Awake();
        rb.isKinematic = true;
        rb.useGravity = false;
        anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
            model = anim.transform;
            modelScale = model.localScale * SizeFactor;
            model.localScale = modelScale;
        }
        body = GetComponent<Collider>();
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0.6f;
        voice.minDistance = 6f;
        voice.maxDistance = 50f;
    }

    private void OnEnable() => Alive.Add(this);
    private void OnDisable() => Alive.Remove(this);

    private void Start()
    {
        pogo = FindAnyObjectByType<PogoPlayer>();
        player = pogo != null ? pogo.transform : null;
        playerCollider = pogo != null ? pogo.GetComponent<Collider>() : null;
        maxHealth *= 1f + healthPerMinute * Minutes;
        currentHealth = maxHealth;

        PlaceSafely();
        FacePlayer(1f);
        Kit?.OnGuardianSpawn(this);
        stateTime = 0f;
        Play(idleClip);
    }

    // Enemy lo movería solo hacia el objetivo: aquí el movimiento es propio.
    protected override void FixedUpdate() { }

    // ------------------------------------------------------------------ suelo y lugar
    private float GroundY(Vector3 p) => Telegraph.GroundY(p, p.y);

    private bool IsHazard(Vector3 p)
    {
        foreach (RaycastHit h in Physics.RaycastAll(p + Vector3.up * 4f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.GetComponent<LavaHazard>() != null)
                return true;
        return false;
    }

    private bool GoodSpot(Vector3 p, out Vector3 ground)
    {
        ground = p;
        if (!Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore) || hit.rigidbody != null)
            return false;
        if (Vector3.Angle(hit.normal, Vector3.up) > 32f || IsHazard(hit.point))
            return false;
        ground = hit.point;
        // Espacio libre para el cuerpo.
        if (Physics.CheckCapsule(ground + Vector3.up * 1.2f, ground + Vector3.up * 2.6f, 0.9f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        // No en una isla flotante: alrededor tiene que haber suelo parecido (ruta de regreso).
        int ok = 0;
        for (int i = 0; i < 8; i++)
        {
            Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * 4f;
            if (Physics.Raycast(ground + d + Vector3.up * 3f, Vector3.down, out RaycastHit h2, 8f, ~0, QueryTriggerInteraction.Ignore) &&
                Mathf.Abs(h2.point.y - ground.y) < 1.5f && !IsHazard(h2.point))
                ok++;
        }
        if (ok < 5)
            return false;
        // Ni pegado al jugador.
        if (player != null)
        {
            Vector3 d = player.position - ground; d.y = 0f;
            if (d.magnitude < 5f) return false;
        }
        return true;
    }

    private void PlaceSafely()
    {
        Vector3 start = transform.position;
        Vector3 ground;
        if (!GoodSpot(start, out ground))
        {
            bool found = false;
            for (float r = 3f; r <= 16f && !found; r += 2.5f)
                for (int i = 0; i < 12 && !found; i++)
                    found = GoodSpot(start + Quaternion.Euler(0f, i * 30f + r * 7f, 0f) * Vector3.forward * r, out ground);
            if (!found)
                ground = new Vector3(start.x, GroundY(start), start.z);
        }
        transform.position = ground + Vector3.up * hover;
        rb.position = transform.position;
    }

    // ------------------------------------------------------------------ bucle
    private void Update()
    {
        if (player == null)
            return;
        float dt = Time.deltaTime;
        stateTime += dt;
        flinchCooldown -= dt;
        for (int i = 0; i < cooldownLeft.Length; i++)
            if (state != State.Attack && state != State.Recover)
                cooldownLeft[i] -= dt;

        switch (state)
        {
            case State.Emerge: UpdateEmerge(); break;
            case State.Chase: UpdateChase(dt); break;
            case State.Attack: UpdateAttack(dt); break;
            case State.Recover:
                if (stateTime >= recoverTime) { state = State.Chase; stateTime = 0f; }
                break;
            case State.Flinch:
                if (stateTime >= 0.45f) { state = State.Chase; stateTime = 0f; Play(moveClip); }
                break;
            case State.Defeat: UpdateDefeat(); break;
        }

        if (state != State.Emerge && state != State.Defeat)
            CheckStomp();
        Bob();
        Juice(dt);
        wasGrounded = pogo != null && PogoBridge.IsGrounded(pogo);
    }

    // ---- Animación extra encima de los clips (más peso y personalidad) ----
    // Un poquito más grandes que el modelo original.
    private const float SizeFactor = 1.18f;
    private Vector3 modelScale = Vector3.one;
    private float juiceTime, hitShake;
    private Quaternion juiceRot = Quaternion.identity;

    private void Juice(float dt)
    {
        if (model == null) return;
        juiceTime += dt;
        float sx = 1f, sy = 1f, pitch = 0f, roll = 0f;
        float breathe = Mathf.Sin(juiceTime * 2.4f);
        sy += breathe * 0.025f; sx -= breathe * 0.012f;

        switch (state)
        {
            case State.Chase:
                // Camina echado para adelante y se bambolea.
                pitch += 7f;
                roll += Mathf.Sin(juiceTime * 7f) * 3.5f;
                sy += Mathf.Abs(Mathf.Sin(juiceTime * 7f)) * 0.04f;
                break;
            case State.Attack:
            {
                float active = Spec.activeTime;
                float t = attackTime;
                if (t < active)
                {
                    // Anticipación: se agacha, se ensancha y se echa para atrás, temblando al final.
                    float k = Mathf.SmoothStep(0f, 1f, t / active);
                    sy -= 0.14f * k; sx += 0.09f * k;
                    pitch -= 12f * k;
                    roll += Mathf.Sin(juiceTime * 45f) * 2.5f * k * k;
                }
                else
                {
                    // Golpe: se estira hacia adelante y regresa con rebote.
                    float k = Mathf.Clamp01((t - active) / 0.35f);
                    float snap = Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f);
                    sy += 0.16f * snap; sx -= 0.08f * snap;
                    pitch += 18f * snap;
                }
                break;
            }
            case State.Recover:
                pitch -= 4f * (1f - Mathf.Clamp01(stateTime / recoverTime));
                sy -= 0.05f * (1f - Mathf.Clamp01(stateTime / recoverTime));
                break;
            case State.Flinch:
                hitShake = Mathf.Max(hitShake, 1f - stateTime / 0.45f);
                break;
            case State.Emerge:
            {
                // Sale como resorte.
                float k = Mathf.Clamp01(stateTime / EmergeTime);
                float pop = k > 0.75f ? Mathf.Sin((k - 0.75f) / 0.25f * Mathf.PI) : 0f;
                sy += pop * 0.2f; sx -= pop * 0.1f;
                break;
            }
        }
        // Cada golpe que recibe (aunque no se interrumpa) lo hace temblar y aplastarse.
        hitShake = Mathf.MoveTowards(hitShake, 0f, dt * 3f);
        if (hitShake > 0f)
        {
            roll += Mathf.Sin(juiceTime * 50f) * 6f * hitShake;
            sy -= 0.1f * hitShake; sx += 0.06f * hitShake;
        }

        model.localScale = new Vector3(modelScale.x * sx, modelScale.y * sy, modelScale.z * sx);
        if (state != State.Emerge && state != State.Defeat)
        {
            Quaternion target = Quaternion.Euler(pitch, 0f, roll);
            juiceRot = Quaternion.Slerp(juiceRot, target, 1f - Mathf.Exp(-14f * dt));
            model.localRotation = juiceRot;
        }
    }

    // Sale del suelo girando.
    private const float EmergeTime = 1.8f;
    private Vector3 emergeBase;
    private void UpdateEmerge()
    {
        if (stateTime <= Time.deltaTime * 1.5f)
        {
            emergeBase = transform.position;
            Kit?.EmergeFx(emergeBase - Vector3.up * hover);
        }
        float k = Mathf.Clamp01(stateTime / EmergeTime);
        float e = 1f - (1f - k) * (1f - k);
        float depth = (body != null ? body.bounds.size.y : 3f) + 0.5f;
        transform.position = emergeBase + Vector3.down * depth * (1f - e);
        rb.position = transform.position;
        if (model != null)
            model.localRotation = Quaternion.Euler(0f, 900f * (1f - e), 0f);
        if (Random.value < 0.25f)
            Kit?.Dust(emergeBase - Vector3.up * hover, 1.6f);
        if (k >= 1f)
        {
            if (model != null) model.localRotation = Quaternion.identity;
            state = State.Chase;
            stateTime = 0f;
            cooldownLeft[0] = 0.8f;
            cooldownLeft[1] = 2.5f;
            Play(moveClip);
        }
    }

    private Vector3 ToPlayer { get { Vector3 d = player.position - transform.position; d.y = 0f; return d; } }

    private void FacePlayer(float k)
    {
        Vector3 d = ToPlayer;
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized), k);
    }

    private void UpdateChase(float dt)
    {
        float dist = ToPlayer.magnitude;
        int choice = ChooseAttack(dist);
        if (choice >= 0)
        {
            StartAttack(choice);
            return;
        }

        FacePlayer(1f - Mathf.Exp(-6f * dt));
        Vector3 dir = ToPlayer.normalized;
        float desired = PreferredDistance();
        Vector3 move = Vector3.zero;
        if (dist > desired + 0.5f) move = dir;
        else if (dist < desired - 1.5f && kind == GuardianKind.Viuda) move = -dir;   // la Viuda guarda distancia

        // El Recaudador se reacomoda rápido de lado después de estampar.
        if (strafeTime > 0f)
        {
            strafeTime -= dt;
            move = (move + strafe).normalized;
        }
        Move(move * moveSpeed * dt);
    }

    private float PreferredDistance()
    {
        switch (kind)
        {
            case GuardianKind.Fundidor: return 2.4f;
            case GuardianKind.Viuda: return 4f;
            case GuardianKind.Recaudador: return 2.2f;
            default: return 3f;
        }
    }

    private void Move(Vector3 step)
    {
        if (step.sqrMagnitude < 0.000001f) return;
        Vector3 next = transform.position + step;
        // No se mete a paredes ni al agua dañina ni cae a precipicios.
        if (Physics.SphereCast(transform.position + Vector3.up * 1.2f, 0.7f, step.normalized, out RaycastHit wall, step.magnitude + 0.4f, ~0, QueryTriggerInteraction.Ignore) &&
            wall.rigidbody == null && Vector3.Angle(wall.normal, Vector3.up) > 45f)
            return;
        float gy = GroundY(next);
        float baseY = transform.position.y - hover;
        if (Mathf.Abs(gy - baseY) > 1.6f || IsHazard(next))
            return;
        next.y = Mathf.Lerp(baseY, gy, 0.5f) + hover;
        rb.MovePosition(next);
        transform.position = next;
    }

    private void Bob()
    {
        if (model == null || state == State.Emerge || state == State.Defeat) return;
        if (hover > 0f)
            model.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 2.2f) * 0.12f, 0f);
    }

    // ------------------------------------------------------------------ ataques
    // Elige una habilidad lista que tenga sentido a esta distancia.
    private int ChooseAttack(float d)
    {
        bool a = cooldownLeft[0] <= 0f, b = cooldownLeft[1] <= 0f;
        switch (kind)
        {
            case GuardianKind.Fundidor:
                if (a && d < 3.8f) return 0;
                if (b && d >= 3.8f && d <= 9f) return 1;
                break;
            case GuardianKind.Viuda:
                if (a && d >= 2f && d <= 5f) return 0;
                if (b && d <= 9f) return 1;
                break;
            case GuardianKind.Recaudador:
                if (a && d < 3f) return 0;
                if (b && d >= 3f && d <= 9f) return 1;
                break;
            case GuardianKind.Barquero:
                if (a && d < 4f) return 0;
                if (b && d >= 4f && d <= 12f) return 1;
                break;
        }
        return -1;
    }

    private GuardianAttackSpec Spec => attacks[attackIndex];

    private void StartAttack(int i)
    {
        attackIndex = i;
        attackTime = 0f;
        firedSteps = 0;
        hitPlayerThisAttack = false;
        state = State.Attack;
        stateTime = 0f;
        FacePlayer(1f);
        lockedForward = transform.forward;
        Play(Spec.clip, 0.08f);
        Kit?.Windup(this, kind, i);

        Vector3 c = Feet;
        // Amarillo/blanco: se distingue bien sobre el suelo rojo del Estigia.
        Color danger = new Color(1f, 0.92f, 0.25f, 0.7f);
        switch (kind)
        {
            case GuardianKind.Fundidor when i == 0:
                signs.Add(Telegraph.Fan(c, lockedForward, 80f, 0.4f, 3.8f).SetColor(danger));
                break;
            case GuardianKind.Fundidor:
                signs.Add(Telegraph.Ring(c, 1f, 8f).SetColor(new Color(1f, 0.85f, 0.3f, 0.14f)));
                furnace = new GameObject("Horno").AddComponent<Light>();
                furnace.transform.SetParent(transform, false);
                furnace.transform.localPosition = new Vector3(0f, 1.8f, 0.6f);
                furnace.type = LightType.Point; furnace.color = new Color(1f, 0.45f, 0.1f); furnace.range = 8f; furnace.shadows = LightShadows.None;
                break;
            case GuardianKind.Viuda when i == 0:
                signs.Add(Telegraph.Fan(c, lockedForward, 120f, 0.5f, 4.8f).SetColor(new Color(0.85f, 0.4f, 1f, 0.7f)));
                break;
            case GuardianKind.Viuda:
                float baseAngle = Random.value * 120f;
                for (int k = 0; k < 3; k++)
                {
                    lineAngles[k] = baseAngle + k * 120f;
                    signs.Add(Telegraph.Strip(c, Quaternion.Euler(0f, lineAngles[k], 0f) * Vector3.forward, 9f, 0.8f).SetColor(new Color(0.85f, 0.4f, 1f, 0.7f)));
                }
                break;
            case GuardianKind.Recaudador when i == 0:
                signs.Add(Telegraph.Disc(c + lockedForward * 1.1f, 1.55f).SetColor(new Color(1f, 0.88f, 0.3f, 0.7f)));
                break;
            case GuardianKind.Recaudador:
            {
                // Tres sellos en abanico alrededor de donde estaba el jugador (fijos desde ya).
                Vector3 p = new Vector3(player.position.x, 0f, player.position.z);
                Vector3 dir = ToPlayer.normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                points[0] = p - side * 1.9f - dir * 0.6f;
                points[1] = p;
                points[2] = p + side * 1.9f - dir * 0.6f;
                for (int k = 0; k < 3; k++)
                {
                    points[k].y = GroundY(points[k] + Vector3.up * player.position.y);
                    signs.Add(Telegraph.Disc(points[k], 1.1f).SetColor(new Color(1f, 0.88f, 0.3f, 0.65f)));
                }
                break;
            }
            case GuardianKind.Barquero when i == 0:
            {
                bool fromLeft = Random.value < 0.5f;
                points[0] = new Vector3(fromLeft ? 1f : 0f, 0f, 0f);
                signs.Add(Telegraph.Fan(c, lockedForward, 130f, 0.5f, 3.8f).SetColor(new Color(0.35f, 1f, 0.9f, 0.7f)));
                // Espuma aqua del lado por donde va a pasar el remo.
                Vector3 sideDir = Quaternion.Euler(0f, fromLeft ? -65f : 65f, 0f) * lockedForward;
                Kit?.Foam(c + sideDir * 2.5f);
                break;
            }
            case GuardianKind.Barquero:
                // Las almas aparecen en la proa (las lanza en el tiempo activo).
                for (int k = 0; k < 3; k++)
                {
                    Vector3 at = transform.position + Vector3.up * 1.6f + Quaternion.Euler(0f, (k - 1) * 35f, 0f) * lockedForward * 1.8f;
                    GameObject soul = SoulProjectile.Spawn(at, Kit != null ? Kit.SoulMaterial : null, Kit != null ? Kit.FxMaterial : null);
                    pending.Add(soul);
                }
                break;
        }
    }

    private Vector3 Feet => new Vector3(transform.position.x, transform.position.y - hover, transform.position.z);

    private void UpdateAttack(float dt)
    {
        attackTime += dt;
        float t = attackTime;
        float active = Spec.activeTime;
        float prep = Mathf.Clamp01(t / active);
        foreach (Telegraph s in signs)
            if (s != null) s.SetFill(prep);

        switch (kind)
        {
            case GuardianKind.Fundidor:
                if (attackIndex == 0)
                {
                    // Sigue al jugador hasta 0.25 s antes del contacto.
                    if (t < active - 0.25f) { FacePlayer(1f - Mathf.Exp(-8f * dt)); lockedForward = transform.forward; signs[0].SetForward(lockedForward); }
                    if (Step(0, t >= active)) HitFan(80f, 0f, 3.8f, 0f, 1.3f, Spec.damage, true);
                }
                else
                {
                    if (furnace != null) furnace.intensity = Mathf.Lerp(0f, 6f, prep) * (0.8f + 0.2f * Mathf.Sin(t * 30f));
                    if (Step(0, t >= active))
                    {
                        Kit?.Play(kind, 1, transform.position);
                        if (furnace != null) Destroy(furnace.gameObject);
                        EnemyTracker.HitArea(Feet, 8f, Spec.damage * DemonDamageFactor, HitSource.Other, true, 2f, this, true);
                    }
                    if (firedSteps > 0)
                    {
                        // Onda que sale de 1 a 8 m en 1.1 s, banda de 0.65 m.
                        float k = Mathf.Clamp01((t - active) / 1.1f);
                        float r = Mathf.Lerp(1f, 8f, k);
                        signs[0].SetRadii(Mathf.Max(0.5f, r - 0.325f), r + 0.325f);
                        signs[0].SetColor(new Color(1f, 0.95f, 0.6f, 0.95f));
                        if (k < 1f && !hitPlayerThisAttack)
                        {
                            Vector3 d = PlayerFeet - Feet; float h = PlayerHeight;
                            float flat = new Vector2(d.x, d.z).magnitude;
                            if (Mathf.Abs(flat - r) <= 0.325f + PlayerRadius && h <= 0.75f && LineOfSight())
                                HurtPlayer(Spec.damage, false);
                        }
                    }
                }
                break;

            case GuardianKind.Viuda:
                if (attackIndex == 0)
                {
                    if (t < active - 0.2f) { FacePlayer(1f - Mathf.Exp(-6f * dt)); lockedForward = transform.forward; signs[0].SetForward(lockedForward); }
                    // El arco se va dibujando en el sentido del giro (izquierda → derecha).
                    if (t < active) signs[0].SetSweep(Mathf.Clamp01(prep * 1.2f), true);
                    if (Step(0, t >= active)) Kit?.Play(kind, 0, transform.position);
                    if (firedSteps > 0 && t <= active + 0.38f && !hitPlayerThisAttack)
                    {
                        float swept = -60f + 120f * Mathf.Clamp01((t - active) / 0.38f);
                        float a = SignedAngleToPlayer();
                        if (a >= -60f && a <= swept)
                            TryHitArc(4.8f, 0.4f, 1.6f, Spec.damage);
                    }
                }
                else
                {
                    // Las tres líneas giran durante la preparación y se fijan 0.35 s antes.
                    if (t < active - 0.35f)
                        for (int k = 0; k < 3; k++) lineAngles[k] += 70f * dt;
                    for (int k = 0; k < 3; k++)
                    {
                        signs[k].SetForward(Quaternion.Euler(0f, lineAngles[k], 0f) * Vector3.forward);
                        signs[k].SetCenter(Feet);
                    }
                    if (Step(0, t >= active)) Kit?.Play(kind, 1, transform.position);
                    if (firedSteps > 0 && !hitPlayerThisAttack)
                    {
                        float k = Mathf.Clamp01((t - active) / 0.65f);
                        float front = 9f * k;
                        for (int s = 0; s < 3; s++)
                        {
                            Vector3 dir = Quaternion.Euler(0f, lineAngles[s], 0f) * Vector3.forward;
                            Vector3 d = PlayerFeet - Feet; d.y = 0f;
                            float along = Vector3.Dot(d, dir);
                            float lateral = Mathf.Abs(Vector3.Dot(d, Vector3.Cross(Vector3.up, dir)));
                            if (along > 0f && along <= front && along >= front - 1.2f && lateral <= 0.4f + PlayerRadius && PlayerHeight < 1.2f && k < 1f)
                                HurtPlayer(Spec.damage, false);
                        }
                        if (k < 1f)
                            for (int s = 0; s < 3; s++)
                                Kit?.Pulse(Feet + Quaternion.Euler(0f, lineAngles[s], 0f) * Vector3.forward * front);
                    }
                }
                break;

            case GuardianKind.Recaudador:
                if (attackIndex == 0)
                {
                    if (Step(0, t >= active))
                    {
                        Kit?.Play(kind, 0, transform.position);
                        Vector3 c = Feet + lockedForward * 1.1f;
                        Kit?.Dust(c, 1.55f);
                        EnemyTracker.HitArea(c, 1.8f, Spec.damage * DemonDamageFactor, HitSource.Other, true, 3f, this, true);
                        Vector3 d = PlayerFeet - c;
                        if (new Vector2(d.x, d.z).magnitude <= 1.55f + PlayerRadius && PlayerHeight <= 1f)
                            HurtPlayer(Spec.damage, true);
                    }
                }
                else
                {
                    // Estampa en orden: activo, +0.30, +0.60. Un solo impacto por serie.
                    for (int k = 0; k < 3; k++)
                        if (Step(k, t >= active + 0.3f * k))
                        {
                            Kit?.Play(kind, 1, points[k]);
                            Kit?.Dust(points[k], 1.1f);
                            EnemyTracker.HitArea(points[k], 1.5f, Spec.damage * DemonDamageFactor, HitSource.Other, true, 3f, this, true);
                            if (signs.Count > k && signs[k] != null) { Destroy(signs[k].gameObject); }
                            Vector3 d = PlayerFeet - points[k];
                            if (!hitPlayerThisAttack && new Vector2(d.x, d.z).magnitude <= 1.1f + PlayerRadius && PlayerHeight <= 1f)
                                HurtPlayer(Spec.damage, true);
                        }
                }
                break;

            case GuardianKind.Barquero:
                if (attackIndex == 0)
                {
                    bool fromLeft = points[0].x > 0.5f;
                    if (t < active) signs[0].SetSweep(Mathf.Clamp01(prep * 1.2f), fromLeft);
                    if (Step(0, t >= active)) Kit?.Play(kind, 0, transform.position);
                    if (firedSteps > 0 && t <= active + 0.35f && !hitPlayerThisAttack)
                    {
                        float p = Mathf.Clamp01((t - active) / 0.35f);
                        float a = SignedAngleToPlayer();
                        bool swept = fromLeft ? a >= -65f && a <= -65f + 130f * p : a <= 65f && a >= 65f - 130f * p;
                        if (swept)
                            TryHitArc(3.8f, 0f, 1.5f, Spec.damage);
                    }
                }
                else
                {
                    // Las almas se quedan en la proa y salen en el tiempo activo, +0.22 y +0.44.
                    for (int k = 0; k < 3; k++)
                    {
                        if (k < pending.Count && pending[k] != null && firedSteps <= k)
                            pending[k].transform.position = transform.position + Vector3.up * 1.6f + Quaternion.Euler(0f, (k - 1) * 35f, 0f) * transform.forward * 1.8f;
                        if (Step(k, t >= active + 0.22f * k) && k < pending.Count && pending[k] != null)
                        {
                            pending[k].GetComponent<SoulProjectile>().Launch(player, Spec.damage, Kit != null ? Kit.SoulHitClip : null);
                            Kit?.Play(kind, 1, pending[k].transform.position);
                        }
                    }
                    if (t < active) FacePlayer(1f - Mathf.Exp(-4f * dt));
                }
                break;
        }

        if (t >= Spec.duration)
            EndAttack(true);
    }

    // Dispara el paso "step" una sola vez, aunque el FPS sea bajo.
    private bool Step(int step, bool reached)
    {
        if (!reached || firedSteps > step) return false;
        firedSteps = step + 1;
        return true;
    }

    private void EndAttack(bool recover)
    {
        foreach (Telegraph s in signs) if (s != null) Destroy(s.gameObject);
        signs.Clear();
        if (furnace != null) Destroy(furnace.gameObject);
        // Lo que no se lanzó se cancela (las almas ya lanzadas siguen su vuelo).
        for (int i = pending.Count - 1; i >= 0; i--)
            if (pending[i] != null && !pending[i].GetComponent<SoulProjectile>().Launched)
                Destroy(pending[i]);
        pending.RemoveAll(p => p == null);
        if (attackIndex >= 0)
            cooldownLeft[attackIndex] = attacks[attackIndex].cooldown;
        attackIndex = -1;
        if (recover)
        {
            state = State.Recover;
            stateTime = 0f;
            Play(idleClip, 0.15f);
            if (kind == GuardianKind.Recaudador)
            {
                strafe = Vector3.Cross(Vector3.up, ToPlayer.normalized) * (Random.value < 0.5f ? 1f : -1f);
                strafeTime = 0.7f;
            }
        }
    }

    // ------------------------------------------------------------------ golpes al jugador
    private Vector3 PlayerFeet => playerCollider != null ? new Vector3(player.position.x, playerCollider.bounds.min.y, player.position.z) : player.position;
    private float PlayerRadius => 0.35f;
    private float PlayerHeight => PlayerFeet.y - GroundY(player.position);

    private float SignedAngleToPlayer()
    {
        Vector3 d = PlayerFeet - Feet; d.y = 0f;
        return Vector3.SignedAngle(lockedForward, d, Vector3.up);
    }

    private void HitFan(float angle, float rMin, float rMax, float hMin, float hMax, float damage, bool knock)
    {
        Kit?.Play(kind, attackIndex, transform.position + lockedForward * 2f);
        Kit?.Dust(Feet + lockedForward * 2.2f, 1.6f);
        Vector3 d = PlayerFeet - Feet; d.y = 0f;
        float flat = d.magnitude;
        if (Mathf.Abs(SignedAngleToPlayer()) <= angle / 2f && flat <= rMax + PlayerRadius && flat >= rMin && PlayerHeight >= hMin - 0.1f && PlayerHeight <= hMax)
            HurtPlayer(damage, knock);
        HurtDemons(rMax, damage, angle, rMin);
    }

    // Los golpes del guardián también aplastan a los demonios que estén en medio (sin experiencia ni monedas).
    public const float DemonDamageFactor = 3f;
    private static readonly List<EnemyTracker> demonBuffer = new List<EnemyTracker>();

    private void HurtDemons(float reach, float damage, float angle = 360f, float rMin = 0f)
    {
        demonBuffer.Clear();
        foreach (EnemyTracker e in EnemyTracker.All)
        {
            if (e == null || e.IsDead || e.IsBoss || e.Enemy == this || e.GetComponent<Guardian>() != null) continue;
            Vector3 d = e.Center - Feet;
            if (d.y < -1f || d.y > 4f) continue;
            d.y = 0f;
            float m = d.magnitude;
            if (m > reach + 0.4f || m < rMin) continue;
            if (angle < 359f && m > 0.1f && Mathf.Abs(Vector3.SignedAngle(lockedForward, d, Vector3.up)) > angle / 2f) continue;
            demonBuffer.Add(e);
        }
        foreach (EnemyTracker e in demonBuffer)
            e.HitNoReward(damage * DemonDamageFactor, HitSource.Other);
    }

    private void TryHitArc(float reach, float hMin, float hMax, float damage)
    {
        Vector3 d = PlayerFeet - Feet; d.y = 0f;
        // El cuerpo del jugador mide ~2 m: lo toca si sus pies están por debajo de la franja alta.
        if (d.magnitude <= reach + PlayerRadius && PlayerHeight < hMax && PlayerHeight + 2f > hMin)
            HurtPlayer(damage, true);
        HurtDemons(reach, damage);
    }

    private bool LineOfSight()
    {
        Vector3 from = Feet + Vector3.up * 0.5f, to = PlayerFeet + Vector3.up * 0.4f;
        foreach (RaycastHit h in Physics.RaycastAll(from, (to - from).normalized, (to - from).magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (h.rigidbody == null && Vector3.Angle(h.normal, Vector3.up) > 50f)
                return false;   // pared o desnivel fuerte
        return true;
    }

    private void HurtPlayer(float damage, bool knock)
    {
        if (hitPlayerThisAttack || isDead) return;
        hitPlayerThisAttack = true;
        if (!GuardianKit.TryHitPlayer()) return;   // respiro global entre golpes de guardián
        PlayerVitals.Damage(pogo, damage);
        if (knock)
        {
            var prb = pogo.GetComponent<Rigidbody>();
            Vector3 d = ToPlayer.normalized;
            prb.linearVelocity = d * 7f + Vector3.up * 6f;
        }
    }

    // ------------------------------------------------------------------ daño al guardián
    // El pisotón de PogoPlayer solo le pega a Zombie: aquí vemos si Pogoman cayó encima o junto.
    private void CheckStomp()
    {
        bool grounded = PogoBridge.IsGrounded(pogo);
        if (!grounded || wasGrounded || body == null) return;
        float radius = PogoBridge.GetFloat(pogo, "attackRadius", 2.5f);
        Vector3 feet = PlayerFeet;
        if (Vector3.Distance(body.ClosestPoint(feet), feet) <= radius)
            TakeDamage(PogoBridge.GetFloat(pogo, "attackDamage", 50f));
    }

    // Lo llaman el escupitajo, los objetos y el pisotón.
    public void TakeDamage(float damage)
    {
        if (isDead || state == State.Emerge || damage <= 0f) return;
        currentHealth -= damage;
        hitShake = Mathf.Max(hitShake, 0.6f);
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Defeat();
            return;
        }
        // Reacción solo fuera de un ataque y no en cada golpe de un multigolpe.
        if ((state == State.Chase || state == State.Recover) && flinchCooldown <= 0f)
        {
            flinchCooldown = 1.6f;
            state = State.Flinch;
            stateTime = 0f;
            Play(hitClip, 0.05f);
        }
    }

    private void Defeat()
    {
        isDead = true;
        // Todo lo pendiente del encuentro se apaga ya.
        foreach (Telegraph s in signs) if (s != null) Destroy(s.gameObject);
        signs.Clear();
        foreach (GameObject p in pending) if (p != null) Destroy(p);
        pending.Clear();
        if (furnace != null) Destroy(furnace.gameObject);
        attackIndex = -1;
        state = State.Defeat;
        stateTime = 0f;
        Play(defeatClip, 0.05f);
        Kit?.OnGuardianDefeated(this);
    }

    private void UpdateDefeat()
    {
        // Después de la animación de derrota se hunde girando y desaparece (el pilar da la llave).
        if (stateTime > defeatLength + 0.3f)
        {
            float k = Mathf.Clamp01((stateTime - defeatLength - 0.3f) / 1.2f);
            if (model != null) model.localRotation = Quaternion.Euler(0f, 540f * k * k, 0f);
            transform.position += Vector3.down * Time.deltaTime * 2.5f * k;
            if (k >= 1f) Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        foreach (Telegraph s in signs) if (s != null) Destroy(s.gameObject);
        foreach (GameObject p in pending) if (p != null) Destroy(p);
        if (furnace != null) Destroy(furnace.gameObject);
    }

    private void Play(string clip, float fade = 0.15f)
    {
        if (anim != null && !string.IsNullOrEmpty(clip) && anim.isActiveAndEnabled)
            anim.CrossFadeInFixedTime(clip, fade);
    }

    // ---- Lo llena el editor desde Balance.json ----
    public void Configure(GuardianKind k, float health, float speed, string idle, string move, string hit, string defeat, float defeatLen,
        GuardianAttackSpec a, GuardianAttackSpec b, float hoverHeight)
    {
        kind = k; maxHealth = health; moveSpeed = speed;
        idleClip = idle; moveClip = move; hitClip = hit; defeatClip = defeat; defeatLength = defeatLen;
        attacks = new[] { a, b };
        hover = hoverHeight;
    }
}
