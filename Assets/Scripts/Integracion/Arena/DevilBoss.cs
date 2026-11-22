using System.Collections.Generic;
using UnityEngine;

// El Diablo Tendero, jefe final de la arena.
// Ciclo: vuela por el cielo invulnerable lanzando bolas de fuego e invoca una oleada
// (hordas chicas intercaladas con jefes de pilar). Cuando matas a toda la oleada baja al suelo
// y puedes golpearlo durante unos segundos; luego vuelve a subir y empieza otra oleada.
public class DevilBoss : Enemy
{
    public enum Phase { Intro, Rising, Flying, Descending, Vulnerable, Dying }

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
                : p == Phase.Vulnerable ? DevilRig.Pose.Stunned
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
                    StartWave();
                }
                break;

            case Phase.Flying:
                Fly(dt);
                SpawnPending(dt);
                Fireballs(dt);
                if (pendingSpawns == 0 && CountAlive() == 0 && stateTimer > 2f)
                    StartDescend();
                break;

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

        bool enraged = Health < MaxHealth * 0.5f;
        fireTimer = (enraged ? enragedFireballInterval : fireballInterval) * Random.Range(0.8f, 1.2f);

        // Apunta un poco adelante de donde va Pogoman.
        Vector3 v = pogo.GetComponent<Rigidbody>().linearVelocity;
        v.y = 0f;
        Vector3 aim = fight.GroundAt(player.position + v * fireballFlight * 0.7f, player.position);
        Vector3 hand = transform.position + Vector3.up * 4f + transform.forward * 1.2f;

        // Enojado (menos de la mitad de vida) lanza tres: al centro y a los lados.
        Vector3 toAim = aim - hand;
        toAim.y = 0f;
        Vector3 side = Vector3.Cross(Vector3.up, toAim.sqrMagnitude > 0.01f ? toAim.normalized : Vector3.forward) * 3.5f;
        int shots = enraged ? 3 : 1;
        for (int i = 0; i < shots; i++)
        {
            Vector3 t = i == 0 ? aim : fight.GroundAt(aim + (i == 1 ? side : -side), aim);
            Fireball.Launch(hand, t, fireballFlight, fireballDamage, fireballRadius, fight.FireballMaterial, fight.FxMaterial, fight.ExplodeClip);
        }
        fight.OnFireball(hand);
        Rig?.Cast();

        // Cada pocas bolas, además, una lluvia de orbes morados alrededor de Pogoman.
        castCount++;
        if (rainEvery > 0 && castCount % rainEvery == 0)
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
