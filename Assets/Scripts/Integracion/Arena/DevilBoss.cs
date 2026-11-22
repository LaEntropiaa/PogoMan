using System.Collections.Generic;
using UnityEngine;

// El Diablo Tendero, jefe final de la arena.
// Ciclo: vuela por el cielo invulnerable lanzando bolas de fuego e invoca una oleada
// (hordas chicas intercaladas con jefes de pilar). Cuando matas a toda la oleada baja al suelo
// y puedes golpearlo durante unos segundos; luego vuelve a subir y empieza otra oleada.
public class DevilBoss : Enemy
{
    public enum Phase { Intro, Rising, Flying, Descending, Vulnerable, Dying, Enraging }

    [Header("Vida")]
    [SerializeField] private float maxHealth = 2600f;
    [Tooltip("Vida extra por cada nivel de Pogoman (si llegas muy cheto, aguanta más).")]
    [SerializeField] private float healthPerLevel = 140f;

    [Header("Fases")]
    [SerializeField] private float introTime = 4.5f;
    [SerializeField] private float riseTime = 1.6f;
    [SerializeField] private float descendTime = 1.6f;
    [SerializeField] private float vulnerableTime = 5f;
    [SerializeField] private float flyHeight = 13f;
    [SerializeField] private float orbitRadius = 15f;
    [SerializeField] private float orbitSpeed = 0.25f;   // vueltas... en radianes por segundo

    [Header("Oleadas")]
    [SerializeField] private int hordeBase = 8;
    [SerializeField] private int hordePerWave = 4;
    [SerializeField] private int maxHorde = 30;
    [SerializeField] private float spawnInterval = 0.22f;
    [Tooltip("Demonios que acompañan a los jefes de pilar.")]
    [SerializeField] private int escortCount = 6;
    [Tooltip("Mini jefes en cada horda (además de los demonios), +1 cada dos oleadas.")]
    [SerializeField] private int miniBossesPerHorde = 1;

    [Header("Bolas de fuego")]
    [SerializeField] private float fireballInterval = 2.8f;
    [SerializeField] private float enragedFireballInterval = 1.7f;
    [SerializeField] private float fireballDamage = 20f;

    [Header("Lluvia de magia (cada pocas bolas)")]
    [SerializeField] private int rainEvery = 3;
    [SerializeField] private int rainOrbs = 7;
    [SerializeField] private float rainDamage = 12f;

    [Header("Golpe al aterrizar")]
    [SerializeField] private float slamRadius = 6f;
    [SerializeField] private float slamDamage = 22f;
    [SerializeField] private float fireballRadius = 2.4f;
    [SerializeField] private float fireballFlight = 1.3f;

    [Header("Golpe del pogo")]
    [Tooltip("Qué tan cerca (horizontal) tienes que caer para que el pisotón le pegue.")]
    [SerializeField] private float stompRadius = 3.4f;
    [Tooltip("Qué tanto baja el cuerpo al quedar hincado (fracción de su altura).")]
    [SerializeField] private float kneelDrop = 0.27f;
    [Tooltip("Altura del collider hincado (fracción de la normal).")]
    [SerializeField] private float kneelColliderHeight = 0.55f;

    [Header("Ataques especiales")]
    [SerializeField] private float specialInterval = 6.5f;
    [SerializeField] private float pillarDamage = 24f;
    [SerializeField] private float ringDamage = 20f;
    [SerializeField] private float diveDamage = 30f;

    [Header("Segunda fase (FURIA)")]
    [Tooltip("Segundos que hay que sobrevivir antes de que baje.")]
    [SerializeField] private float surviveTime = 10f;
    [SerializeField] private float enrageTime = 3.2f;
    [Tooltip("En la furia todo va más rápido (multiplica las recargas).")]
    [SerializeField] private float furyCooldownFactor = 0.55f;
    [SerializeField] private int furyFlyers = 4;

    // ---- Estado público (HUD) ----
    public Phase State { get; private set; } = Phase.Intro;
    public int Wave { get; private set; }
    public bool PillarWave => Wave % 2 == 1;
    public int WaveRemaining => pendingSpawns + CountAlive();
    public float VulnerableLeft => State == Phase.Vulnerable ? vulnerableTimer : 0f;
    public float VulnerableTotal => vulnerableTime;
    public float Health => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public bool Fury { get; private set; }
    public float SurviveLeft => Fury && State == Phase.Flying ? Mathf.Max(0f, surviveTime - stateTimer) : 0f;
    public float IntroTime { get => introTime; set => introTime = value; }
    public event System.Action<Phase> PhaseChanged;

    // Campos con los nombres que lee PogoBridge (barra de vida, EnemyTracker...).
    private float currentHealth;
    private bool isDead;

    private ArenaFight fight;
    private Transform player;
    private PogoPlayer pogo;
    private Vector3 center;
    private Vector3 groundPoint;
    private Vector3 fromPoint, toPoint;
    private float stateTimer;
    private float vulnerableTimer;
    private float orbitAngle;
    private float fireTimer;
    private int pendingSpawns;
    private Enemy pendingPrefab;
    private float spawnTimer;
    private readonly List<Enemy> waveEnemies = new List<Enemy>();
    private bool wasGrounded;
    private Transform model;
    private EnemyTracker tracker;
    private int castCount;
    public DevilRig Rig { get; set; }

    public void Setup(ArenaFight owner, Transform modelRoot, Vector3 arenaCenter, Vector3 ground, PogoPlayer target)
    {
        fight = owner;
        model = modelRoot;
        center = arenaCenter;
        groundPoint = ground;
        pogo = target;
        player = target.transform;
        int level = PlayerStats.Instance != null ? PlayerStats.Instance.Level : 1;
        maxHealth += healthPerLevel * (level - 1);
        currentHealth = maxHealth;
        tracker = GetComponent<EnemyTracker>();
        if (tracker != null)
        {
            tracker.CustomUi = true;
            tracker.Targetable = false;
        }
        transform.position = ground;
        orbitAngle = Mathf.Atan2(ground.z - center.z, ground.x - center.x);
        SetState(Phase.Intro);
    }

    protected override void Awake()
    {
        base.Awake();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    // Enemy lo movería hacia el objetivo: aquí el movimiento es propio.
    protected override void FixedUpdate() { }

    private void SetState(Phase p)
    {
        State = p;
        stateTimer = 0f;
        if (tracker != null)
            tracker.Targetable = p == Phase.Vulnerable;
        if (Rig != null)
            Rig.State = p == Phase.Intro ? DevilRig.Pose.Intro
                : p == Phase.Vulnerable || p == Phase.Enraging ? DevilRig.Pose.Stunned
                : p == Phase.Dying ? DevilRig.Pose.Dying
                : DevilRig.Pose.Fly;
        PhaseChanged?.Invoke(p);
    }

    private void Update()
    {
        if (isDead || player == null)
            return;

        float dt = Time.deltaTime;
        stateTimer += dt;
        FacePlayer(dt);

        switch (State)
        {
            case Phase.Intro:
                if (stateTimer >= introTime)
                    StartRise();
                break;

            case Phase.Rising:
                MoveArc(stateTimer / riseTime, true);
                if (stateTimer >= riseTime)
                {
                    SetState(Phase.Flying);
                    if (Fury) { fireTimer = 0.8f; specialTimer = 1.5f; flyerTimer = 0.5f; fight.OnSurviveStarted(); }
                    else StartWave();
                }
                break;

            case Phase.Flying:
                if (!UpdateDive(dt))
                    Fly(dt);
                Fireballs(dt);
                Specials(dt);
                if (Fury)
                {
                    // Furia: sin oleadas, solo unos ojos que hacen bulto. Sobrevive y baja.
                    KeepFlyers(dt);
                    if (stateTimer >= surviveTime && diveT < 0f)
                        StartDescend();
                }
                else
                {
                    SpawnPending(dt);
                    if (pendingSpawns == 0 && CountAlive() == 0 && stateTimer > 2f && diveT < 0f)
                        StartDescend();
                }
                break;

            case Phase.Enraging:
            {
                // Se levanta rojo y recupera toda su vida.
                float k = Mathf.Clamp01(stateTimer / enrageTime);
                currentHealth = Mathf.Lerp(0.01f, maxHealth, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.2f) / 0.7f)));
                SetKneel(1f - Mathf.Clamp01((k - 0.6f) / 0.3f));
                model.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(stateTimer * 40f) * 2.5f * (1f - k));
                fight.OnEnrageTick(k);
                if (stateTimer >= enrageTime)
                {
                    SetKneel(0f);
                    model.localRotation = Quaternion.identity;
                    currentHealth = maxHealth;
                    StartRise();
                }
                break;
            }

            case Phase.Descending:
                MoveArc(stateTimer / descendTime, false);
                if (stateTimer >= descendTime)
                {
                    Slam();
                    vulnerableTimer = vulnerableTime;
                    SetState(Phase.Vulnerable);
                    fight.OnVulnerable();
                }
                break;

            case Phase.Vulnerable:
                vulnerableTimer -= dt;
                // Hincado y mareado: baja el cuerpo y el collider para que el pogo lo alcance.
                SetKneel(Mathf.Clamp01(stateTimer / 0.35f) * Mathf.Clamp01(vulnerableTimer / 0.3f));
                model.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(stateTimer * 5f) * 3f);
                CheckStomp();
                if (vulnerableTimer <= 0f)
                {
                    SetKneel(0f);
                    model.localRotation = Quaternion.identity;
                    Wave++;
                    StartRise();
                }
                break;
        }

        wasGrounded = PogoBridge.IsGrounded(pogo);
    }

    private CapsuleCollider capsule;
    private float standHeight = -1f;

    private void SetKneel(float k)
    {
        if (capsule == null) capsule = GetComponent<CapsuleCollider>();
        if (capsule == null) return;
        if (standHeight < 0f) standHeight = capsule.height;
        float e = k * k * (3f - 2f * k);
        model.localPosition = Vector3.down * standHeight * kneelDrop * e;
        capsule.height = Mathf.Lerp(standHeight, standHeight * kneelColliderHeight, e);
        capsule.center = Vector3.up * capsule.height * 0.5f;
    }

    // ---- Movimiento ----
    private Vector3 SkyPoint(float angle) =>
        center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitRadius + Vector3.up * flyHeight;

    private void StartRise()
    {
        fromPoint = transform.position;
        toPoint = SkyPoint(orbitAngle);
        SetState(Phase.Rising);
        Rig?.Laugh();
        fight.OnRise();
    }

    private void StartDescend()
    {
        ClearSigns();
        fromPoint = transform.position;
        // Baja a un punto del suelo entre el centro y Pogoman (para que llegue a pegarle).
        Vector3 toward = player.position - center;
        toward.y = 0f;
        Vector3 candidate = center + Vector3.ClampMagnitude(toward, 9f) * 0.6f;
        toPoint = fight.GroundAt(candidate, groundPoint);
        SetState(Phase.Descending);
        fight.OnDescend();
    }

    private void MoveArc(float k, bool up)
    {
        k = Mathf.Clamp01(k);
        float e = k * k * (3f - 2f * k);
        Vector3 p = Vector3.Lerp(fromPoint, toPoint, e);
        p.y += Mathf.Sin(k * Mathf.PI) * (up ? 3f : 2f);
        rb.MovePosition(p);
        transform.position = p;
    }

    private void Fly(float dt)
    {
        orbitAngle += orbitSpeed * dt * (Health < MaxHealth * 0.5f ? 1.5f : 1f);
        Vector3 p = SkyPoint(orbitAngle) + Vector3.up * Mathf.Sin(stateTimer * 1.7f) * 1.2f;
        p = Vector3.Lerp(transform.position, p, 1f - Mathf.Exp(-3f * dt));
        rb.MovePosition(p);
        transform.position = p;
        model.localRotation = Quaternion.Euler(Mathf.Sin(stateTimer * 2f) * 4f, 0f, Mathf.Sin(stateTimer * 1.3f) * 7f);
    }

    private void FacePlayer(float dt)
    {
        Vector3 look = player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.01f)
            return;
        Quaternion target = Quaternion.LookRotation(look.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-4f * dt));
    }

    // ---- Oleadas ----
    private void StartWave()
    {
        waveEnemies.Clear();
        spawnTimer = 0.6f;
        if (PillarWave)
        {
            // Varios jefes de pilar (con sus habilidades) y demonios de escolta.
            int bosses = 2 + Wave / 3;
            for (int i = 0; i < bosses; i++)
                fight.Summon(fight.PillarBossPrefab, waveEnemies, true);
            pendingPrefab = null;
            pendingSpawns = escortCount + Wave;
        }
        else
        {
            // Horda con mini jefes metidos.
            int minis = miniBossesPerHorde + Wave / 2;
            for (int i = 0; i < minis; i++)
                fight.Summon(fight.PillarBossPrefab, waveEnemies, true);
            pendingPrefab = null;
            pendingSpawns = Mathf.Min(maxHorde, hordeBase + hordePerWave * (Wave / 2));
        }
        Rig?.Summon();
        fight.OnWaveStarted(Wave, PillarWave);
        fireTimer = 1.5f;
    }

    private void SpawnPending(float dt)
    {
        if (pendingSpawns <= 0)
            return;
        spawnTimer -= dt;
        if (spawnTimer > 0f)
            return;
        spawnTimer = spawnInterval;
        if (fight.Summon(pendingPrefab, waveEnemies, false))
            pendingSpawns--;
    }

    private int CountAlive()
    {
        int alive = 0;
        for (int i = waveEnemies.Count - 1; i >= 0; i--)
        {
            Enemy e = waveEnemies[i];
            if (e == null || PogoBridge.IsDead(e))
                waveEnemies.RemoveAt(i);
            else
                alive++;
        }
        return alive;
    }

    // ---- Bolas de fuego ----
    private void Fireballs(float dt)
    {
        fireTimer -= dt;
        if (fireTimer > 0f)
            return;

        bool enraged = Fury || Health < MaxHealth * 0.5f;
        fireTimer = (enraged ? enragedFireballInterval : fireballInterval) * Random.Range(0.8f, 1.2f) * (Fury ? furyCooldownFactor : 1f);
        float flight = Fury ? fireballFlight * 0.75f : fireballFlight;

        // Apunta un poco adelante de donde va Pogoman.
        Vector3 v = pogo.GetComponent<Rigidbody>().linearVelocity;
        v.y = 0f;
        Vector3 aim = fight.GroundAt(player.position + v * flight * 0.7f, player.position);
        Vector3 hand = transform.position + Vector3.up * 4f + transform.forward * 1.2f;

        // Enojado (menos de la mitad de vida) lanza tres: al centro y a los lados.
        Vector3 toAim = aim - hand;
        toAim.y = 0f;
        Vector3 side = Vector3.Cross(Vector3.up, toAim.sqrMagnitude > 0.01f ? toAim.normalized : Vector3.forward) * 3.5f;
        int shots = Fury ? 5 : enraged ? 3 : 1;
        for (int i = 0; i < shots; i++)
        {
            Vector3 off = i == 0 ? Vector3.zero : (i % 2 == 1 ? side : -side) * ((i + 1) / 2);
            Vector3 t = i == 0 ? aim : fight.GroundAt(aim + off, aim);
            Fireball.Launch(hand, t, flight, fireballDamage, fireballRadius, fight.FireballMaterial, fight.FxMaterial, fight.ExplodeClip);
        }
        fight.OnFireball(hand);
        Rig?.Cast();

        // Cada pocas bolas, además, una lluvia de orbes morados alrededor de Pogoman.
        castCount++;
        if (rainEvery > 0 && castCount % (Fury ? 2 : rainEvery) == 0)
            MagicRain();
    }

    private void MagicRain()
    {
        for (int i = 0; i < rainOrbs; i++)
        {
            Vector2 c = Random.insideUnitCircle * 7f;
            Vector3 from = player.position + new Vector3(c.x, 14f + Random.value * 4f, c.y);
            Vector3 v = Vector3.down * Random.Range(6f, 9f) + new Vector3(-c.x, 0f, -c.y) * 0.15f;
            MagicOrb.Launch(from, v, i % 3 == 0 ? player : null, rainDamage, BossCaster.OrbMaterial, fight.FxMaterial, BossCaster.ImpactClip);
        }
    }

    // Al bajar golpea el suelo: onda que daña y empuja si estás cerca.
    private void Slam()
    {
        fight.OnSlam(transform.position);
        Vector3 d = player.position - transform.position;
        float flat = new Vector2(d.x, d.z).magnitude;
        if (flat <= slamRadius && d.y < 3f)
        {
            PlayerVitals.Damage(pogo, slamDamage * (1f - flat / slamRadius * 0.5f));
            var body = pogo.GetComponent<Rigidbody>();
            Vector3 push = new Vector3(d.x, 0f, d.z).normalized * 10f + Vector3.up * 7f;
            body.linearVelocity = push;
        }
    }

    // ---- Segunda fase ----
    private void StartFury()
    {
        Fury = true;
        currentHealth = 0.01f;
        diveT = -1f;
        ClearSigns();
        // Su oleada se muere (sin recompensa): en la furia solo quedan unos ojos.
        foreach (Enemy e in waveEnemies)
            if (e != null && !PogoBridge.IsDead(e))
                e.GetComponent<EnemyTracker>()?.HitNoReward(999999f);
        waveEnemies.Clear();
        pendingSpawns = 0;
        SetState(Phase.Enraging);
        fight.OnEnrage(this);
    }

    private float flyerTimer;

    private void KeepFlyers(float dt)
    {
        flyerTimer -= dt;
        if (flyerTimer > 0f)
            return;
        flyerTimer = 1.6f;
        if (CountAlive() < furyFlyers)
            fight.SummonFlyer(waveEnemies);
    }

    // ---- Ataques especiales (columnas de fuego, anillo de fuego, picada) ----
    private float specialTimer = 3f;
    private int lastSpecial = -1;
    private readonly List<Telegraph> signs = new List<Telegraph>();

    private void Specials(float dt)
    {
        if (diveT >= 0f)
            return;
        specialTimer -= dt;
        if (specialTimer > 0f)
            return;
        specialTimer = specialInterval * Random.Range(0.85f, 1.15f) * (Fury ? furyCooldownFactor : 1f);

        int pick;
        do pick = Random.Range(0, 3); while (pick == lastSpecial);
        lastSpecial = pick;
        switch (pick)
        {
            case 0: StartCoroutine(FirePillars()); break;
            case 1: StartCoroutine(FireRing()); break;
            default: StartDive(); break;
        }
    }

    private void ClearSigns()
    {
        foreach (Telegraph t in signs) if (t != null) Destroy(t.gameObject);
        signs.Clear();
    }

    private static bool PlayerNear(Vector3 p, Vector3 point, float radius, float maxHeight)
    {
        Vector3 d = p - point;
        return new Vector2(d.x, d.z).magnitude <= radius + 0.35f && d.y < maxHeight && d.y > -1.5f;
    }

    // Columnas de fuego: marca círculos donde estás (y alrededor) y luego hacen erupción.
    private System.Collections.IEnumerator FirePillars()
    {
        Rig?.Summon();
        fight.OnSpecial(0);
        int count = Fury ? 7 : 4;
        float warn = Fury ? 0.85f : 1.2f;
        var spots = new List<Vector3>();
        Vector3 vel = pogo.GetComponent<Rigidbody>().linearVelocity; vel.y = 0f;
        for (int i = 0; i < count; i++)
        {
            // Una donde estás, otra donde vas a estar y las demás alrededor.
            Vector2 r = Random.insideUnitCircle * 7f;
            Vector3 c = i == 0 ? player.position : i == 1 ? player.position + vel * warn : player.position + new Vector3(r.x, 0f, r.y);
            Vector3 g = fight.GroundAt(c, player.position);
            spots.Add(g);
            Telegraph t = Telegraph.Disc(g, 1.9f).SetColor(new Color(1f, 0.35f, 0.05f, 0.45f));
            signs.Add(t);
        }
        for (float k = 0f; k < warn; k += Time.deltaTime)
        {
            foreach (Telegraph t in signs) if (t != null) t.SetFill(k / warn);
            if (isDead) yield break;
            yield return null;
        }
        ClearSigns();
        bool hitDone = false;
        foreach (Vector3 g in spots)
        {
            fight.OnPillarErupt(g);
            if (!hitDone && PlayerNear(player.position, g, 1.9f, 6f))
            {
                hitDone = true;
                PlayerVitals.Damage(pogo, pillarDamage);
                var body = pogo.GetComponent<Rigidbody>();
                body.linearVelocity = new Vector3(body.linearVelocity.x, 13f, body.linearVelocity.z);
            }
        }
    }

    // Anillo de fuego: sale del centro de la arena por el suelo. Hay que saltarlo.
    private System.Collections.IEnumerator FireRing()
    {
        Rig?.Cast();
        fight.OnSpecial(1);
        Vector3 c = fight.GroundAt(center, groundPoint);
        Telegraph t = Telegraph.Ring(c, 0.5f, 1.3f).SetColor(new Color(1f, 0.25f, 0.05f, 0.6f));
        signs.Add(t);
        float speed = Fury ? 13f : 9f;
        float maxR = 30f;
        int rings = Fury ? 2 : 1;
        for (int n = 0; n < rings; n++)
        {
            bool hit = false;
            for (float r = 1f; r < maxR; r += speed * Time.deltaTime)
            {
                if (isDead || t == null) yield break;
                t.SetRadii(Mathf.Max(0f, r - 0.6f), r + 0.6f);
                t.SetFill(1f);
                fight.OnRingTick(c, r);
                Vector3 d = player.position - c;
                float flat = new Vector2(d.x, d.z).magnitude;
                float height = player.position.y - 1f - Telegraph.GroundY(player.position, c.y);
                if (!hit && Mathf.Abs(flat - r) < 0.9f && height < 1.1f)
                {
                    hit = true;
                    PlayerVitals.Damage(pogo, ringDamage);
                    var body = pogo.GetComponent<Rigidbody>();
                    Vector3 out_ = new Vector3(d.x, 0f, d.z).normalized;
                    body.linearVelocity = out_ * 8f + Vector3.up * 9f;
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.35f);
        }
        signs.Remove(t);
        if (t != null) Destroy(t.gameObject);
    }

    // Picada: marca dónde estás, se lanza en picada, golpea el suelo y regresa al cielo.
    private float diveT = -1f;
    private Vector3 diveFrom, diveTo;
    private Telegraph diveSign;
    private bool diveSlammed;

    private void StartDive()
    {
        diveT = 0f;
        diveSlammed = false;
        diveFrom = transform.position;
        diveTo = fight.GroundAt(player.position, groundPoint);
        diveSign = Telegraph.Disc(diveTo, 4f).SetColor(new Color(0.85f, 0.1f, 0.25f, 0.45f));
        signs.Add(diveSign);
        Rig?.Laugh();
        fight.OnSpecial(2);
    }

    // true mientras la picada controla la posición.
    private bool UpdateDive(float dt)
    {
        if (diveT < 0f)
            return false;
        diveT += dt;
        float warn = Fury ? 0.8f : 1.15f, down = 0.35f, stay = 0.35f, up = 0.9f;
        if (diveT < warn)
        {
            // Sigue un poquito a Pogoman mientras avisa.
            diveTo = Vector3.Lerp(diveTo, fight.GroundAt(player.position, diveTo), dt * 1.5f);
            if (diveSign != null) { diveSign.SetCenter(diveTo); diveSign.SetFill(diveT / warn); }
            Vector3 hover = diveFrom + Vector3.up * Mathf.Sin(diveT / warn * Mathf.PI) * 1.5f;
            rb.MovePosition(hover);
            transform.position = hover;
        }
        else if (diveT < warn + down)
        {
            float k = (diveT - warn) / down;
            Vector3 p = Vector3.Lerp(diveFrom, diveTo, k * k);
            rb.MovePosition(p);
            transform.position = p;
        }
        else if (diveT < warn + down + stay)
        {
            if (!diveSlammed)
            {
                diveSlammed = true;
                signs.Remove(diveSign);
                if (diveSign != null) Destroy(diveSign.gameObject);
                fight.OnSlam(diveTo);
                if (PlayerNear(player.position, diveTo, 4f, 3.5f))
                {
                    PlayerVitals.Damage(pogo, diveDamage);
                    Vector3 d = player.position - diveTo; d.y = 0f;
                    pogo.GetComponent<Rigidbody>().linearVelocity = d.normalized * 12f + Vector3.up * 8f;
                }
            }
        }
        else if (diveT < warn + down + stay + up)
        {
            float k = (diveT - warn - down - stay) / up;
            Vector3 p = Vector3.Lerp(diveTo, SkyPoint(orbitAngle), Mathf.SmoothStep(0f, 1f, k));
            rb.MovePosition(p);
            transform.position = p;
        }
        else
        {
            diveT = -1f;
            return false;
        }
        return true;
    }

    // ---- Daño ----
    // El pisotón de PogoPlayer solo le pega a Zombie: aquí revisamos si Pogoman cayó encima o junto a él.
    private void CheckStomp()
    {
        bool grounded = PogoBridge.IsGrounded(pogo);
        if (!grounded || wasGrounded)
            return;

        Vector3 d = player.position - transform.position;
        float vertical = d.y;
        d.y = 0f;
        if (d.magnitude <= stompRadius && vertical > -1f && vertical < 9f)
            TakeDamage(PogoBridge.GetFloat(pogo, "attackDamage", 50f));
    }

    // Lo llaman el escupitajo, los objetos y el pisotón (SendMessage "TakeDamage").
    public void TakeDamage(float damage)
    {
        if (isDead || State != Phase.Vulnerable || damage <= 0f)
            return;

        currentHealth -= damage;
        Rig?.Hit();
        fight.OnBossHit(damage);
        if (currentHealth <= 0f && !Fury)
        {
            // La primera vez no se rinde: entra en FURIA (rojo, más rápido, recupera su vida).
            StartFury();
            return;
        }
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            isDead = true;
            State = Phase.Dying;
            PhaseChanged?.Invoke(Phase.Dying);
            fight.OnBossDefeated(this);
        }
    }
}
